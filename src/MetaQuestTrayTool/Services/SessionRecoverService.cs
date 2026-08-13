using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// After a Link / SteamVR / VD session drops, offer one-click recovery:
/// restart OVRService, re-apply Link + globals, fix audio, optional SteamVR OpenXR.
/// </summary>
public sealed class SessionRecoverService
{
    private readonly App _app;
    private readonly object _gate = new();

    public DateTime? LastDropUtc { get; private set; }
    public string? LastDropLabel { get; private set; }
    public VrConnectionKind? LastDropKind { get; private set; }

    public SessionRecoverService(App app) => _app = app;

    public void NotifySessionEnded(VrConnectionKind? kind, string label)
    {
        lock (_gate)
        {
            LastDropUtc = DateTime.UtcNow;
            LastDropKind = kind;
            LastDropLabel = label;
        }

        _app.Log.Info($"{label} — Recover PCVR is available on Info / tray if the stream does not come back.");
        _app.TrayNotify(
            "PCVR session ended",
            "If Link dropped unexpectedly, use Recover PCVR (Info page or tray) to restart OVRService and re-apply settings.");
    }

    public void NotifySessionStarted()
    {
        lock (_gate)
        {
            LastDropUtc = null;
            LastDropLabel = null;
            LastDropKind = null;
        }
    }

    public bool ShouldSuggestRecover(VrConnectionStatus? status = null)
    {
        status ??= _app.LinkConnection.Probe(includeEnumHmd: false);
        if (status.SessionActive)
        {
            return false;
        }

        lock (_gate)
        {
            return LastDropUtc is { } drop
                   && DateTime.UtcNow - drop < TimeSpan.FromMinutes(20);
        }
    }

    public string DescribeSuggestion()
    {
        lock (_gate)
        {
            if (LastDropUtc is null)
            {
                return "No recent session drop.";
            }

            var ago = DateTime.UtcNow - LastDropUtc.Value;
            var when = ago.TotalMinutes < 1
                ? "just now"
                : $"{(int)ago.TotalMinutes} min ago";
            return $"{LastDropLabel ?? "Session ended"} ({when}). Recover restarts OVRService and re-applies Link / audio / globals.";
        }
    }

    public string Recover(string reason = "manual")
    {
        var parts = new List<string> { $"Recover PCVR ({reason})" };

        _app.Oculus.Refresh();
        if (_app.Oculus.ServiceExists)
        {
            parts.Add(_app.Oculus.Restart());
        }
        else
        {
            parts.Add("OVRService not installed — skipped service restart.");
        }

        if (_app.Settings.Current.ApplyLinkSettingsOnStart
            || _app.Settings.Current.LinkSettings.BitrateMbps > 0
            || _app.Settings.Current.LinkSettings.EncodeResolutionWidth > 0)
        {
            parts.Add(_app.ApplyMetaLinkSettings(_app.Settings.Current.LinkSettings, deleteUnsetOverrides: true));
        }

        parts.Add(_app.ApplyGlobalGameSettings(includeOdt: true));

        var audio = _app.Settings.Current.Audio;
        if (audio.AutoSwitchEnabled)
        {
            try
            {
                if (_app.Audio.IsLinkAudioSessionActive(audio)
                    || _app.Audio.IsCurrentPlaybackHeadset())
                {
                    parts.Add(_app.Audio.ApplyVrDevices(audio));
                }
                else if (!string.IsNullOrWhiteSpace(audio.FallbackPlaybackDeviceId))
                {
                    parts.Add(_app.Audio.RestoreFallbackDevices(audio));
                }
            }
            catch (Exception ex)
            {
                parts.Add("Audio: " + ex.Message);
            }
        }

        var openXr = _app.Settings.Current.OpenXr;
        if (openXr.PreferSteamVrDuringSteamLink
            && LastDropKind == VrConnectionKind.SteamLinkOrSteamVr
            && _app.OpenXr.IsAvailable(OpenXrRuntimeKind.SteamVr))
        {
            // After Steam Link drop, restore preferred runtime.
            var preferred = openXr.PreferredRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr
                ? openXr.PreferredRuntime
                : OpenXrRuntimeKind.SteamVr;
            parts.Add(_app.OpenXr.Set(preferred));
        }

        lock (_gate)
        {
            LastDropUtc = null;
            LastDropLabel = null;
            LastDropKind = null;
        }

        var summary = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        _app.Log.Info(summary);
        _app.TrayNotify("PCVR recover", "OVRService / Link / audio recovery finished. Reconnect Link if needed.");
        return summary;
    }
}
