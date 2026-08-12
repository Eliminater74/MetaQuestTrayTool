using NAudio.CoreAudioApi;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Native;

namespace MetaQuestTrayTool.Services;

public sealed class AudioDeviceService
{
    private static readonly string[] HeadsetNameHints =
    [
        "oculus",
        "quest",
        "rift",
        "meta virtual audio",
        "headphones (oculus",
        "steamvr",
        "vive",
        "index",
        "galaxy xr"
    ];

    public IReadOnlyList<AudioDeviceInfo> ListDevices(AudioDeviceKind kind)
    {
        using var enumerator = new MMDeviceEnumerator();
        var dataFlow = kind == AudioDeviceKind.Playback ? DataFlow.Render : DataFlow.Capture;
        var devices = enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active);

        string? defaultMultimedia = null;
        string? defaultCommunications = null;
        try
        {
            defaultMultimedia = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia).ID;
        }
        catch
        {
            // No default device.
        }

        try
        {
            defaultCommunications = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Communications).ID;
        }
        catch
        {
            // No default communications device.
        }

        return devices
            .Select(device => new AudioDeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                Kind = kind,
                IsDefaultMultimedia = string.Equals(device.ID, defaultMultimedia, StringComparison.OrdinalIgnoreCase),
                IsDefaultCommunications = string.Equals(device.ID, defaultCommunications, StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public AudioDeviceInfo? GetDefault(AudioDeviceKind kind, bool communications = false)
    {
        return ListDevices(kind).FirstOrDefault(device =>
            communications ? device.IsDefaultCommunications : device.IsDefaultMultimedia);
    }

    public bool IsDeviceActive(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return ListDevices(AudioDeviceKind.Playback)
            .Concat(ListDevices(AudioDeviceKind.Recording))
            .Any(device => device.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
    }

    public bool LooksLikeHeadset(AudioDeviceInfo device) =>
        HeadsetNameHints.Any(hint => device.Name.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True while Air Link / USB Link audio endpoints are present.
    /// This is more accurate than watching OVRService, which often stays running after Link ends.
    /// </summary>
    public bool IsLinkAudioSessionActive(AudioSwitchSettings settings)
    {
        var playback = ListDevices(AudioDeviceKind.Playback);
        if (!string.IsNullOrWhiteSpace(settings.VrPlaybackDeviceId))
        {
            return playback.Any(device => device.Id.Equals(settings.VrPlaybackDeviceId, StringComparison.OrdinalIgnoreCase));
        }

        return playback.Any(LooksLikeHeadset);
    }

    public string DescribeLinkAudioState(AudioSwitchSettings settings)
    {
        var active = IsLinkAudioSessionActive(settings);
        var playbackDefault = GetDefault(AudioDeviceKind.Playback)?.Name ?? "none";
        return active
            ? $"Link audio active. Default playback: {playbackDefault}"
            : $"Link audio inactive. Default playback: {playbackDefault}";
    }

    public string SetDefault(string deviceId, bool includeCommunications)
    {
        try
        {
            PolicyConfig.SetDefaultEndpoint(deviceId, includeCommunications);
            var name = FindName(deviceId) ?? deviceId;
            return $"Default audio device set to '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Could not set default audio device: {ex.Message}";
        }
    }

    public string SetCommunicationsDefault(string deviceId)
    {
        try
        {
            PolicyConfig.SetDefaultEndpoint(deviceId, setMultimedia: false, setCommunications: true);
            var name = FindName(deviceId) ?? deviceId;
            return $"Default communications device set to '{name}'.";
        }
        catch (Exception ex)
        {
            return $"Could not set communications device: {ex.Message}";
        }
    }

    public string ApplyVrDevices(AudioSwitchSettings settings)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.VrPlaybackDeviceId))
        {
            if (!IsDeviceActive(settings.VrPlaybackDeviceId))
            {
                messages.Add("VR playback device is not active yet (headset may still be connecting).");
            }
            else
            {
                messages.Add(SetDefault(settings.VrPlaybackDeviceId, includeCommunications: false));
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.VrRecordingDeviceId))
        {
            if (IsDeviceActive(settings.VrRecordingDeviceId))
            {
                messages.Add(SetDefault(settings.VrRecordingDeviceId, includeCommunications: false));
            }
        }

        if (settings.AlsoSetCommunicationsRole)
        {
            var commPlayback = settings.VrCommunicationsPlaybackDeviceId ?? settings.VrPlaybackDeviceId;
            var commRecording = settings.VrCommunicationsRecordingDeviceId ?? settings.VrRecordingDeviceId;
            if (!string.IsNullOrWhiteSpace(commPlayback) && IsDeviceActive(commPlayback))
            {
                messages.Add(SetCommunicationsDefault(commPlayback));
            }

            if (!string.IsNullOrWhiteSpace(commRecording) && IsDeviceActive(commRecording))
            {
                messages.Add(SetCommunicationsDefault(commRecording));
            }
        }

        return messages.Count == 0
            ? "No VR audio devices are configured or active."
            : string.Join(" ", messages);
    }

    public string RestoreFallbackDevices(AudioSwitchSettings settings)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.FallbackPlaybackDeviceId)
            && IsDeviceActive(settings.FallbackPlaybackDeviceId))
        {
            messages.Add(SetDefault(settings.FallbackPlaybackDeviceId, includeCommunications: false));
        }

        if (!string.IsNullOrWhiteSpace(settings.FallbackRecordingDeviceId)
            && IsDeviceActive(settings.FallbackRecordingDeviceId))
        {
            messages.Add(SetDefault(settings.FallbackRecordingDeviceId, includeCommunications: false));
        }

        if (settings.AlsoSetCommunicationsRole)
        {
            var commPlayback = settings.FallbackCommunicationsPlaybackDeviceId ?? settings.FallbackPlaybackDeviceId;
            var commRecording = settings.FallbackCommunicationsRecordingDeviceId ?? settings.FallbackRecordingDeviceId;
            if (!string.IsNullOrWhiteSpace(commPlayback) && IsDeviceActive(commPlayback))
            {
                messages.Add(SetCommunicationsDefault(commPlayback));
            }

            if (!string.IsNullOrWhiteSpace(commRecording) && IsDeviceActive(commRecording))
            {
                messages.Add(SetCommunicationsDefault(commRecording));
            }
        }

        return messages.Count == 0
            ? "No fallback audio devices are configured or currently available."
            : string.Join(" ", messages);
    }

    public string CaptureCurrentAsFallback(AudioSwitchSettings settings)
    {
        var playback = GetDefault(AudioDeviceKind.Playback);
        var recording = GetDefault(AudioDeviceKind.Recording);
        var commPlayback = GetDefault(AudioDeviceKind.Playback, communications: true);
        var commRecording = GetDefault(AudioDeviceKind.Recording, communications: true);

        // Never store the headset itself as the fallback.
        if (playback is not null
            && !IsConfiguredVrDevice(settings, playback.Id)
            && !LooksLikeHeadset(playback))
        {
            settings.FallbackPlaybackDeviceId = playback.Id;
        }

        if (recording is not null
            && !IsConfiguredVrDevice(settings, recording.Id)
            && !LooksLikeHeadset(recording))
        {
            settings.FallbackRecordingDeviceId = recording.Id;
        }

        if (commPlayback is not null
            && !IsConfiguredVrDevice(settings, commPlayback.Id)
            && !LooksLikeHeadset(commPlayback))
        {
            settings.FallbackCommunicationsPlaybackDeviceId = commPlayback.Id;
        }

        if (commRecording is not null
            && !IsConfiguredVrDevice(settings, commRecording.Id)
            && !LooksLikeHeadset(commRecording))
        {
            settings.FallbackCommunicationsRecordingDeviceId = commRecording.Id;
        }

        return $"Stored fallback playback='{FindName(settings.FallbackPlaybackDeviceId) ?? "none"}', recording='{FindName(settings.FallbackRecordingDeviceId) ?? "none"}'.";
    }

    public string? FindName(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return ListDevices(AudioDeviceKind.Playback)
            .Concat(ListDevices(AudioDeviceKind.Recording))
            .FirstOrDefault(device => device.Id.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private static bool IsConfiguredVrDevice(AudioSwitchSettings settings, string deviceId) =>
        string.Equals(settings.VrPlaybackDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings.VrRecordingDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings.VrCommunicationsPlaybackDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings.VrCommunicationsRecordingDeviceId, deviceId, StringComparison.OrdinalIgnoreCase);
}
