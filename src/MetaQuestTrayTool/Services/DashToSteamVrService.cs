using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// PreventDashLaunch registry + launch SteamVR so Air Link / wired Link can drive SteamVR games
/// without Meta Dash owning the session. Does <b>not</b> kill Meta processes.
/// See <see href="https://github.com/DevOculus-Meta-Quest/OculusKiller">OculusKiller</see>
/// and <c>HKLM\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus\Config\PreventDashLaunch</c> = 1.
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

    private readonly App _app;
    private readonly DispatcherTimer _sessionTimer;
    private readonly DispatcherTimer _steamVrExitWatch;
    private DispatcherTimer? _pendingAutoLaunchTimer;
    private bool _ranThisMetaSession;
    private bool _wasMetaSession;
    private bool _sawSteamVrRunning;
    private bool _sawHealthySteamVr;
    private int _steamVrGonePolls;
    private int _waitingForFirstSteamVrPolls;
    private volatile bool _restartingOvrAfterSteamVrExit;
    private bool _awaitingLinkIdleAfterSteamVrExit;
    private int _linkIdleConfirmPolls;
    private CancellationTokenSource? _ovrDropCts;

    private int _streamingConfirmPolls;

    /// <summary>Require consecutive streaming probes before auto-launch (avoids one-off ghosts).</summary>
    private const int StreamingConfirmPollsRequired = 2;

    /// <summary>Confirm SteamVR is really gone before restarting OVRService (avoids restart blips).</summary>
    private const int SteamVrExitConfirmPolls = 2;

    /// <summary>Need several idle polls after OVR drop before the next connect can auto-start SteamVR.</summary>
    private const int LinkIdleConfirmPollsRequired = 3;

    /// <summary>Stop OVRService this long so Air Link fully drops to Quest Home (restart is too fast).</summary>
    private static readonly TimeSpan OvrHoldForLinkDrop = TimeSpan.FromSeconds(10);

    /// <summary>Give vrstartup a few minutes to spawn vrserver before disarming.</summary>
    private const int MaxWaitForSteamVrPolls = 36;

    public DashToSteamVrService(App app)
    {
        _app = app;
        _sessionTimer = new DispatcherTimer { Interval = IdleCadence.Quiet };
        _sessionTimer.Tick += (_, _) => PollAuto();
        _steamVrExitWatch = new DispatcherTimer { Interval = IdleCadence.Active };
        _steamVrExitWatch.Tick += (_, _) => PollSteamVrExit();
    }

    public DashToSteamVrSettings Settings => _app.Settings.Current.DashToSteamVr;

    public void Start()
    {
        if (ShouldAutoStartSteamVrOnMetaLink() && !Settings.RestartOvrServiceWhenSteamVrExits)
        {
            Settings.RestartOvrServiceWhenSteamVrExits = true;
            _app.Settings.Save();
            _app.Log.Info(
                "PreventDashLaunch is on — enabled SteamVR-exit OVRService drop so Link can disconnect for Quest Home.");
        }

        ArmSessionBaselineFromCurrentProbe();
        SyncSessionWatch();
        SyncSteamVrExitWatch();
    }

    /// <summary>
    /// If Link is already streaming <b>and</b> SteamVR is healthy at tray start, do not treat that
    /// as a fresh connect (avoids launching SteamVR again on every app/update restart).
    /// If Link is up without SteamVR, PollAuto will still start it (PreventDash black-void case).
    /// </summary>
    private void ArmSessionBaselineFromCurrentProbe()
    {
        if (!ShouldAutoStartSteamVrOnMetaLink())
        {
            return;
        }

        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            if (!IsPreventDashConnect(status) || !IsSteamVrSessionHealthy())
            {
                return;
            }

            _wasMetaSession = true;
            _ranThisMetaSession = true;
            if (Settings.RestartOvrServiceWhenSteamVrExits)
            {
                ArmSteamVrExitWatch(sawRunning: true);
            }

            _app.Log.Info(
                "Meta Link + SteamVR already running at tray start — will drop OVRService when SteamVR exits.");
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Dash → SteamVR baseline probe failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Run the Meta Link poll only while auto Dash→SteamVR / PreventDashLaunch is armed.
    /// Otherwise stay completely quiet in the tray.
    /// </summary>
    public void SyncSessionWatch()
    {
        if (ShouldAutoStartSteamVrOnMetaLink())
        {
            if (!_sessionTimer.IsEnabled)
            {
                _sessionTimer.Start();
                PollAuto();
            }

            // PreventDash must notice Air Link within seconds, not the idle 30s Quiet cadence.
            IdleCadence.Set(_sessionTimer, IdleCadence.Active);
        }
        else
        {
            _sessionTimer.Stop();
            _wasMetaSession = false;
            _ranThisMetaSession = false;
            ClearSteamVrExitIdleLatch();
        }
    }

    /// <summary>
    /// SteamVR-exit → OVRService drop. Armed from Dash→SteamVR (RunNow) and whenever PreventDash
    /// is on and SteamVR is running (manual start / leftover processes).
    /// </summary>
    public void SyncSteamVrExitWatch()
    {
        if (!Settings.RestartOvrServiceWhenSteamVrExits)
        {
            StopSteamVrExitWatch(resetSaw: true);
            return;
        }

        // PreventDash + leftover/manual SteamVR must still drop Link on exit (black void otherwise).
        if (ShouldAutoStartSteamVrOnMetaLink()
            && (IsSteamVrSessionHealthy() || IsProcessRunning("vrserver") || IsProcessRunning("vrstartup")))
        {
            ArmSteamVrExitWatch(sawRunning: IsProcessRunning("vrserver") || IsSteamVrSessionHealthy());
            return;
        }

        if (_sawSteamVrRunning || _steamVrExitWatch.IsEnabled || _waitingForFirstSteamVrPolls > 0)
        {
            if (!_steamVrExitWatch.IsEnabled)
            {
                _steamVrExitWatch.Start();
            }

            IdleCadence.Set(_steamVrExitWatch, IdleCadence.Active);
        }
    }

    public void Dispose()
    {
        CancelPendingAutoLaunch();
        CancelOvrDrop();
        _sessionTimer.Stop();
        _steamVrExitWatch.Stop();
    }

    private void CancelOvrDrop()
    {
        try
        {
            _ovrDropCts?.Cancel();
        }
        catch
        {
            // dispose path
        }
    }

    /// <summary>Manual / hotkey / voice entry point.</summary>
    public string RunNow(string reason = "manual", bool restartSteamVrIfRunning = false)
    {
        if (!IsPreventDashLaunchEnabled())
        {
            const string msg =
                "PreventDashLaunch is off — enable it on Service & Startup and Apply. "
                + "This tool does not kill Meta processes; Dash is blocked via registry only.";
            _app.Log.Warn($"Dash → SteamVR ({reason}): {msg}");
            return msg;
        }

        ClearSteamVrExitIdleLatch();
        var parts = new List<string> { "PreventDashLaunch is on — Dash blocked via registry." };

        if (!reason.StartsWith("auto", StringComparison.OrdinalIgnoreCase))
        {
            _app.HeadsetAnnouncer.AnnounceSteamVrStarting();
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

            PcvrSetup.SyncSavedOpenXrToSetup(_app);
        }

        parts.Add(LaunchSteamVr(restartIfRunning: restartSteamVrIfRunning));
        var steamVrLaunchedOrRunning = IsSteamVrSessionHealthy()
            || parts[^1].Contains("Started SteamVR", StringComparison.OrdinalIgnoreCase)
            || parts[^1].Contains("already running", StringComparison.OrdinalIgnoreCase);

        if (Settings.RestartOvrServiceWhenSteamVrExits && steamVrLaunchedOrRunning)
        {
            ArmSteamVrExitWatch(sawRunning: IsProcessRunning("vrserver"));
            parts.Add("Will restart OVRService when SteamVR exits.");
        }

        var summary = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        _app.Log.Info($"Dash → SteamVR ({reason}): {summary}");
        if (_app.Settings.Current.ShowNotifications)
        {
            _app.TrayNotify("Dash → SteamVR", summary);
        }

        return summary;
    }

    /// <summary>
    /// Always launch SteamVR (no PreventDashLaunch required). Use when auto-start missed or you want
    /// SteamVR by hand from the tray, Status, hotkey, or voice.
    /// </summary>
    public string StartSteamVrNow(string reason = "manual")
    {
        ClearSteamVrExitIdleLatch();
        _app.HeadsetAnnouncer.AnnounceSteamVrStarting();

        var parts = new List<string>();
        if (Settings.SwitchOpenXrToSteamVr)
        {
            var openXr = _app.OpenXr.ReadActiveKind();
            if (openXr != OpenXrRuntimeKind.SteamVr)
            {
                parts.Add(_app.OpenXr.Set(OpenXrRuntimeKind.SteamVr));
            }

            PcvrSetup.SyncSavedOpenXrToSetup(_app);
        }

        parts.Add(LaunchSteamVr());
        var steamVrLaunchedOrRunning = IsSteamVrSessionHealthy()
            || parts[^1].Contains("Started SteamVR", StringComparison.OrdinalIgnoreCase)
            || parts[^1].Contains("already running", StringComparison.OrdinalIgnoreCase);

        if (IsPreventDashLaunchEnabled()
            && Settings.RestartOvrServiceWhenSteamVrExits
            && steamVrLaunchedOrRunning)
        {
            ArmSteamVrExitWatch(sawRunning: IsProcessRunning("vrserver"));
            parts.Add("Will restart OVRService when SteamVR exits.");
        }

        var summary = string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        _app.Log.Info($"Start SteamVR ({reason}): {summary}");
        if (_app.Settings.Current.ShowNotifications)
        {
            _app.TrayNotify("SteamVR", summary);
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
            if (enabled)
            {
                // Without this, exiting SteamVR leaves PreventDash users stuck in Link with no Dash.
                Settings.RestartOvrServiceWhenSteamVrExits = true;
            }

            _app.Settings.Save();
            SyncSessionWatch();
            SyncSteamVrExitWatch();
            if (enabled)
            {
                var aligned = PcvrSetup.SyncSavedOpenXrToSetup(_app);
                if (!string.IsNullOrWhiteSpace(aligned))
                {
                    _app.Log.Info(aligned);
                }
            }

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
                ClearSteamVrExitIdleLatch();
                SyncSessionWatch();
                SyncSteamVrExitWatch();
                return;
            }

            SyncSteamVrExitWatch();

            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            var meta = IsPreventDashConnect(status);

            // Manual SteamVR start (or zombie relaunch) during PreventDash Link must still arm
            // the exit watch — otherwise SteamVR exit leaves a black void with no Dash / Quest Home.
            if (meta
                && Settings.RestartOvrServiceWhenSteamVrExits
                && (IsSteamVrSessionHealthy() || IsProcessRunning("vrserver")))
            {
                ArmSteamVrExitWatch(sawRunning: true);
            }

            if (_awaitingLinkIdleAfterSteamVrExit)
            {
                IdleCadence.Set(_sessionTimer, IdleCadence.Active);
                // Service still down — ignore DeviceCache blips; do not arm the next SteamVR launch.
                if (_restartingOvrAfterSteamVrExit)
                {
                    _ranThisMetaSession = true;
                    _wasMetaSession = true;
                    _linkIdleConfirmPolls = 0;
                    return;
                }

                if (IsSteamVrRuntimePresent())
                {
                    StopSteamVrRuntime(
                        "Steam respawned SteamVR after you exited — holding off until Quest Home");
                    _ranThisMetaSession = true;
                    _wasMetaSession = true;
                    _linkIdleConfirmPolls = 0;
                    return;
                }

                if (meta)
                {
                    _ranThisMetaSession = true;
                    _wasMetaSession = true;
                    _linkIdleConfirmPolls = 0;
                    return;
                }

                _linkIdleConfirmPolls++;
                if (_linkIdleConfirmPolls < LinkIdleConfirmPollsRequired)
                {
                    _app.Log.Info(
                        $"Meta Link idle suspected after SteamVR exit ({_linkIdleConfirmPolls}/{LinkIdleConfirmPollsRequired}).");
                    return;
                }

                ClearSteamVrExitIdleLatch();
                _ranThisMetaSession = false;
                _wasMetaSession = false;
                _app.Log.Info(
                    "Quest Home confirmed after SteamVR exit — next Air Link / Quest Link connect will auto-start SteamVR.");
            }

            if (!meta)
            {
                if (_wasMetaSession)
                {
                    _app.Log.Info("Meta Link session ended — Dash → SteamVR auto will arm again next connect.");
                }

                _wasMetaSession = false;
                _ranThisMetaSession = false;
                _streamingConfirmPolls = 0;
                IdleCadence.Set(_sessionTimer, IdleCadence.Active);
                return;
            }

            if (!_wasMetaSession)
            {
                _wasMetaSession = true;
                _ranThisMetaSession = false;
                _streamingConfirmPolls = 0;
                IdleCadence.Set(_sessionTimer, IdleCadence.Active);
            }

            if (_ranThisMetaSession)
            {
                return;
            }

            if (IsSteamVrSessionHealthy())
            {
                _app.Log.Info(
                    "SteamVR processes are still running at this Link connect — will restart them so they attach to this session.");
            }

            _streamingConfirmPolls++;
            var confirmNeeded = status.MetaLinkStreaming ? 1 : StreamingConfirmPollsRequired;
            if (_streamingConfirmPolls < confirmNeeded)
            {
                _app.Log.Info(
                    $"Meta Link streaming suspected ({_streamingConfirmPolls}/{confirmNeeded}) — "
                    + $"{status.Detail ?? status.Summary}; waiting before auto SteamVR.");
                IdleCadence.Set(_sessionTimer, IdleCadence.Active);
                return;
            }

            // Give Link a moment to settle after connect before starting SteamVR.
            _ranThisMetaSession = true;
            var reason = IsPreventDashLaunchEnabled() || Settings.PreferPreventDashLaunch
                ? "auto SteamVR (PreventDashLaunch)"
                : "auto on Meta Link connect";
            _app.Log.Info($"Meta Link connected — {reason} in 2s…");
            _app.HeadsetAnnouncer.AnnounceSteamVrComing(status);
            CancelPendingAutoLaunch();
            _pendingAutoLaunchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pendingAutoLaunchTimer.Tick += OnPendingAutoLaunchTick;
            _pendingAutoLaunchTimer.Tag = reason;
            _pendingAutoLaunchTimer.Start();
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Dash → SteamVR auto poll failed: {ex.Message}");
        }
    }

    private void OnPendingAutoLaunchTick(object? sender, EventArgs e)
    {
        var timer = sender as DispatcherTimer ?? _pendingAutoLaunchTimer;
        var reason = timer?.Tag as string ?? "auto SteamVR (PreventDashLaunch)";
        CancelPendingAutoLaunch();
        try
        {
            _app.LinkConnection.InvalidateCache();
            var again = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            if (!IsPreventDashConnect(again))
            {
                _app.Log.Info("Auto Dash → SteamVR cancelled — Meta Link session no longer active.");
                _ranThisMetaSession = false;
                _streamingConfirmPolls = 0;
                return;
            }

            RunNow(reason, restartSteamVrIfRunning: true);
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Auto Dash → SteamVR failed: {ex.Message}");
        }
    }

    private void CancelPendingAutoLaunch()
    {
        if (_pendingAutoLaunchTimer is null)
        {
            return;
        }

        _pendingAutoLaunchTimer.Stop();
        _pendingAutoLaunchTimer.Tick -= OnPendingAutoLaunchTick;
        _pendingAutoLaunchTimer = null;
    }

    private bool ShouldAutoStartSteamVrOnMetaLink() =>
        Settings.PreferPreventDashLaunch || IsPreventDashLaunchEnabled();

    /// <summary>
    /// PreventDash connect: live Meta stream only. Companion is not used (it lingers while charging).
    /// </summary>
    private bool IsPreventDashConnect(VrConnectionStatus status) =>
        !status.VirtualDesktopRunning && status.MetaLinkStreaming;

    /// <summary>If Meta Link is already active, run Dash→SteamVR now; otherwise wait for connect poll.</summary>
    private string TryStartSteamVrAfterPreventDash(string reason)
    {
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            if (IsPreventDashConnect(status))
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

    private void ArmSteamVrExitWatch(bool sawRunning)
    {
        if (!Settings.RestartOvrServiceWhenSteamVrExits)
        {
            return;
        }

        if (sawRunning)
        {
            _sawSteamVrRunning = true;
            _steamVrGonePolls = 0;
            _waitingForFirstSteamVrPolls = 0;
            if (IsSteamVrSessionHealthy())
            {
                _sawHealthySteamVr = true;
            }
        }
        else
        {
            _waitingForFirstSteamVrPolls = 0;
        }

        if (!_steamVrExitWatch.IsEnabled)
        {
            _steamVrExitWatch.Start();
            _app.Log.Info(
                "SteamVR exit watch armed — will stop OVRService for 10s when SteamVR ends so Quest Home can return.");
        }

        IdleCadence.Set(_steamVrExitWatch, IdleCadence.Active);
    }

    private void ClearSteamVrExitIdleLatch()
    {
        _awaitingLinkIdleAfterSteamVrExit = false;
        _linkIdleConfirmPolls = 0;
    }

    private void StopSteamVrExitWatch(bool resetSaw)
    {
        _steamVrExitWatch.Stop();
        _steamVrGonePolls = 0;
        _waitingForFirstSteamVrPolls = 0;
        if (resetSaw)
        {
            _sawSteamVrRunning = false;
            _sawHealthySteamVr = false;
        }
    }

    private void PollSteamVrExit()
    {
        try
        {
            if (!Settings.RestartOvrServiceWhenSteamVrExits)
            {
                StopSteamVrExitWatch(resetSaw: true);
                return;
            }

            if (_restartingOvrAfterSteamVrExit)
            {
                return;
            }

            // Healthy compositor/dashboard = still in SteamVR.
            if (IsSteamVrSessionHealthy() || IsProcessRunning("vrstartup"))
            {
                _sawSteamVrRunning = true;
                _sawHealthySteamVr = IsSteamVrSessionHealthy() || _sawHealthySteamVr;
                _steamVrGonePolls = 0;
                _waitingForFirstSteamVrPolls = 0;
                IdleCadence.Set(_steamVrExitWatch, IdleCadence.Active);
                return;
            }

            // vrserver without compositor: either still starting, or zombie after an "invisible" exit.
            if (IsProcessRunning("vrserver"))
            {
                if (!_sawHealthySteamVr)
                {
                    _waitingForFirstSteamVrPolls++;
                    if (_waitingForFirstSteamVrPolls < MaxWaitForSteamVrPolls)
                    {
                        IdleCadence.Set(_steamVrExitWatch, IdleCadence.Active);
                        return;
                    }

                    _app.Log.Info(
                        "SteamVR exit watch: vrserver never became healthy — clearing zombie and dropping Link.");
                    StopSteamVrRuntime("SteamVR never became healthy");
                }
                else
                {
                    // Was healthy; compositor gone — user left SteamVR / stuck in dark void.
                    StopSteamVrRuntime("SteamVR compositor gone after healthy session");
                }

                _sawSteamVrRunning = true;
            }

            if (!_sawSteamVrRunning)
            {
                _waitingForFirstSteamVrPolls++;
                if (_waitingForFirstSteamVrPolls >= MaxWaitForSteamVrPolls)
                {
                    _app.Log.Info("SteamVR exit watch: vrserver never appeared — disarming.");
                    StopSteamVrExitWatch(resetSaw: true);
                    return;
                }

                IdleCadence.Set(_steamVrExitWatch, IdleCadence.Active);
                return;
            }

            _steamVrGonePolls++;
            if (_steamVrGonePolls < SteamVrExitConfirmPolls)
            {
                return;
            }

            CancelPendingAutoLaunch();

            _restartingOvrAfterSteamVrExit = true;
            StopSteamVrExitWatch(resetSaw: true);
            _awaitingLinkIdleAfterSteamVrExit = true;
            _linkIdleConfirmPolls = 0;
            _ranThisMetaSession = true;
            _wasMetaSession = true;
            _app.HeadsetAnnouncer.AnnounceSteamVrExitBeforeOvrStop();
            _app.AudioWatch?.NotifyPcvrSessionEnded("SteamVR exited — restoring desktop / fallback audio.");
            _ovrDropCts?.Dispose();
            _ovrDropCts = new CancellationTokenSource();
            var token = _ovrDropCts.Token;
            _app.LinkConnection.InvalidateCache();
            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    // Stop OVRService completely, wait for Air Link to fall off, then start again.
                    var result = _app.Oculus.RestartForLinkDrop(OvrHoldForLinkDrop, token);
                    _app.LinkConnection.InvalidateCache();
                    var summary =
                        "SteamVR exited — stopped OVRService for 10s so Link can drop to Quest Home, then started it again. "
                        + result;
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    _app.Dispatcher.BeginInvoke(() =>
                    {
                        _app.Log.Info(summary);
                        if (_app.Settings.Current.ShowNotifications)
                        {
                            _app.TrayNotify("SteamVR exit", summary);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // tray exiting
                }
                catch (Exception ex)
                {
                    _app.Dispatcher.BeginInvoke(() =>
                        _app.Log.Warn("SteamVR exit watch failed: " + ex.Message));
                }
                finally
                {
                    _restartingOvrAfterSteamVrExit = false;
                }
            }, token);
        }
        catch (Exception ex)
        {
            _restartingOvrAfterSteamVrExit = false;
            _app.Log.Warn("SteamVR exit watch failed: " + ex.Message);
            StopSteamVrExitWatch(resetSaw: true);
        }
    }

    private string LaunchSteamVr(bool restartIfRunning = false)
    {
        if (IsSteamVrSessionHealthy() && !restartIfRunning)
        {
            return "SteamVR already running (vrserver + compositor).";
        }

        var notes = new List<string>();
        if (IsSteamVrRuntimePresent() || (restartIfRunning && IsSteamVrSessionHealthy()))
        {
            notes.Add(StopSteamVrRuntime(
                restartIfRunning
                    ? "restart so SteamVR attaches to this Link session"
                    : "zombie / invisible SteamVR (vrserver without compositor)"));
        }

        if (SessionHelperClient.TryLaunchSteamVr(out var started))
        {
            notes.Add("Started SteamVR. " + started);
            return string.Join(" ", notes);
        }

        var paths = TryResolveSteamVrPaths();
        if (paths is not null
            && SessionHelperClient.TryLaunchExe(
                paths.Value.StartupPath,
                arguments: null,
                Path.GetDirectoryName(paths.Value.StartupPath),
                out var exeDetail))
        {
            notes.Add($"Started SteamVR ({Path.GetFileName(paths.Value.StartupPath)}). " + exeDetail);
            return string.Join(" ", notes);
        }

        notes.Add("Could not start SteamVR: " + started);
        return string.Join(" ", notes);
    }

    /// <summary>
    /// Healthy SteamVR needs the compositor (or dashboard). A lone vrserver / vrmonitor leftover is
    /// often invisible in the headset — relaunch instead of reporting "already running".
    /// </summary>
    private static bool IsSteamVrSessionHealthy() =>
        IsProcessRunning("vrserver")
        && (IsProcessRunning("vrcompositor") || IsProcessRunning("vrdashboard"));

    private static bool IsSteamVrRuntimePresent() =>
        SteamVrRuntimeProcessNames.Any(IsProcessRunning);

    private string StopSteamVrRuntime(string reason)
    {
        var killed = 0;
        foreach (var name in SteamVrRuntimeProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    killed++;
                }
                catch
                {
                    try
                    {
                        process.Kill();
                        killed++;
                    }
                    catch
                    {
                        // best-effort
                    }
                }

                try
                {
                    process.WaitForExit(1_500);
                }
                catch
                {
                    // already gone
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        var summary = killed > 0
            ? $"Stopped {killed} SteamVR process(es) ({reason})."
            : $"No SteamVR processes to stop ({reason}).";
        _app.Log.Info(summary);
        return summary;
    }

    private static readonly string[] SteamVrRuntimeProcessNames =
    [
        "vrserver",
        "vrcompositor",
        "vrdashboard",
        "vrmonitor",
        "vrstartup",
        "vrwebhelper",
        "vrcleanup"
    ];

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
