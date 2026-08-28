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
    /// <summary>Minimum wait after Link connect so Meta virtual audio is up before the first phrase.</summary>
    private const int ConnectDelayFloorMs = 2200;

    private readonly App _app;
    private readonly object _queueLock = new();
    private readonly Queue<QueuedAnnouncement> _queue = new();
    private CancellationTokenSource _lifetimeCts = new();
    private SpeechSynthesizer? _synthesizer;
    private DispatcherTimer? _gapTimer;
    private bool _speaking;
    private int _generation;
    private bool _disposed;

    public HeadsetAnnouncerService(App app) => _app = app;

    public string Status
    {
        get
        {
            if (!_app.Settings.Current.HeadsetAnnouncer.Enabled)
            {
                return "Headset announcements off.";
            }

            var voice = TtsVoiceCatalog.DescribeActive(_app.Settings.Current.HeadsetAnnouncer.VoiceName);
            return "Headset announcements on (" + voice + ").";
        }
    }

    public void Reload()
    {
        CancelPendingAnnouncements();
        _synthesizer?.Dispose();
        _synthesizer = null;

        if (!_app.Settings.Current.HeadsetAnnouncer.Enabled)
        {
            return;
        }

        EnsureSynthesizer();
    }

    private void EnsureSynthesizer()
    {
        if (_synthesizer is not null)
        {
            return;
        }

        try
        {
            _synthesizer = new SpeechSynthesizer { Rate = 0 };
            TtsVoiceCatalog.Apply(_synthesizer, _app.Settings.Current.HeadsetAnnouncer.VoiceName);
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Headset announcer unavailable: {ex.Message}");
        }
    }

    public void TestSpeak()
    {
        Enqueue(
            HeadsetAnnounceKind.SessionConnect,
            "Headset announcements test. This is the selected voice.",
            delayMs: 0,
            force: true);
    }

    public void AnnounceSessionConnected(VrConnectionStatus status)
    {
        var phrase = BuildConnectPhrase(status);
        var settings = _app.Settings.Current.HeadsetAnnouncer;
        var connectDelay = Math.Clamp(
            Math.Max(settings.ConnectDelayMs, settings.DelayMs),
            ConnectDelayFloorMs,
            8000);
        Enqueue(HeadsetAnnounceKind.SessionConnect, phrase, delayMs: connectDelay);
    }

    /// <summary>True when the session-connect phrase already tells the user SteamVR is starting.</summary>
    public bool ConnectPhraseCoversSteamVrStart(VrConnectionStatus status)
    {
        if (!ShouldAnnounce(HeadsetAnnounceKind.SessionConnect))
        {
            return false;
        }

        return status.Kind is VrConnectionKind.MetaAirLink or VrConnectionKind.MetaWiredLink
               && PcvrSetup.GetMode(_app) == PcvrSetupMode.SteamVrOverMetaLink;
    }

    private string BuildConnectPhrase(VrConnectionStatus status)
    {
        var linkLabel = status.Kind switch
        {
            VrConnectionKind.MetaAirLink => "Air Link",
            VrConnectionKind.MetaWiredLink => "Wired Link",
            VrConnectionKind.SteamLinkOrSteamVr => "Steam Link",
            VrConnectionKind.VirtualDesktop => "Virtual Desktop",
            _ => "PCVR"
        };
        var openXrPhrase = BuildOpenXrPhrase();
        var audioPhrase = _app.Settings.Current.Audio.AutoSwitchEnabled
            ? " VR audio switching is enabled."
            : string.Empty;

        if (status.Kind is VrConnectionKind.MetaAirLink or VrConnectionKind.MetaWiredLink)
        {
            if (PcvrSetup.GetMode(_app) == PcvrSetupMode.SteamVrOverMetaLink)
            {
                return $"Connected. {linkLabel}. SteamVR OpenXR runtime will be used. "
                       + $"Now starting SteamVR.{audioPhrase}";
            }

            return $"Connected. {linkLabel}. {openXrPhrase} Meta Horizon will load.{audioPhrase}";
        }

        return status.Kind switch
        {
            VrConnectionKind.SteamLinkOrSteamVr =>
                $"Connected. Steam Link or SteamVR. {openXrPhrase}{audioPhrase}",
            VrConnectionKind.VirtualDesktop =>
                $"Connected. Virtual Desktop. {openXrPhrase}{audioPhrase}",
            _ => $"Connected. {openXrPhrase}{audioPhrase}"
        };
    }

    /// <summary>
    /// Reports the Windows active OpenXR runtime. This is the runtime selected for
    /// newly launched OpenXR applications; an already-running game may retain the
    /// runtime it selected at launch.
    /// </summary>
    private string BuildOpenXrPhrase()
    {
        try
        {
            return _app.OpenXr.ReadActiveKind() switch
            {
                OpenXrRuntimeKind.Meta => "OpenXR is set to Meta.",
                OpenXrRuntimeKind.SteamVr => "OpenXR is set to SteamVR.",
                _ => "OpenXR runtime is unknown."
            };
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Headset announcer could not read OpenXR runtime: {ex.Message}");
            return "OpenXR runtime could not be read.";
        }
    }

    private string BuildSteamVrTargetPhrase() =>
        _app.Settings.Current.DashToSteamVr.SwitchOpenXrToSteamVr
            ? "SteamVR OpenXR runtime will be used."
            : BuildOpenXrPhrase();

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
        if (_app.Settings.Current.Audio.AutoSwitchEnabled)
        {
            phrase += " Desktop audio will be restored.";
        }

        var playbackDeviceId = ResolveHeadsetPlaybackId();
        Enqueue(
            HeadsetAnnounceKind.SessionDisconnect,
            phrase,
            delayMs: DisconnectDelayMs,
            allowWithoutLiveSession: true,
            playbackDeviceId: playbackDeviceId);
    }

    public void AnnounceProfileApplied(string profileName)
    {
        AnnounceProfileApplied(profileName, summary: null);
    }

    public void AnnounceProfileApplied(string profileName, string? summary)
    {
        var outcome = DescribeResult(summary, "Profile settings applied.");
        Enqueue(
            HeadsetAnnounceKind.ProfileApplied,
            $"Profile {SanitizeName(profileName)} applied. {outcome} {BuildOpenXrPhrase()}");
    }

    public void AnnounceProfileDetected(string profileName, string? summary)
    {
        var outcome = DescribeResult(summary, "Profile settings applied.");
        Enqueue(
            HeadsetAnnounceKind.ProfileApplied,
            $"{SanitizeName(profileName)} detected. Profile applied. {outcome} {BuildOpenXrPhrase()}");
    }

    public void AnnounceProfileApplyFailed(string profileName)
    {
        Enqueue(
            HeadsetAnnounceKind.ProfileApplied,
            $"Profile {SanitizeName(profileName)} failed to apply. Check Log.");
    }

    public void AnnounceProfileRestored(string profileName)
    {
        AnnounceProfileRestored(profileName, summary: null);
    }

    public void AnnounceProfileRestored(string profileName, string? summary)
    {
        var outcome = DescribeResult(summary, "Global settings restored.");
        Enqueue(
            HeadsetAnnounceKind.ProfileRestored,
            $"{SanitizeName(profileName)} closed. "
            + $"{outcome} {BuildOpenXrPhrase()}");
    }

    public void AnnounceProfileRestoreFailed(string profileName)
    {
        Enqueue(
            HeadsetAnnounceKind.ProfileRestored,
            $"Global settings failed to restore after {SanitizeName(profileName)}. Check Log.");
    }

    public void AnnounceGameLaunch(string gameName)
    {
        AnnounceGameLaunch(gameName, profileName: null, platform: null);
    }

    public void AnnounceGameLaunch(string gameName, string? profileName, string? platform)
    {
        var profile = string.IsNullOrWhiteSpace(profileName)
            ? "No personal profile is configured."
            : $"Profile {SanitizeName(profileName)} is armed.";
        var source = string.IsNullOrWhiteSpace(platform)
            ? string.Empty
            : $" {SanitizeName(platform)} game.";
        Enqueue(
            HeadsetAnnounceKind.GameLaunch,
            $"Launching {SanitizeName(gameName)}.{source} {profile}");
    }

    public void AnnounceGameLaunchFailed(string gameName, string? profileName)
    {
        var profile = string.IsNullOrWhiteSpace(profileName)
            ? string.Empty
            : $" Profile {SanitizeName(profileName)} was not launched.";
        Enqueue(
            HeadsetAnnounceKind.LaunchFailed,
            $"Could not launch {SanitizeName(gameName)}.{profile} Check Log for details.",
            allowWithoutLiveSession: true);
    }

    public void AnnounceActionResult(string actionName, string? summary)
    {
        Enqueue(
            HeadsetAnnounceKind.ActionResult,
            $"{SanitizeName(actionName)}. {DescribeResult(summary, "Completed.")}");
    }

    public void AnnounceAudioRouting(string summary)
    {
        Enqueue(
            HeadsetAnnounceKind.Audio,
            $"Audio routing. {DescribeResult(summary, "Completed.")}");
    }

    public void AnnounceHeadsetAction(string summary)
    {
        Enqueue(
            HeadsetAnnounceKind.Headset,
            $"Headset settings. {DescribeResult(summary, "Completed.")}");
    }

    public void AnnounceRecovery(string summary)
    {
        Enqueue(
            HeadsetAnnounceKind.Recovery,
            $"PCVR recovery. {DescribeResult(summary, "Completed.")}",
            allowWithoutLiveSession: true);
    }

    public void AnnounceExperimentalMsfsVr(string phrase)
    {
        Enqueue(
            HeadsetAnnounceKind.Experimental,
            $"MSFS experimental VR. {phrase}");
    }

    public void AnnounceDashToSteamVr() => AnnounceSteamVrStarting();

    /// <summary>Spoken before auto SteamVR launch when connect did not already announce it.</summary>
    public void AnnounceSteamVrComing(VrConnectionStatus? status = null)
    {
        if (status is not null && ConnectPhraseCoversSteamVrStart(status))
        {
            return;
        }

        Enqueue(
            HeadsetAnnounceKind.DashToSteamVr,
            $"Please wait. Starting SteamVR. {BuildSteamVrTargetPhrase()}",
            delayMs: Math.Max(400, _app.Settings.Current.HeadsetAnnouncer.DelayMs / 2),
            allowWithoutLiveSession: true,
            force: true);
    }

    /// <summary>Speak in the Quest that SteamVR is starting (manual tray / hotkey / voice).</summary>
    public void AnnounceSteamVrStarting()
    {
        EnsureSynthesizer();
        Enqueue(
            HeadsetAnnounceKind.DashToSteamVr,
            $"Starting SteamVR now. {BuildSteamVrTargetPhrase()}",
            delayMs: Math.Max(400, _app.Settings.Current.HeadsetAnnouncer.DelayMs / 2),
            allowWithoutLiveSession: true,
            force: true);
    }

    /// <summary>
    /// Speak and wait before OVRService stop / desktop audio restore so the phrase is heard in the headset.
    /// </summary>
    public void AnnounceSteamVrExitBeforeOvrStop()
    {
        SpeakAndWait(
            HeadsetAnnounceKind.SteamVrExit,
            "SteamVR closed. Stopping Meta service for 10 seconds.");
    }

    public void AnnounceSteamLink(string phrase)
    {
        Enqueue(HeadsetAnnounceKind.SteamLinkAssist, phrase);
    }

    public void Dispose()
    {
        _disposed = true;
        CancelPendingAnnouncements();
        _gapTimer?.Stop();
        _synthesizer?.Dispose();
        _synthesizer = null;
    }

    private void Enqueue(
        HeadsetAnnounceKind kind,
        string phrase,
        int? delayMs = null,
        bool allowWithoutLiveSession = false,
        string? playbackDeviceId = null,
        bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        if (!force && !ShouldAnnounce(kind))
        {
            return;
        }

        if (_synthesizer is null)
        {
            EnsureSynthesizer();
        }

        if (_synthesizer is null)
        {
            return;
        }

        var delay = Math.Clamp(delayMs ?? _app.Settings.Current.HeadsetAnnouncer.DelayMs, 0, 5000);
        var generation = Volatile.Read(ref _generation);
        var token = _lifetimeCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > 0)
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested || generation != Volatile.Read(ref _generation))
                {
                    return;
                }

                // Give Meta a moment to create/default the virtual audio endpoint after Link connect.
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    if (!string.IsNullOrWhiteSpace(playbackDeviceId)
                        || force
                        || CanSpeakToHeadset(allowWithoutLiveSession))
                    {
                        if (force && ResolveHeadsetPlaybackId() is null && !_app.Audio.IsCurrentPlaybackHeadset())
                        {
                            if (attempt < 3)
                            {
                                await Task.Delay(400, token).ConfigureAwait(false);
                                continue;
                            }

                            _app.Log.Info($"Headset announcer skipped (no headset audio path): {phrase}");
                            return;
                        }

                        break;
                    }

                    if (attempt < 3)
                    {
                        await Task.Delay(400, token).ConfigureAwait(false);
                        continue;
                    }

                    _app.Log.Info($"Headset announcer skipped (no headset audio path): {phrase}");
                    return;
                }

                if (token.IsCancellationRequested || generation != Volatile.Read(ref _generation))
                {
                    return;
                }

                lock (_queueLock)
                {
                    if (_queue.Any(item => item.Kind == kind && item.Phrase.Equals(phrase, StringComparison.Ordinal)))
                    {
                        return;
                    }

                    var item = new QueuedAnnouncement(kind, phrase, playbackDeviceId, PriorityFor(kind));
                    if (item.Priority >= 2 && _queue.Any(queued => queued.Priority < item.Priority))
                    {
                        var prioritized = new Queue<QueuedAnnouncement>();
                        prioritized.Enqueue(item);
                        foreach (var queued in _queue.Where(queued => queued.Priority >= item.Priority))
                        {
                            prioritized.Enqueue(queued);
                        }

                        _queue.Clear();
                        foreach (var queued in prioritized)
                        {
                            _queue.Enqueue(queued);
                        }
                    }
                    else
                    {
                        _queue.Enqueue(item);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    _ = _app.Dispatcher.InvokeAsync(BeginDrainQueue);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Reload / shutdown cancelled this pending phrase.
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Headset announcer queue failed: {ex.Message}");
            }
        });
    }

    private void CancelPendingAnnouncements()
    {
        Interlocked.Increment(ref _generation);
        var previous = Interlocked.Exchange(ref _lifetimeCts, new CancellationTokenSource());
        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }

        lock (_queueLock)
        {
            _queue.Clear();
        }

        _gapTimer?.Stop();
    }

    private void SpeakAndWait(HeadsetAnnounceKind kind, string phrase)
    {
        if (!ShouldAnnounce(kind))
        {
            return;
        }

        EnsureSynthesizer();
        var synth = _synthesizer;
        if (synth is null)
        {
            return;
        }

        void Run()
        {
            Interlocked.Increment(ref _generation);
            lock (_queueLock)
            {
                _queue.Clear();
            }

            try
            {
                _gapTimer?.Stop();
                synth.SpeakAsyncCancelAll();
                _speaking = true;
                if (SpeakBlocking(phrase))
                {
                    _app.Log.Info("Headset announcer: " + phrase);
                }
            }
            catch (Exception ex)
            {
                _app.Log.Warn("Headset announcer speak failed: " + ex.Message);
            }
            finally
            {
                _speaking = false;
                try
                {
                    synth.SetOutputToDefaultAudioDevice();
                }
                catch
                {
                    // restore best-effort
                }
            }
        }

        if (_app.Dispatcher.CheckAccess())
        {
            Run();
        }
        else
        {
            _app.Dispatcher.Invoke(Run);
        }
    }

    private bool SpeakBlocking(string phrase)
    {
        if (_synthesizer is null || string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        if (_app.Audio.IsCurrentPlaybackHeadset())
        {
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.Speak(phrase);
            return true;
        }

        var deviceId = ResolveHeadsetPlaybackId();
        if (deviceId is null)
        {
            _app.Log.Info("Headset announcer skipped (no headset audio path): " + phrase);
            return false;
        }

        SpeakViaWasapi(phrase, deviceId);
        return true;
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
            if (kind is HeadsetAnnounceKind.SessionConnect
                or HeadsetAnnounceKind.SessionDisconnect
                or HeadsetAnnounceKind.ProfileApplied
                or HeadsetAnnounceKind.ProfileRestored
                or HeadsetAnnounceKind.Experimental)
            {
                return true;
            }

            return kind == HeadsetAnnounceKind.SteamVrExit && settings.DashToSteamVr;
        }

        return kind switch
        {
            HeadsetAnnounceKind.SessionConnect => settings.SessionConnect,
            HeadsetAnnounceKind.SessionDisconnect => settings.SessionDisconnect,
            HeadsetAnnounceKind.ProfileApplied or HeadsetAnnounceKind.ProfileRestored => settings.Profiles,
            HeadsetAnnounceKind.GlobalDefaults => settings.ActionResults,
            HeadsetAnnounceKind.GameLaunch or HeadsetAnnounceKind.LaunchFailed => settings.GameLaunch,
            HeadsetAnnounceKind.DashToSteamVr or HeadsetAnnounceKind.SteamVrExit => settings.DashToSteamVr,
            HeadsetAnnounceKind.SteamLinkAssist => settings.SteamLinkAssist,
            HeadsetAnnounceKind.ActionResult => settings.ActionResults,
            HeadsetAnnounceKind.Audio => settings.Audio,
            HeadsetAnnounceKind.Headset => settings.Headset,
            HeadsetAnnounceKind.Recovery => settings.Recovery,
            HeadsetAnnounceKind.Experimental => settings.GameLaunch,
            _ => false
        };
    }

    private bool CanSpeakToHeadset(bool allowWithoutLiveSession)
    {
        if (!allowWithoutLiveSession)
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            var live = status.SessionActive
                        && (status.Kind is VrConnectionKind.SteamLinkOrSteamVr
                            or VrConnectionKind.VirtualDesktop
                            || status.MetaLinkStreaming);
            if (!live)
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

        QueuedAnnouncement? next;
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
            if (SpeakNow(next.Phrase, next.PlaybackDeviceId))
            {
                _app.Log.Info($"Headset announcer: {next.Phrase}");
            }
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Headset announcer speak failed: {ex.Message}");
            _speaking = false;
            BeginDrainQueue();
        }
    }

    private bool SpeakNow(string phrase, string? playbackDeviceId = null)
    {
        if (_synthesizer is null || string.IsNullOrWhiteSpace(phrase))
        {
            _speaking = false;
            BeginDrainQueue();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playbackDeviceId))
        {
            SpeakViaWasapi(phrase, playbackDeviceId);
            return true;
        }

        if (_app.Audio.IsCurrentPlaybackHeadset())
        {
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.SpeakCompleted += OnSpeakCompleted;
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(phrase);
            return true;
        }

        var deviceId = ResolveHeadsetPlaybackId();
        if (deviceId is null)
        {
            _app.Log.Info("Headset announcer skipped (no headset audio path): " + phrase);
            _speaking = false;
            BeginDrainQueue();
            return false;
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
        return true;
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
        try
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
        finally
        {
            try
            {
                _synthesizer?.SetOutputToDefaultAudioDevice();
            }
            catch
            {
                // restore best-effort so later SpeakAsync still works
            }
        }
    }

    /// <summary>
    /// Resolve a playback endpoint the Quest can hear. Meta Virtual Audio is always listed;
    /// prefer it when Link/SteamVR is live or when it is already Windows default.
    /// </summary>
    private string? ResolveHeadsetPlaybackId()
    {
        var audio = _app.Settings.Current.Audio;
        var playback = _app.Audio.ListDevices(AudioDeviceKind.Playback, force: true);

        if (!string.IsNullOrWhiteSpace(audio.VrPlaybackDeviceId))
        {
            var configured = playback.FirstOrDefault(device =>
                device.Id.Equals(audio.VrPlaybackDeviceId, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
            {
                return configured.Id;
            }
        }

        var headsets = playback.Where(device => _app.Audio.LooksLikeHeadset(device)).ToList();
        if (headsets.Count == 0)
        {
            return null;
        }

        // Prefer whatever Windows is already sending to the HMD.
        var asDefault = headsets.FirstOrDefault(device => device.IsDefaultMultimedia);
        if (asDefault is not null)
        {
            return asDefault.Id;
        }

        var sessionLive = false;
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false, includeAudioLink: true);
            sessionLive = status.SessionActive || status.MetaLinkStreaming
                          || status.SteamVrRunning || status.VirtualDesktopRunning;
        }
        catch
        {
            // probe best-effort
        }

        // Removable headset endpoints (rare for Quest) — presence is enough.
        var removable = headsets.FirstOrDefault(device => !_app.Audio.IsPersistentVirtualHeadsetDriver(device));
        if (removable is not null)
        {
            return removable.Id;
        }

        // Meta/Oculus virtual: use when a PCVR session is up so WASAPI can reach the HMD
        // even before Windows has finished flipping the default (or if auto-switch has no VR id set).
        if (sessionLive)
        {
            return headsets.FirstOrDefault(device => _app.Audio.IsPersistentVirtualHeadsetDriver(device))?.Id
                   ?? headsets[0].Id;
        }

        return null;
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

    private static int PriorityFor(HeadsetAnnounceKind kind) => kind switch
    {
        HeadsetAnnounceKind.SessionConnect
            or HeadsetAnnounceKind.SessionDisconnect
            or HeadsetAnnounceKind.SteamVrExit
            or HeadsetAnnounceKind.Recovery => 2,
        _ => 1
    };

    private static string DescribeResult(string? summary, string success)
    {
        var text = summary?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return success.TrimEnd('.', '!', ' ');
        }

        var failure = text.Contains("failed", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("could not", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("not found", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("error", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("rejected", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("needs administrator", StringComparison.OrdinalIgnoreCase);
        var skipped = text.Contains("skipped", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("not active", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("no VR audio devices", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("no fallback audio", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("unavailable", StringComparison.OrdinalIgnoreCase);

        return failure && skipped
            ? "Completed with errors and skipped settings. Check Log"
            : failure
                ? "Failed. Check Log"
                : skipped
                    ? "Completed with some settings skipped"
                    : success.TrimEnd('.', '!', ' ');
    }

    private sealed record QueuedAnnouncement(
        HeadsetAnnounceKind Kind,
        string Phrase,
        string? PlaybackDeviceId,
        int Priority);
}
