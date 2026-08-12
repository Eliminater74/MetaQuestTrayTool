using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Executes tray actions shared by hotkeys (and future voice commands).
/// </summary>
public sealed class HotKeyCommandService
{
    private readonly App _app;

    public HotKeyCommandService(App app)
    {
        _app = app;
    }

    public string Execute(HotKeyAction action)
    {
        return action switch
        {
            HotKeyAction.ApplyGlobal => ExecuteApplyGlobal(),
            HotKeyAction.RestartOvrService => _app.Oculus.Restart(),
            HotKeyAction.AswOff => SetAsw(AswMode.Off),
            HotKeyAction.AswAuto => SetAsw(AswMode.Auto),
            HotKeyAction.AswClock45 => SetAsw(AswMode.Clock45),
            HotKeyAction.AswCycle => CycleAsw(),
            HotKeyAction.SuperSamplingCycle => CycleSuperSampling(),
            HotKeyAction.TogglePerfHud => TogglePerfHud(),
            HotKeyAction.VoicePushToTalk => ExecuteVoicePushToTalk(),
            HotKeyAction.OpenMetaLink => _app.Oculus.ShowMetaHorizonLink(),
            _ => $"Unknown hotkey action: {action}"
        };
    }

    private string ExecuteVoicePushToTalk()
    {
        if (!_app.Settings.Current.Voice.Enabled)
        {
            return "Voice commands are disabled.";
        }

        _app.Voice.ListenOnce();
        return "Voice push-to-talk listening.";
    }

    private string GuardProfileMutation(string actionLabel)
    {
        if (!_app.IsGameProfileActive)
        {
            return string.Empty;
        }

        var profile = _app.ActiveProfileName ?? "profile";
        return $"{actionLabel} skipped — personal profile '{profile}' is active.";
    }

    private string ExecuteApplyGlobal()
    {
        var summary = _app.ApplyGlobalBaseline(notify: false);
        return summary.Length == 0 ? "Global defaults applied." : summary;
    }

    private string SetAsw(AswMode mode)
    {
        var blocked = GuardProfileMutation("ASW change");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        var sessionBlock = GuardMetaOdt("ASW change");
        if (sessionBlock.Length > 0)
        {
            return sessionBlock;
        }

        var game = _app.Settings.Current.DefaultGameSettings;
        game.AswMode = mode;
        _app.Settings.Save();
        return ApplyGameSettings($"ASW → {FormatAsw(mode)}");
    }

    private string CycleAsw()
    {
        var blocked = GuardProfileMutation("ASW cycle");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        var sessionBlock = GuardMetaOdt("ASW cycle");
        if (sessionBlock.Length > 0)
        {
            return sessionBlock;
        }

        var game = _app.Settings.Current.DefaultGameSettings;
        game.AswMode = game.AswMode switch
        {
            AswMode.Off => AswMode.Auto,
            AswMode.Auto => AswMode.Clock45,
            AswMode.Clock45 => AswMode.Clock30,
            AswMode.Clock30 => AswMode.Clock18,
            _ => AswMode.Off
        };
        _app.Settings.Save();
        return ApplyGameSettings($"ASW → {FormatAsw(game.AswMode)}");
    }

    private string CycleSuperSampling()
    {
        var blocked = GuardProfileMutation("Super sampling cycle");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        var sessionBlock = GuardMetaOdt("Super sampling cycle");
        if (sessionBlock.Length > 0)
        {
            return sessionBlock;
        }

        var game = _app.Settings.Current.DefaultGameSettings;
        var presets = GameSettings.SuperSamplingPresets.Where(value => value > 0).ToList();
        if (presets.Count == 0)
        {
            return "No super sampling presets configured.";
        }

        var current = game.SuperSampling <= 0 ? presets[0] : game.SuperSampling;
        var index = presets.FindIndex(value => Math.Abs(value - current) < 0.001);
        var next = presets[(index + 1) % presets.Count];
        game.SuperSampling = next;
        _app.Settings.Save();
        return ApplyGameSettings($"SS → {next:0.0}");
    }

    private string TogglePerfHud()
    {
        var blocked = GuardProfileMutation("Performance HUD toggle");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        var sessionBlock = GuardMetaOdt("Performance HUD toggle");
        if (sessionBlock.Length > 0)
        {
            return sessionBlock;
        }

        var game = _app.Settings.Current.DefaultGameSettings;
        game.VisualHud = game.VisualHud == VisualHudMode.None
            ? VisualHudMode.Performance
            : VisualHudMode.None;
        _app.Settings.Save();
        return ApplyGameSettings(game.VisualHud == VisualHudMode.None
            ? "Perf HUD off"
            : "Perf HUD on");
    }

    private string GuardMetaOdt(string actionLabel)
    {
        var caps = _app.LinkConnection.GetCapabilities();
        if (caps.AllowsOculusDebugTool)
        {
            return string.Empty;
        }

        return $"{actionLabel} skipped — {caps.OdtSkipMessage}";
    }

    private string ApplyGameSettings(string title)
    {
        var caps = _app.LinkConnection.GetCapabilities();
        if (!caps.AllowsOculusDebugTool)
        {
            return caps.OdtSkipMessage;
        }

        if (!_app.DebugTool.IsAvailable)
        {
            return "OculusDebugToolCLI not found.";
        }

        var result = _app.DebugTool.Apply(_app.Settings.Current.DefaultGameSettings);
        if (!result.CliFound || !result.Started)
        {
            _app.Log.Error(result.Summary);
            return result.Summary;
        }

        if (result.LooksRejected)
        {
            _app.Log.Warn(result.Summary);
        }
        else
        {
            _app.Log.Info($"{title}: {result.Summary}");
        }

        return $"{title}. {result.Summary}";
    }

    private static string FormatAsw(AswMode mode) => mode switch
    {
        AswMode.Off => "Off",
        AswMode.Auto => "Auto",
        AswMode.Clock45 => "45 FPS",
        AswMode.Clock30 => "30 FPS",
        AswMode.Clock18 => "18 FPS",
        _ => mode.ToString()
    };
}
