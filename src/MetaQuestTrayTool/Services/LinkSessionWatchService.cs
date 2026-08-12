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

    public LinkSessionWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        _timer.Start();
        Poll();
    }

    public void Dispose() => _timer.Stop();

    private void Poll()
    {
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            var fingerprint = BuildFingerprint(status);
            if (string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            var previous = _lastFingerprint;
            _lastFingerprint = fingerprint;

            if (status.SessionActive)
            {
                LogConnected(status);
            }
            else if (!string.IsNullOrEmpty(previous) && !previous.StartsWith("idle:", StringComparison.Ordinal))
            {
                _app.Log.Info($"Link session ended — {status.InfoBanner}");
            }
            else if (status.Kind is VrConnectionKind.MetaAirLink or VrConnectionKind.MetaWiredLink
                     && status.Summary.Contains("inoperable", StringComparison.OrdinalIgnoreCase))
            {
                _app.Log.Warn($"Link session problem — {status.InfoBanner}. {status.Detail}");
            }
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Link session watcher failed: {ex.Message}");
        }
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
    }

    private static string BuildFingerprint(VrConnectionStatus status)
    {
        if (!status.SessionActive)
        {
            if (status.Summary.Contains("inoperable", StringComparison.OrdinalIgnoreCase))
            {
                return $"broken:{status.Kind}:{status.HeadsetSerial}:{status.DeviceCacheConnectionState}";
            }

            return $"idle:{status.Kind}:{status.IsUsingAirLink}";
        }

        return $"active:{status.Kind}:{status.HeadsetSerial}:{status.DeviceCacheConnectionState}:{status.IsUsingAirLink}";
    }
}
