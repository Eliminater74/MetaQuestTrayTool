using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Kill Meta Dash and launch SteamVR so Air Link / wired Link can drive SteamVR games
/// (MSFS, etc.) without the Meta dashboard owning the session.
/// Runtime behaviour inspired by
/// <see href="https://github.com/DevOculus-Meta-Quest/OculusKiller">DevOculus-Meta-Quest/OculusKiller</see>
/// — launches <c>vrstartup.exe</c> from <c>openvrpaths.vrpath</c> and terminates Dash;
/// does <b>not</b> replace <c>OculusDash.exe</c> on disk.
/// </summary>
public sealed class DashToSteamVrService : IDisposable
{
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
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _sessionTimer.Tick += (_, _) => PollAuto();
        _dashReaper = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
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

        if (Settings.KeepKillingDashWhileSteamVr)
        {
            _dashReaper.Start();
            parts.Add("Dash reaper on while SteamVR runs.");
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

    private void PollAuto()
    {
        try
        {
            if (!Settings.AutoOnMetaLinkConnect)
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

            // Give Link a moment to settle after connect before killing Dash.
            _ranThisMetaSession = true;
            _app.Log.Info("Meta Link connected — auto Dash → SteamVR in 3s…");
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

                    RunNow("auto on Meta Link connect");
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
