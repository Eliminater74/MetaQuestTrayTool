using System.Globalization;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Windows speech recognition for fixed OTT-style voice commands.
/// Uses push-to-talk by default; optional always-on listening.
/// </summary>
public sealed class VoiceCommandService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int PushToTalkHotKeyId = VoicePhraseCatalog.PushToTalkHotKeyId;

    private readonly App _app;
    private readonly HotKeyCommandService _commands;
    private SpeechRecognitionEngine? _engine;
    private SpeechSynthesizer? _synthesizer;
    private PushToTalkWindow? _pushToTalkWindow;
    private bool _listeningOnce;
    private bool _isListeningContinuous;
    private string? _micOverridePreviousId;
    private bool _micOverrideActive;

    public VoiceCommandService(App app, HotKeyCommandService commands)
    {
        _app = app;
        _commands = commands;
    }

    public bool IsAvailable { get; private set; }
    public string Status { get; private set; } = "Voice commands are off.";

    public void Reload()
    {
        StopRecognition();
        UnregisterPushToTalk();
        IsAvailable = false;

        var voice = _app.Settings.Current.Voice;
        if (!voice.Enabled)
        {
            Status = "Voice commands are off.";
            return;
        }

        if (HotKeyChordHelper.ConflictsWithHotKeys(voice, _app.Settings.Current.HotKeys))
        {
            Status = $"Voice push-to-talk conflicts with a hotkey ({DescribePushToTalk(voice)}). Change one shortcut.";
            _app.Log.Warn(Status);
            return;
        }

        try
        {
            _engine = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            _engine.LoadGrammar(BuildGrammar());
            _engine.SetInputToDefaultAudioDevice();
            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechRecognitionRejected += OnSpeechRejected;
            IsAvailable = true;
            Status = voice.PushToTalkOnly
                ? $"Voice ready. Press {DescribePushToTalk(voice)} then speak a command."
                : $"Voice listening continuously (min confidence {voice.MinConfidence:P0}). Speak a command.";
        }
        catch (Exception ex)
        {
            Status = $"Voice recognition unavailable: {ex.Message}";
            _app.Log.Warn(Status);
            DisposeEngine();
            return;
        }

        if (voice.AudioConfirmation)
        {
            try
            {
                _synthesizer?.Dispose();
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
                _synthesizer.Rate = 1;
                TtsVoiceCatalog.Apply(_synthesizer, _app.Settings.Current.HeadsetAnnouncer.VoiceName);
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Voice feedback unavailable: {ex.Message}");
            }
        }
        else
        {
            _synthesizer?.Dispose();
            _synthesizer = null;
        }

        if (voice.PushToTalkOnly)
        {
            RegisterPushToTalk(voice);
        }
        else
        {
            ApplyPreferredMic(forContinuous: true);
            StartContinuousListening();
        }

        _app.Log.Info(Status);
    }

    public void ListenOnce()
    {
        if (!IsAvailable || _engine is null || !_app.Settings.Current.Voice.PushToTalkOnly)
        {
            return;
        }

        if (_listeningOnce)
        {
            _app.Log.Info("Voice is already listening.");
            return;
        }

        try
        {
            ApplyPreferredMic(forContinuous: false);
            _listeningOnce = true;
            Speak("Listening");
            _engine.RecognizeAsync(RecognizeMode.Single);
            _app.Log.Info("Voice push-to-talk: listening for one command.");
        }
        catch (Exception ex)
        {
            _listeningOnce = false;
            RestorePreferredMic();
            _app.Log.Error("Voice listen failed.", ex);
        }
    }

    public void Dispose()
    {
        StopRecognition();
        RestorePreferredMic();
        UnregisterPushToTalk();
        DisposeEngine();
        _synthesizer?.Dispose();
        _synthesizer = null;
    }

    private Grammar BuildGrammar()
    {
        var choices = new Choices();
        foreach (var phrase in VoicePhraseCatalog.Phrases)
        {
            choices.Add(phrase.Phrase);
        }

        foreach (var phrase in _app.Settings.Current.Voice.NormalizedCustomPhrases())
        {
            choices.Add(phrase.Phrase);
        }

        return new Grammar(new GrammarBuilder(choices)) { Name = "MetaQuestTrayToolVoice" };
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        _listeningOnce = false;
        var text = e.Result?.Text ?? string.Empty;
        var confidence = e.Result?.Confidence ?? 0;
        _app.Dispatcher.BeginInvoke(() => HandleRecognition(text, accepted: true, confidence));
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        _listeningOnce = false;
        _app.Dispatcher.BeginInvoke(() => HandleRecognition(string.Empty, accepted: false, confidence: 0));
    }

    private void HandleRecognition(string text, bool accepted, float confidence)
    {
        var voice = _app.Settings.Current.Voice;
        if (voice.PushToTalkOnly)
        {
            RestorePreferredMic();
        }

        var minConfidence = Math.Clamp(voice.MinConfidence, 0.30f, 0.95f);
        if (accepted && confidence < minConfidence)
        {
            var low = $"Voice ignored (confidence {confidence:P0} < {minConfidence:P0}): \"{text}\".";
            _app.Log.Info(low);
            if (!voice.PushToTalkOnly && _app.Settings.Current.ShowNotifications)
            {
                // Always-on: stay quiet on low-confidence noise unless debugging via log.
            }

            if (voice.PushToTalkOnly)
            {
                Speak("Sorry");
                if (_app.Settings.Current.ShowNotifications)
                {
                    _app.TrayNotify("Voice", low);
                }
            }

            return;
        }

        if (!accepted || !VoicePhraseCatalog.TryMatch(text, voice.NormalizedCustomPhrases(), out var action))
        {
            var message = string.IsNullOrWhiteSpace(text)
                ? "Voice command not recognized."
                : $"Voice command not recognized: \"{text}\".";
            _app.Log.Info(message);
            Speak("Sorry");
            if (_app.Settings.Current.ShowNotifications)
            {
                _app.TrayNotify("Voice", message);
            }

            return;
        }

        try
        {
            var summary = _commands.Execute(action);
            var label = HotKeyCatalog.DescribeAction(action);
            _app.Log.Info($"Voice \"{text}\" ({confidence:P0}) → {label}: {summary}");
            if (!HeadsetAnnouncerHandles(action))
            {
                Speak(label);
            }
            if (_app.Settings.Current.ShowNotifications)
            {
                _app.TrayNotify("Voice", $"{label}\n{summary}");
            }
        }
        catch (Exception ex)
        {
            _app.Log.Error("Voice command failed.", ex);
            Speak("Error");
        }
    }

    private bool HeadsetAnnouncerHandles(HotKeyAction action)
    {
        var settings = _app.Settings.Current.HeadsetAnnouncer;
        if (!settings.Enabled)
        {
            return false;
        }

        return action is HotKeyAction.DashToSteamVr or HotKeyAction.StartSteamVr
            ? settings.DashToSteamVr
            : action != HotKeyAction.VoicePushToTalk && settings.ActionResults;
    }

    private void ApplyPreferredMic(bool forContinuous)
    {
        var preferred = (_app.Settings.Current.Voice.PreferredRecordingDeviceId ?? string.Empty).Trim();
        if (preferred.Length == 0)
        {
            return;
        }

        if (!_app.Audio.IsDeviceActive(preferred))
        {
            _app.Log.Warn("Voice preferred mic is not active — using Windows default capture.");
            return;
        }

        try
        {
            var current = _app.Audio.GetDefault(AudioDeviceKind.Recording);
            if (current?.Id is string id && id.Equals(preferred, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _micOverridePreviousId ??= current?.Id;
            _app.Audio.SetDefault(preferred, includeCommunications: false);
            _micOverrideActive = true;
            _app.Log.Info(forContinuous
                ? "Voice using preferred microphone (always-on)."
                : "Voice temporarily switched to preferred microphone.");
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Could not switch voice microphone: {ex.Message}");
        }
    }

    private void RestorePreferredMic()
    {
        if (!_micOverrideActive)
        {
            _micOverridePreviousId = null;
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_micOverridePreviousId)
                && _app.Audio.IsDeviceActive(_micOverridePreviousId))
            {
                _app.Audio.SetDefault(_micOverridePreviousId, includeCommunications: false);
                _app.Log.Info("Restored previous default microphone after voice listen.");
            }
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Could not restore previous microphone: {ex.Message}");
        }
        finally
        {
            _micOverrideActive = false;
            _micOverridePreviousId = null;
        }
    }

    private void Speak(string text)
    {
        if (!_app.Settings.Current.Voice.AudioConfirmation || _synthesizer is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Voice feedback failed: {ex.Message}");
        }
    }

    private void StartContinuousListening()
    {
        if (_engine is null || _isListeningContinuous)
        {
            return;
        }

        _engine.RecognizeAsync(RecognizeMode.Multiple);
        _isListeningContinuous = true;
    }

    private void StopRecognition()
    {
        if (_engine is null)
        {
            return;
        }

        try
        {
            _engine.RecognizeAsyncCancel();
            if (_isListeningContinuous)
            {
                _engine.RecognizeAsyncStop();
                _isListeningContinuous = false;
            }
        }
        catch
        {
            // Best effort.
        }

        _listeningOnce = false;
        if (!_app.Settings.Current.Voice.PushToTalkOnly)
        {
            RestorePreferredMic();
        }
    }

    private void DisposeEngine()
    {
        StopRecognition();
        if (_engine is not null)
        {
            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.SpeechRecognitionRejected -= OnSpeechRejected;
            _engine.Dispose();
            _engine = null;
        }
    }

    private void RegisterPushToTalk(VoiceSettings voice)
    {
        if (!voice.ToPushToTalkBinding().TryParseKey(out var key))
        {
            _app.Log.Warn("Voice push-to-talk key is not set.");
            return;
        }

        EnsurePushToTalkWindow();
        var modifiers = ToNativeModifiers(voice.PushToTalkModifiers) | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_pushToTalkWindow!.Handle, PushToTalkHotKeyId, modifiers, virtualKey))
        {
            _app.Log.Warn($"Voice push-to-talk hotkey not registered: {DescribePushToTalk(voice)}");
        }
    }

    private void UnregisterPushToTalk()
    {
        if (_pushToTalkWindow is null)
        {
            return;
        }

        UnregisterHotKey(_pushToTalkWindow.Handle, PushToTalkHotKeyId);
        _pushToTalkWindow.HotKeyPressed -= OnPushToTalkHotKey;
        _pushToTalkWindow.Dispose();
        _pushToTalkWindow = null;
    }

    private void EnsurePushToTalkWindow()
    {
        if (_pushToTalkWindow is not null)
        {
            return;
        }

        _pushToTalkWindow = new PushToTalkWindow();
        _pushToTalkWindow.HotKeyPressed += OnPushToTalkHotKey;
        _pushToTalkWindow.CreateControl();
    }

    private void OnPushToTalkHotKey(int id)
    {
        if (id != PushToTalkHotKeyId)
        {
            return;
        }

        _app.Dispatcher.BeginInvoke(ListenOnce);
    }

    public static string DescribePushToTalk(VoiceSettings voice)
    {
        return voice.ToPushToTalkBinding().DescribeChord();
    }

    private static uint ToNativeModifiers(HotKeyModifiers modifiers)
    {
        uint native = 0;
        if (modifiers.HasFlag(HotKeyModifiers.Alt))
        {
            native |= 0x0001;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Control))
        {
            native |= 0x0002;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Shift))
        {
            native |= 0x0004;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Windows))
        {
            native |= 0x0008;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class PushToTalkWindow : Form
    {
        public event Action<int>? HotKeyPressed;

        public PushToTalkWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Size = new System.Drawing.Size(0, 0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotKey)
            {
                HotKeyPressed?.Invoke(m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }
    }
}
