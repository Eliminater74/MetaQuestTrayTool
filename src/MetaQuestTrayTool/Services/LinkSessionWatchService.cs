using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Logs PCVR session connect / disconnect and what the tray will apply or skip.
/// </summary>
public sealed class LinkSessionWatchService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private string? _lastFingerprint;
    private VrConnectionKind? _lastActiveKind;
    private int _pollGate;
    private int _endConfirmPolls;
    private DateTime _resumeQuietUntilUtc = DateTime.MinValue;

    /// <summary>Ignore connect/disconnect edges while Windows audio / DeviceCache settle after sleep.</summary>
    private static readonly TimeSpan ResumeQuiet = TimeSpan.FromSeconds(20);

    private const int EndConfirmPollsRequired = 2;

    public LinkSessionWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = IdleCadence.Quiet };
        _timer.Tick += (_, _) => BeginPoll();
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        _timer.Start();
        BeginPoll();
    }

    public void Dispose() => _timer.Stop();

    /// <summary>
    /// PC woke from sleep — DeviceCache / OVRService / audio often blip. Re-baseline quietly
    /// so we do not toast “Link session ended” for a ghost drop.
    /// </summary>
    public void NotifySystemResumed()
    {
        _resumeQuietUntilUtc = DateTime.UtcNow + ResumeQuiet;
        _endConfirmPolls = 0;
        _app.LinkConnection.InvalidateCache();
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            _lastFingerprint = BuildFingerprint(status);
            _lastActiveKind = IsLivePcvrSession(status) ? status.Kind : null;
            ApplyCadence(status.SessionActive);
            _app.Log.Info(
                "System resumed — ignoring Link connect/disconnect edges for "
                + $"{(int)ResumeQuiet.TotalSeconds}s (probe: {status.InfoBanner}).");
        }
        catch (Exception ex)
        {
            _lastFingerprint = null;
            _lastActiveKind = null;
            _app.Log.Warn($"Resume Link baseline failed: {ex.Message}");
        }
    }

    private void ApplyCadence(bool sessionActive)
    {
        IdleCadence.Set(_timer, sessionActive ? IdleCadence.Watching : IdleCadence.Quiet);
    }

    private void BeginPoll()
    {
        if (Interlocked.Exchange(ref _pollGate, 1) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Warn($"Link session watcher failed: {ex.Message}"));
            }
            finally
            {
                Interlocked.Exchange(ref _pollGate, 0);
            }
        });
    }

    private void Poll()
    {
        var status = _app.LinkConnection.Probe(includeEnumHmd: false);
        var live = IsLivePcvrSession(status);
        _app.Dispatcher.BeginInvoke(() => ApplyCadence(live || status.SessionActive));
        var fingerprint = BuildFingerprint(status);

        if (DateTime.UtcNow < _resumeQuietUntilUtc)
        {
            _lastFingerprint = fingerprint;
            _lastActiveKind = live ? status.Kind : null;
            _endConfirmPolls = 0;
            return;
        }

        if (string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
        {
            if (!live)
            {
                _endConfirmPolls = 0;
            }

            return;
        }

        var previous = _lastFingerprint;
        var previousActive = _lastActiveKind;

        if (live)
        {
            _endConfirmPolls = 0;
            _lastFingerprint = fingerprint;
            _lastActiveKind = status.Kind;
            _app.Dispatcher.BeginInvoke(() =>
            {
                _app.SessionRecover.NotifySessionStarted();
                LogConnected(status);
                _app.HeadsetAnnouncer.AnnounceSessionConnected(status);
            });
            return;
        }

        if (!string.IsNullOrEmpty(previous) && previous.StartsWith("active:", StringComparison.Ordinal))
        {
            _endConfirmPolls++;
            if (_endConfirmPolls < EndConfirmPollsRequired)
            {
                _app.Log.Info(
                    $"Link drop suspected ({_endConfirmPolls}/{EndConfirmPollsRequired}) — "
                    + $"{status.InfoBanner}; waiting before treating as session end.");
                return;
            }

            _endConfirmPolls = 0;
            _lastFingerprint = fingerprint;
            _lastActiveKind = null;
            _app.Dispatcher.BeginInvoke(() => LogSessionEnded(previousActive, status));
            return;
        }

        _endConfirmPolls = 0;
        _lastFingerprint = fingerprint;
        _lastActiveKind = null;

        // Meta Wi‑Fi auto-connect without Link UI — informational, not an error.
        if (status.Kind is VrConnectionKind.MetaAirLink or VrConnectionKind.MetaWiredLink
            && status.Summary.Contains("auto-connect", StringComparison.OrdinalIgnoreCase))
        {
            _app.Dispatcher.BeginInvoke(() => _app.Log.Info(
                $"Meta DeviceCache — {status.InfoBanner}. "
                + "Headset is on the network without a Link stream (normal if you use Steam Link / VD)."));
        }
    }

    private void LogSessionEnded(VrConnectionKind? previousActive, VrConnectionStatus status)
    {
        var ended = previousActive switch
        {
            VrConnectionKind.SteamLinkOrSteamVr => "Steam Link / SteamVR session ended",
            VrConnectionKind.VirtualDesktop => "Virtual Desktop session ended",
            VrConnectionKind.MetaAirLink => "Meta Air Link session ended",
            VrConnectionKind.MetaWiredLink => "Meta wired Link session ended",
            _ => "Link session ended"
        };

        if (status.Summary.Contains("auto-connect", StringComparison.OrdinalIgnoreCase))
        {
            _app.Log.Info($"{ended} — Meta DeviceCache still shows auto-connect (headset on Wi‑Fi).");
            _app.SessionRecover.NotifySessionEnded(previousActive, ended);
            _app.AudioWatch?.NotifyPcvrSessionEnded($"{ended} — restoring desktop / fallback audio.");
            _app.HeadsetAnnouncer.AnnounceSessionDisconnected(previousActive);
            return;
        }

        _app.Log.Info($"{ended} — {status.InfoBanner}");
        _app.SessionRecover.NotifySessionEnded(previousActive, ended);
        _app.AudioWatch?.NotifyPcvrSessionEnded($"{ended} — restoring desktop / fallback audio.");
        _app.HeadsetAnnouncer.AnnounceSessionDisconnected(previousActive);
    }

    private void LogConnected(VrConnectionStatus status)
    {
        var caps = VrSessionCapabilities.From(status);
        var serial = string.IsNullOrWhiteSpace(status.HeadsetSerial) ? "unknown serial" : status.HeadsetSerial;
        _app.Log.Info($"Link connected — {status.InfoBanner}");
        if (!string.IsNullOrWhiteSpace(status.Detail))
        {
            _app.Log.Info($"Link detail — {status.Detail}");
        }

        _app.Log.Info(
            $"Link session — serial {serial}; Meta Link registry {(caps.AllowsMetaLinkRegistry ? "allowed" : "skipped")}; "
            + $"ODT SS/ASW {(caps.AllowsOculusDebugTool ? "allowed" : "skipped")}; "
            + $"ADB tweaks {(caps.AllowsHeadsetAdb ? "allowed" : "skipped")}; "
            + $"OpenXR switch {(caps.AllowsOpenXrSwitch ? "allowed" : "skipped")}.");

        var overlays = _app.OverlayClose.CloseConfiguredOverlays(status.InfoBanner);
        if (!string.IsNullOrWhiteSpace(overlays))
        {
            _app.Log.Info(overlays);
        }
    }

    private static string BuildFingerprint(VrConnectionStatus status)
    {
        if (!IsLivePcvrSession(status))
        {
            if (status.Summary.Contains("auto-connect", StringComparison.OrdinalIgnoreCase)
                || status.Summary.Contains("inoperable", StringComparison.OrdinalIgnoreCase))
            {
                return $"broken:{status.Kind}:{status.HeadsetSerial}:{status.DeviceCacheConnectionState}";
            }

            return $"idle:{status.Kind}:{status.IsUsingAirLink}";
        }

        return $"active:{status.Kind}:{status.HeadsetSerial}:{status.DeviceCacheConnectionState}:{status.IsUsingAirLink}";
    }

    /// <summary>
    /// Real PCVR stream — not DeviceCache Wi‑Fi auto-connect / EnumHmd ghosts that flip on sleep/wake.
    /// </summary>
    private static bool IsLivePcvrSession(VrConnectionStatus status)
    {
        if (!status.SessionActive)
        {
            return false;
        }

        return status.Kind switch
        {
            VrConnectionKind.SteamLinkOrSteamVr => true,
            VrConnectionKind.VirtualDesktop => true,
            _ => status.MetaLinkStreaming
        };
    }
}
