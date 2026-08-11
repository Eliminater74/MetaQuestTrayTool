using NAudio.CoreAudioApi;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Native;

namespace MetaQuestTrayTool.Services;

public sealed class AudioDeviceService
{
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

    public AudioDeviceInfo? GetDefault(AudioDeviceKind kind)
    {
        return ListDevices(kind).FirstOrDefault(device => device.IsDefaultMultimedia);
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

    public string ApplyVrDevices(AudioSwitchSettings settings)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.VrPlaybackDeviceId))
        {
            messages.Add(SetDefault(settings.VrPlaybackDeviceId, settings.AlsoSetCommunicationsRole));
        }

        if (!string.IsNullOrWhiteSpace(settings.VrRecordingDeviceId))
        {
            messages.Add(SetDefault(settings.VrRecordingDeviceId, settings.AlsoSetCommunicationsRole));
        }

        return messages.Count == 0
            ? "No VR audio devices are configured."
            : string.Join(" ", messages);
    }

    public string RestoreFallbackDevices(AudioSwitchSettings settings)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.FallbackPlaybackDeviceId))
        {
            messages.Add(SetDefault(settings.FallbackPlaybackDeviceId, settings.AlsoSetCommunicationsRole));
        }

        if (!string.IsNullOrWhiteSpace(settings.FallbackRecordingDeviceId))
        {
            messages.Add(SetDefault(settings.FallbackRecordingDeviceId, settings.AlsoSetCommunicationsRole));
        }

        return messages.Count == 0
            ? "No fallback audio devices are configured."
            : string.Join(" ", messages);
    }

    public string CaptureCurrentAsFallback(AudioSwitchSettings settings)
    {
        settings.FallbackPlaybackDeviceId = GetDefault(AudioDeviceKind.Playback)?.Id;
        settings.FallbackRecordingDeviceId = GetDefault(AudioDeviceKind.Recording)?.Id;
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
}
