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
            _ => $"Unknown hotkey action: {action}"
        };
    }

    private string ExecuteApplyGlobal()
    {
        var summary = _app.ApplyGlobalBaseline(notify: false);
        return summary.Length == 0 ? "Global defaults applied." : summary;
    }

    private string SetAsw(AswMode mode)
    {
        var game = _app.Settings.Current.DefaultGameSettings;
        game.AswMode = mode;
        _app.Settings.Save();
        return ApplyGameSettings($"ASW → {FormatAsw(mode)}");
    }

    private string CycleAsw()
    {
        var game = _app.Settings.Current.DefaultGameSettings;
        game.AswMode = game.AswMode switch
        {
            AswMode.Off => AswMode.Auto,
            AswMode.Auto => AswMode.Clock45,
            _ => AswMode.Off
        };
        _app.Settings.Save();
        return ApplyGameSettings($"ASW → {FormatAsw(game.AswMode)}");
    }

    private string CycleSuperSampling()
    {
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
        var game = _app.Settings.Current.DefaultGameSettings;
        game.VisualHud = game.VisualHud == VisualHudMode.None
            ? VisualHudMode.Performance
            : VisualHudMode.None;
        _app.Settings.Save();
        return ApplyGameSettings(game.VisualHud == VisualHudMode.None
            ? "Perf HUD off"
            : "Perf HUD on");
    }

    private string ApplyGameSettings(string title)
    {
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
