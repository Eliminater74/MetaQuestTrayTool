using System.Globalization;
using System.IO;
using System.Speech.Synthesis;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Speaks short PCVR status phrases on the Quest headset so users hear what the tray is doing
/// when desktop notifications are not visible in the headset.
/// </summary>
public sealed class HeadsetAnnouncerService : IDisposable
{
    private const int GapBetweenMessagesMs = 1200;
    private const int DisconnectDelayMs = 350;

    private readonly App _app;
    private readonly object _queueLock = new();
    private readonly Queue<string> _queue = new();
    private SpeechSynthesizer? _synthesizer;
    private DispatcherTimer? _gapTimer;
    private bool _speaking;

    public HeadsetAnnouncerService(App app) => _app = app;

    public string Status =>
        _app.Settings.Current.HeadsetAnnouncer.Enabled
            ? "Headset announcements on."
            : "Headset announcements off.";

    public void Reload()
    {
        _synthesizer?.Dispose();
        _synthesizer = null;

        if (!_app.Settings.Current.HeadsetAnnouncer.Enabled)
        {
            return;
        }

        try
        {
            _synthesizer = new SpeechSynthesizer { Rate = 0 };
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Headset announcer unavailable: {ex.Message}");
        }
    }

    public void TestSpeak()
    {
        Enqueue(HeadsetAnnounceKind.SessionConnect, "Headset announcements test.", delayMs: 0, force: true);
    }

    public void AnnounceSessionConnected(VrConnectionStatus status)
    {
        var phrase = status.Kind switch
        {
            VrConnectionKind.MetaAirLink => "Connected. Air Link.",
            VrConnectionKind.MetaWiredLink => "Connected. Wired Link.",
            VrConnectionKind.SteamLinkOrSteamVr => "Connected. Steam Link.",
            VrConnectionKind.VirtualDesktop => "Connected. Virtual Desktop.",
            _ => "Connected."
        };
        Enqueue(HeadsetAnnounceKind.SessionConnect, phrase);
    }

    public void AnnounceSessionDisconnected(VrConnectionKind? previous)
    {
        var phrase = previous switch
        {
            VrConnectionKind.MetaAirLink => "Air Link session ended.",
            VrConnectionKind.MetaWiredLink => "Wired Link session ended.",
            VrConnectionKind.SteamLinkOrSteamVr => "Steam Link session ended.",
            VrConnectionKind.VirtualDesktop => "Virtual Desktop session ended.",
            _ => "PCVR session ended."
        };
        Enqueue(HeadsetAnnounceKind.SessionDisconnect, phrase, delayMs: DisconnectDelayMs, allowWithoutLiveSession: true);
    }

    public void AnnounceProfileApplied(string profileName)
    {
        Enqueue(HeadsetAnnounceKind.ProfileApplied, $"Applying profile. {SanitizeName(profileName)}.");
    }

    public void AnnounceProfileRestored(string profileName)
    {
        Enqueue(HeadsetAnnounceKind.ProfileRestored, $"Restored global settings after {SanitizeName(profileName)}.");
    }

    public void AnnounceGameLaunch(string gameName)
    {
        Enqueue(HeadsetAnnounceKind.GameLaunch, $"Launching. {SanitizeName(gameName)}.");
    }

    public void AnnounceDashToSteamVr()
    {
        Enqueue(HeadsetAnnounceKind.DashToSteamVr, "Starting SteamVR.");
    }

    public void AnnounceSteamVrExit()
    {
        Enqueue(HeadsetAnnounceKind.SteamVrExit, "SteamVR exited. Restarting Meta service.");
    }

    public void AnnounceSteamLink(string phrase)
    {
        Enqueue(HeadsetAnnounceKind.SteamLinkAssist, phrase);
    }

    public void Dispose()
    {
        _gapTimer?.Stop();
        _synthesizer?.Dispose();
        _synthesizer = null;
    }

    private void Enqueue(
        HeadsetAnnounceKind kind,
        string phrase,
        int? delayMs = null,
        bool allowWithoutLiveSession = false,
        bool force = false)
    {
        if (!force && !ShouldAnnounce(kind))
        {
            return;
        }

        if (_synthesizer is null && force)
        {
            Reload();
        }

        if (_synthesizer is null)
        {
            return;
        }

        var delay = Math.Clamp(delayMs ?? _app.Settings.Current.HeadsetAnnouncer.DelayMs, 0, 5000);
        _ = Task.Run(async () =>
        {
            if (delay > 0)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }

            if (!force && !CanSpeakToHeadset(allowWithoutLiveSession))
            {
                _app.Log.Info($"Headset announcer skipped (no headset audio path): {phrase}");
                return;
            }

            lock (_queueLock)
            {
                _queue.Enqueue(phrase);
            }

            _app.Dispatcher.BeginInvoke(new Action(BeginDrainQueue));
        });
    }

    private bool ShouldAnnounce(HeadsetAnnounceKind kind)
    {
        var settings = _app.Settings.Current.HeadsetAnnouncer;
        if (!settings.Enabled)
        {
            return false;
        }

        if (settings.QuietWhileGameProfileActive && _app.IsGameProfileActive)
        {
            return kind is HeadsetAnnounceKind.SessionConnect or HeadsetAnnounceKind.SessionDisconnect;
        }

        return kind switch
        {
            HeadsetAnnounceKind.SessionConnect => settings.SessionConnect,
            HeadsetAnnounceKind.SessionDisconnect => settings.SessionDisconnect,
            HeadsetAnnounceKind.ProfileApplied or HeadsetAnnounceKind.ProfileRestored => settings.Profiles,
            HeadsetAnnounceKind.GlobalDefaults => settings.Profiles,
            HeadsetAnnounceKind.GameLaunch => settings.GameLaunch,
            HeadsetAnnounceKind.DashToSteamVr or HeadsetAnnounceKind.SteamVrExit => settings.DashToSteamVr,
            HeadsetAnnounceKind.SteamLinkAssist => settings.SteamLinkAssist,
            _ => false
        };
    }

    private bool CanSpeakToHeadset(bool allowWithoutLiveSession)
    {
        if (!allowWithoutLiveSession)
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            if (!status.SessionActive)
            {
                return false;
            }
        }

        if (_app.Audio.IsCurrentPlaybackHeadset())
        {
            return true;
        }

        var audio = _app.Settings.Current.Audio;
        if (audio.AutoSwitchEnabled && _app.Audio.IsLinkAudioSessionActive(audio))
        {
            return true;
        }

        return ResolveHeadsetPlaybackId() is not null;
    }

    private void BeginDrainQueue()
    {
        if (_speaking)
        {
            return;
        }

        string? next;
        lock (_queueLock)
        {
            if (_queue.Count == 0)
            {
                return;
            }

            next = _queue.Dequeue();
        }

        _speaking = true;
        try
        {
            SpeakNow(next);
            _app.Log.Info($"Headset announcer: {next}");
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Headset announcer speak failed: {ex.Message}");
            _speaking = false;
            BeginDrainQueue();
        }
    }

    private void SpeakNow(string phrase)
    {
        if (_synthesizer is null || string.IsNullOrWhiteSpace(phrase))
        {
            _speaking = false;
            BeginDrainQueue();
            return;
        }

        if (_app.Audio.IsCurrentPlaybackHeadset())
        {
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.SpeakCompleted += OnSpeakCompleted;
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(phrase);
            return;
        }

        var deviceId = ResolveHeadsetPlaybackId();
        if (deviceId is null)
        {
            _speaking = false;
            BeginDrainQueue();
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                SpeakViaWasapi(phrase, deviceId);
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Headset announcer WASAPI failed: {ex.Message}");
            }
            finally
            {
                _app.Dispatcher.BeginInvoke(ScheduleNextAfterGap);
            }
        });
    }

    private void OnSpeakCompleted(object? sender, SpeakCompletedEventArgs e)
    {
        if (_synthesizer is not null)
        {
            _synthesizer.SpeakCompleted -= OnSpeakCompleted;
        }

        ScheduleNextAfterGap();
    }

    private void ScheduleNextAfterGap()
    {
        _gapTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GapBetweenMessagesMs) };
        _gapTimer.Tick -= GapTimer_Tick;
        _gapTimer.Tick += GapTimer_Tick;
        _gapTimer.Stop();
        _gapTimer.Start();
    }

    private void GapTimer_Tick(object? sender, EventArgs e)
    {
        _gapTimer?.Stop();
        _speaking = false;
        BeginDrainQueue();
    }

    private void SpeakViaWasapi(string phrase, string deviceId)
    {
        using var ms = new MemoryStream();
        _synthesizer!.SetOutputToWaveStream(ms);
        _synthesizer.Speak(phrase);
        ms.Position = 0;

        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(deviceId);
        using var reader = new WaveFileReader(ms);
        using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 200);
        output.Init(reader);
        output.Play();
        while (output.PlaybackState == PlaybackState.Playing)
        {
            Thread.Sleep(50);
        }
    }

    private string? ResolveHeadsetPlaybackId()
    {
        var audio = _app.Settings.Current.Audio;
        var playback = _app.Audio.ListDevices(AudioDeviceKind.Playback);

        if (!string.IsNullOrWhiteSpace(audio.VrPlaybackDeviceId))
        {
            var configured = playback.FirstOrDefault(device =>
                device.Id.Equals(audio.VrPlaybackDeviceId, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
            {
                return configured.Id;
            }
        }

        var headset = playback.FirstOrDefault(device =>
            _app.Audio.LooksLikeHeadset(device)
            && (!_app.Audio.IsPersistentVirtualHeadsetDriver(device) || device.IsDefaultMultimedia));
        return headset?.Id;
    }

    private static string SanitizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "game";
        }

        if (trimmed.Length <= 48)
        {
            return trimmed;
        }

        return trimmed[..45] + "...";
    }
}
