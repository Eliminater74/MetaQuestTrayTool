using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Kill Meta Dash and launch SteamVR so Air Link / wired Link can drive SteamVR games
/// (MSFS, etc.) without the Meta dashboard owning the session.
/// Runtime behaviour inspired by
/// <see href="https://github.com/DevOculus-Meta-Quest/OculusKiller">DevOculus-Meta-Quest/OculusKiller</see>
/// — launches <c>vrstartup.exe</c> from <c>openvrpaths.vrpath</c> and terminates Dash;
/// does <b>not</b> replace <c>OculusDash.exe</c> on disk.
/// Also supports the OculusKiller registry alternative:
/// <c>HKLM\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus\Config\PreventDashLaunch</c> = 1.
/// </summary>
public sealed class DashToSteamVrService : IDisposable
{
    public const string PreventDashLaunchValueName = "PreventDashLaunch";
    public const string CoreChannelValueName = "CoreChannel";

    public static readonly string[] PreventDashLaunchKeyPaths =
    [
        @"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus\Config",
        @"SOFTWARE\Oculus VR, LLC\Oculus\Config"
    ];

    /// <summary>Known Meta Quest Link PC client channels (CoreChannel).</summary>
    public static readonly string[] KnownCoreChannels =
    [
        "LIVE",
        "PublicTest",
        "NO_UPDATES"
    ];

    private static readonly string[] DashProcessNames =
    [
        "OculusDash"
    ];

    private readonly App _app;
    private readonly DispatcherTimer _sessionTimer;
    private readonly DispatcherTimer _dashReaper;
    private bool _ranThisMetaSession;
    private bool _wasMetaSession;

    public DashToSteamVrService(App app)
    {
        _app = app;
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _sessionTimer.Tick += (_, _) => PollAuto();
        _dashReaper = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dashReaper.Tick += (_, _) => ReapDashIfNeeded();
    }

    public DashToSteamVrSettings Settings => _app.Settings.Current.DashToSteamVr;

    public void Start()
    {
        if (_sessionTimer.IsEnabled)
        {
            return;
        }

        _sessionTimer.Start();
        PollAuto();
    }

    public void Dispose()
    {
        _sessionTimer.Stop();
        _dashReaper.Stop();
    }

    /// <summary>Manual / hotkey / voice entry point.</summary>
    public string RunNow(string reason = "manual")
    {
        var parts = new List<string>();
        parts.Add(KillDash(hard: true));

        if (Settings.CloseMetaClient)
        {
            parts.Add(CloseMetaClient());
        }

        if (Settings.SwitchOpenXrToSteamVr)
        {
            var openXr = _app.OpenXr.ReadActiveKind();
            if (openXr != OpenXrRuntimeKind.SteamVr)
            {
                parts.Add(_app.OpenXr.Set(OpenXrRuntimeKind.SteamVr));
            }
            else
            {
                parts.Add("OpenXR already SteamVR.");
            }
        }

        parts.Add(LaunchSteamVr());

        if (Settings.KeepKillingDashWhileSteamVr && !IsPreventDashLaunchEnabled())
        {
            _dashReaper.Start();
            parts.Add("Dash reaper on while SteamVR runs.");
        }
        else if (IsPreventDashLaunchEnabled())
        {
            parts.Add("PreventDashLaunch is on — Dash should not spawn.");
        }

        var summary = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        _app.Log.Info($"Dash → SteamVR ({reason}): {summary}");
        if (_app.Settings.Current.ShowNotifications)
        {
            _app.TrayNotify("Dash → SteamVR", summary);
        }

        return summary;
    }

    public string DescribeSteamVrPaths()
    {
        var paths = TryResolveSteamVrPaths();
        if (paths is null)
        {
            return "SteamVR paths not found (run SteamVR once so openvrpaths.vrpath exists).";
        }

        return $"SteamVR startup: {paths.Value.StartupPath}";
    }

    public string DescribePreventDashLaunch()
    {
        var live = IsPreventDashLaunchEnabled();
        return live
            ? "PreventDashLaunch: ON — Dash blocked; SteamVR auto-starts when Meta Link connects (and on Apply if already linked)."
            : "PreventDashLaunch: OFF — Meta may launch Oculus Dash on Link.";
    }

    public string DescribeCoreChannel()
    {
        var channel = ReadCoreChannel() ?? "(not set)";
        var label = DescribeCoreChannelLabel(channel);
        return $"CoreChannel: {channel} — {label}";
    }

    public static string DescribeCoreChannelLabel(string? channel) => channel switch
    {
        "LIVE" => "Stable / production Meta Quest Link builds",
        "PublicTest" => "Public Test Channel (PC beta / PTC)",
        "NO_UPDATES" => "Block Meta PC client/runtime updates (OculusKiller precaution — not a beta track)",
        _ => "Unknown / custom channel string"
    };

    public string? ReadCoreChannel()
    {
        foreach (var path in PreventDashLaunchKeyPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path, writable: false);
                if (key?.GetValue(CoreChannelValueName) is string channel
                    && !string.IsNullOrWhiteSpace(channel))
                {
                    return channel.Trim();
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

    /// <summary>Writes CoreChannel (LIVE / PublicTest / NO_UPDATES). Needs Administrator.</summary>
    public string SetCoreChannel(string channel, bool restartOvrService)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return "CoreChannel value is empty.";
        }

        channel = channel.Trim();
        try
        {
            var previous = ReadCoreChannel();
            if (string.Equals(channel, "NO_UPDATES", StringComparison.OrdinalIgnoreCase)
                && previous is not null
                && !string.Equals(previous, "NO_UPDATES", StringComparison.OrdinalIgnoreCase))
            {
                Settings.CoreChannelBeforeNoUpdates = previous;
                _app.Settings.Save();
            }

            var wrote = false;
            Exception? lastError = null;
            foreach (var path in PreventDashLaunchKeyPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(path, writable: true);
                    if (key is null)
                    {
                        continue;
                    }

                    key.SetValue(CoreChannelValueName, channel, RegistryValueKind.String);
                    wrote = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (!wrote)
            {
                return lastError is null
                    ? "Could not open Oculus Config registry key (run the tray elevated)."
                    : $"Could not write CoreChannel: {lastError.Message}";
            }

            var message = $"CoreChannel set to {channel} ({DescribeCoreChannelLabel(channel)}).";
            if (restartOvrService)
            {
                message += " " + _app.Oculus.Restart();
            }
            else
            {
                message += " Restart OVRService (or re-open Meta Link) for full effect.";
            }

            _app.Log.Info(message);
            return message;
        }
        catch (UnauthorizedAccessException)
        {
            return "CoreChannel needs Administrator rights (enable Run as Administrator on Service & Startup).";
        }
        catch (Exception ex)
        {
            return $"Could not write CoreChannel: {ex.Message}";
        }
    }

    public string RestoreCoreChannelBeforeNoUpdates(bool restartOvrService)
    {
        var previous = Settings.CoreChannelBeforeNoUpdates;
        if (string.IsNullOrWhiteSpace(previous))
        {
            previous = "LIVE";
        }

        return SetCoreChannel(previous, restartOvrService);
    }

    public bool IsPreventDashLaunchEnabled()
    {
        foreach (var path in PreventDashLaunchKeyPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path, writable: false);
                if (key?.GetValue(PreventDashLaunchValueName) is int dword && dword != 0)
                {
                    return true;
                }

                if (key?.GetValue(PreventDashLaunchValueName) is long qword && qword != 0)
                {
                    return true;
                }
            }
            catch
            {
                // try next path
            }
        }

        return false;
    }

    /// <summary>
    /// Writes <c>PreventDashLaunch</c> under Oculus Config (needs Administrator).
    /// Does not start SteamVR — call <see cref="RunNow"/> or restart OVRService separately.
    /// </summary>
    public string SetPreventDashLaunch(bool enabled, bool restartOvrService)
    {
        try
        {
            var wrote = false;
            Exception? lastError = null;
            foreach (var path in PreventDashLaunchKeyPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(path, writable: true);
                    if (key is null)
                    {
                        continue;
                    }

                    if (enabled)
                    {
                        key.SetValue(PreventDashLaunchValueName, 1, RegistryValueKind.DWord);
                    }
                    else if (key.GetValue(PreventDashLaunchValueName) is not null)
                    {
                        key.DeleteValue(PreventDashLaunchValueName, throwOnMissingValue: false);
                    }

                    wrote = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (!wrote)
            {
                return lastError is null
                    ? "Could not open Oculus Config registry key (run the tray elevated)."
                    : $"Could not write PreventDashLaunch: {lastError.Message}";
            }

            Settings.PreferPreventDashLaunch = enabled;
            _app.Settings.Save();

            var message = enabled
                ? "PreventDashLaunch set to 1 — Meta should not launch Dash."
                : "PreventDashLaunch removed — Meta may launch Dash again.";

            if (restartOvrService)
            {
                var restart = _app.Oculus.Restart();
                message += " " + restart;
            }
            else
            {
                message += " Restart OVRService for full effect.";
            }

            if (enabled)
            {
                // Registry alone never starts SteamVR — we do when Link is already up, else on next connect.
                var steam = TryStartSteamVrAfterPreventDash("PreventDashLaunch enabled");
                if (!string.IsNullOrWhiteSpace(steam))
                {
                    message += " " + steam;
                }

                if (Settings.AlsoSetNoUpdatesWithPreventDash)
                {
                    var channel = SetCoreChannel("NO_UPDATES", restartOvrService: false);
                    message += " " + channel;
                }
            }

            _app.Log.Info(message);
            return message;
        }
        catch (UnauthorizedAccessException)
        {
            return "PreventDashLaunch needs Administrator rights (enable Run as Administrator on Service & Startup).";
        }
        catch (Exception ex)
        {
            return $"Could not write PreventDashLaunch: {ex.Message}";
        }
    }

    private void PollAuto()
    {
        try
        {
            // PreventDashLaunch implies auto SteamVR on Meta Link (registry alone never starts it).
            if (!ShouldAutoStartSteamVrOnMetaLink())
            {
                _wasMetaSession = false;
                _ranThisMetaSession = false;
                return;
            }

            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            var meta = status.SessionActive
                       && status.Kind is VrConnectionKind.MetaAirLink
                           or VrConnectionKind.MetaWiredLink
                           or VrConnectionKind.MetaLinkUnknownTransport;

            if (!meta)
            {
                if (_wasMetaSession)
                {
                    _app.Log.Info("Meta Link session ended — Dash → SteamVR auto will arm again next connect.");
                }

                _wasMetaSession = false;
                _ranThisMetaSession = false;
                return;
            }

            if (!_wasMetaSession)
            {
                _wasMetaSession = true;
                _ranThisMetaSession = false;
            }

            if (_ranThisMetaSession)
            {
                return;
            }

            // Give Link a moment to settle after connect before killing Dash / starting SteamVR.
            _ranThisMetaSession = true;
            var reason = IsPreventDashLaunchEnabled() || Settings.PreferPreventDashLaunch
                ? "auto SteamVR (PreventDashLaunch)"
                : "auto on Meta Link connect";
            _app.Log.Info($"Meta Link connected — {reason} in 3s…");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    // Re-check still on Meta Link (user may have switched to Steam Link).
                    var again = _app.LinkConnection.Probe(includeEnumHmd: false);
                    if (!(again.SessionActive
                          && again.Kind is VrConnectionKind.MetaAirLink
                              or VrConnectionKind.MetaWiredLink
                              or VrConnectionKind.MetaLinkUnknownTransport))
                    {
                        _app.Log.Info("Auto Dash → SteamVR cancelled — Meta Link session no longer active.");
                        return;
                    }

                    RunNow(reason);
                }
                catch (Exception ex)
                {
                    _app.Log.Warn($"Auto Dash → SteamVR failed: {ex.Message}");
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Dash → SteamVR auto poll failed: {ex.Message}");
        }
    }

    private bool ShouldAutoStartSteamVrOnMetaLink() =>
        Settings.AutoOnMetaLinkConnect
        || Settings.PreferPreventDashLaunch
        || IsPreventDashLaunchEnabled();

    /// <summary>If Meta Link is already active, run Dash→SteamVR now; otherwise wait for connect poll.</summary>
    private string TryStartSteamVrAfterPreventDash(string reason)
    {
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            var meta = status.SessionActive
                       && status.Kind is VrConnectionKind.MetaAirLink
                           or VrConnectionKind.MetaWiredLink
                           or VrConnectionKind.MetaLinkUnknownTransport;
            if (meta)
            {
                return RunNow(reason);
            }

            return "SteamVR will auto-start on the next Meta Link / Air Link connect.";
        }
        catch (Exception ex)
        {
            return $"Could not auto-start SteamVR yet: {ex.Message}";
        }
    }

    private void ReapDashIfNeeded()
    {
        try
        {
            if (!Settings.KeepKillingDashWhileSteamVr
                || !IsProcessRunning("vrserver"))
            {
                _dashReaper.Stop();
                return;
            }

            var killed = KillDash(hard: true, quiet: true);
            if (!string.IsNullOrWhiteSpace(killed)
                && !killed.Contains("No OculusDash", StringComparison.OrdinalIgnoreCase))
            {
                _app.Log.Info("Dash reaper: " + killed);
            }
        }
        catch
        {
            // ignore reaper errors
        }
    }

    private string KillDash(bool hard, bool quiet = false)
    {
        var killed = 0;
        foreach (var name in DashProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    using (process)
                    {
                        if (hard)
                        {
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(3000);
                        }
                        else
                        {
                            process.CloseMainWindow();
                        }

                        killed++;
                    }
                }
                catch (Exception ex)
                {
                    if (!quiet)
                    {
                        _app.Log.Warn($"Could not stop {name}: {ex.Message}");
                    }
                }
            }
        }

        return killed == 0
            ? "No OculusDash process running."
            : $"Killed OculusDash ({killed}).";
    }

    private string CloseMetaClient()
    {
        var clientPath = _app.Oculus.ResolveClientExePath();
        var closed = 0;
        foreach (var name in new[] { "Client", "OculusClient" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    using (process)
                    {
                        if (name.Equals("Client", StringComparison.OrdinalIgnoreCase))
                        {
                            string? path = null;
                            try
                            {
                                path = process.MainModule?.FileName;
                            }
                            catch
                            {
                                continue;
                            }

                            if (path is null
                                || (clientPath is not null
                                    && !string.Equals(path, clientPath, StringComparison.OrdinalIgnoreCase)
                                    && !path.Contains("oculus-client", StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }

                        process.CloseMainWindow();
                        closed++;
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return closed == 0
            ? "Meta client not open."
            : $"Closed Meta client ({closed}).";
    }

    private string LaunchSteamVr()
    {
        if (IsProcessRunning("vrserver"))
        {
            return "SteamVR already running (vrserver).";
        }

        var paths = TryResolveSteamVrPaths();
        if (paths is null)
        {
            return "Could not find SteamVR (openvrpaths.vrpath / vrstartup.exe). Launch SteamVR once from Steam, then retry.";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = paths.Value.StartupPath,
                WorkingDirectory = Path.GetDirectoryName(paths.Value.StartupPath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });
            return $"Started SteamVR ({Path.GetFileName(paths.Value.StartupPath)}).";
        }
        catch (Exception ex)
        {
            return $"Could not start SteamVR: {ex.Message}";
        }
    }

    private static (string StartupPath, string ServerPath)? TryResolveSteamVrPaths()
    {
        var openVrPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "openvr",
            "openvrpaths.vrpath");
        if (!File.Exists(openVrPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(openVrPath));
            if (!doc.RootElement.TryGetProperty("runtime", out var runtime)
                || runtime.ValueKind != JsonValueKind.Array
                || runtime.GetArrayLength() == 0)
            {
                return null;
            }

            var root = runtime[0].GetString();
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var startup = Path.Combine(root, "bin", "win64", "vrstartup.exe");
            var server = Path.Combine(root, "bin", "win64", "vrserver.exe");
            if (!File.Exists(startup) || !File.Exists(server))
            {
                return null;
            }

            return (startup, server);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsProcessRunning(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
