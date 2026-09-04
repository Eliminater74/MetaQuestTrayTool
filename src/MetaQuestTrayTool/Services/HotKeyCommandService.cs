using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Executes tray actions shared by HotKeys and voice commands.
/// </summary>
public sealed class HotKeyCommandService
{
    private readonly App _app;

    public HotKeyCommandService(App app)
    {
        _app = app;
    }

    public string Execute(HotKeyAction action, HotKeyCommandSource source = HotKeyCommandSource.HotKey)
    {
        var sourceLabel = DescribeSource(source);
        var summary = action switch
        {
            HotKeyAction.ApplyGlobal => ExecuteApplyGlobal(),
            HotKeyAction.RestartOvrService => ExecuteRestartOvr(source),
            HotKeyAction.AswOff => SetAsw(AswMode.Off),
            HotKeyAction.AswAuto => SetAsw(AswMode.Auto),
            HotKeyAction.AswClock45 => SetAsw(AswMode.Clock45),
            HotKeyAction.AswCycle => CycleAsw(),
            HotKeyAction.SuperSamplingCycle => CycleSuperSampling(),
            HotKeyAction.TogglePerfHud => CyclePerfHud(),
            HotKeyAction.VoicePushToTalk => ExecuteVoicePushToTalk(),
            HotKeyAction.OpenMetaLink => _app.Oculus.ShowMetaHorizonLink(),
            HotKeyAction.OpenDebugTool => _app.Oculus.ShowOculusDebugTool(),
            HotKeyAction.DashToSteamVr => _app.DashToSteamVr.RunNow(sourceLabel),
            HotKeyAction.StartSteamVr => _app.DashToSteamVr.StartSteamVrNow(sourceLabel),
            HotKeyAction.OpenSteamVrHome => _app.SteamVrInstall.OpenSteamVrHome(),
            HotKeyAction.RecoverPcvr => _app.SessionRecover.Recover(sourceLabel),
            HotKeyAction.RestoreDesktopAudio => ExecuteRestoreDesktopAudio(),
            HotKeyAction.SwitchToVrAudio => ExecuteSwitchToVrAudio(),
            HotKeyAction.OpenXrMeta => SwitchOpenXr(OpenXrRuntimeKind.Meta),
            HotKeyAction.OpenXrSteamVr => SwitchOpenXr(OpenXrRuntimeKind.SteamVr),
            HotKeyAction.CloseOverlays => ExecuteCloseOverlays(sourceLabel),
            HotKeyAction.ApplyGpuPresets => ExecuteApplyGpuPresets(),
            HotKeyAction.TakeScreenshot => ExecuteTakeScreenshot(sourceLabel),
            HotKeyAction.TakeQuestLinkMirrorScreenshot => ExecuteTakeQuestLinkMirrorScreenshot(sourceLabel),
            HotKeyAction.TakeHeadsetScreenshot => ExecuteTakeHeadsetScreenshot(sourceLabel),
            HotKeyAction.ExitApp => ExecuteExitApp(),
            _ => $"Unknown hotkey action: {action}"
        };

        if (action is not HotKeyAction.VoicePushToTalk
            and not HotKeyAction.DashToSteamVr
            and not HotKeyAction.StartSteamVr
            and not HotKeyAction.RecoverPcvr
            and not HotKeyAction.TakeScreenshot
            and not HotKeyAction.TakeQuestLinkMirrorScreenshot
            and not HotKeyAction.TakeHeadsetScreenshot
            and not HotKeyAction.ExitApp)
        {
            AnnounceCommandResult(source, HotKeyCatalog.DescribeAction(action), summary);
        }

        return summary;
    }

    private string ExecuteExitApp()
    {
        _app.Dispatcher.BeginInvoke(new Action(() => _app.Shutdown()));
        return "Exiting Meta Quest Tray Tool.";
    }

    private string ExecuteTakeScreenshot(string sourceLabel)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var summary = _app.CaptureScreenshot(sourceLabel);
                _app.Dispatcher.BeginInvoke(() =>
                    _app.TrayNotify("Screenshot", Truncate(summary)));
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                {
                    var summary = "Screenshot failed: " + ex.Message;
                    _app.Log.Warn(summary);
                    _app.HeadsetAnnouncer.AnnounceScreenshotFailed();
                    _app.TrayNotify("Screenshot", Truncate(ex.Message));
                });
            }
        });

        return "Taking screenshot…";
    }

    private string ExecuteTakeQuestLinkMirrorScreenshot(string sourceLabel)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var summary = _app.CaptureQuestLinkMirrorScreenshot(sourceLabel);
                _app.Dispatcher.BeginInvoke(() =>
                    _app.TrayNotify("Screenshot", Truncate(summary)));
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                {
                    var summary = "Quest Link mirror screenshot failed: " + ex.Message;
                    _app.Log.Warn(summary);
                    _app.HeadsetAnnouncer.AnnounceScreenshotFailed();
                    _app.TrayNotify("Screenshot", Truncate(ex.Message));
                });
            }
        });

        return "Taking Quest Link mirror screenshot…";
    }

    private string ExecuteTakeHeadsetScreenshot(string sourceLabel)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var summary = _app.CaptureHeadsetScreenshot(sourceLabel);
                _app.Dispatcher.BeginInvoke(() =>
                    _app.TrayNotify("Screenshot", Truncate(summary)));
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                {
                    var summary = "Headset screenshot failed: " + ex.Message;
                    _app.Log.Warn(summary);
                    _app.HeadsetAnnouncer.AnnounceScreenshotFailed();
                    _app.TrayNotify("Screenshot", Truncate(ex.Message));
                });
            }
        });

        return "Taking headset screenshot…";
    }

    private string ExecuteRestoreDesktopAudio()
    {
        var result = _app.Audio.RestoreFallbackDevices(_app.Settings.Current.Audio);
        _app.Log.Info(result);
        return result;
    }

    private string ExecuteSwitchToVrAudio()
    {
        var result = _app.Audio.ApplyVrDevices(_app.Settings.Current.Audio);
        _app.Log.Info(result);
        return result;
    }

    private string SwitchOpenXr(OpenXrRuntimeKind kind)
    {
        _app.Settings.Current.OpenXr.PreferredRuntime = kind;
        _app.Settings.Save();
        var result = _app.OpenXr.Set(kind);
        _app.Log.Info(result);
        return result;
    }

    private string ExecuteCloseOverlays(string sourceLabel)
    {
        var summary = _app.OverlayClose.CloseConfiguredOverlays(sourceLabel, force: true);
        if (string.IsNullOrWhiteSpace(summary))
        {
            var count = (_app.Settings.Current.OverlayCloseProcesses ?? []).Count;
            return count == 0
                ? "No overlay processes configured — add names under Game Settings → Close overlays on Link connect."
                : "No matching overlay processes were running.";
        }

        return summary;
    }

    private string ExecuteRestartOvr(HotKeyCommandSource source)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var result = _app.Oculus.Restart();
                _app.Dispatcher.BeginInvoke(() =>
                {
                    _app.Log.Info(result);
                    AnnounceCommandResult(source, "OVRService restart", result);
                });
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                {
                    var result = "OVRService restart failed: " + ex.Message;
                    _app.Log.Warn(result);
                    AnnounceCommandResult(source, "OVRService restart", result);
                });
            }
        });

        return "Restarting OVRService…";
    }

    private string ExecuteApplyGpuPresets()
    {
        var blocked = GuardProfileMutation("GPU preset apply");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        return _app.ApplyGpuRecommendedPresets();
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

    private string CyclePerfHud()
    {
        var blocked = GuardProfileMutation("Performance HUD");
        if (blocked.Length > 0)
        {
            return blocked;
        }

        var sessionBlock = GuardMetaOdt("Performance HUD");
        if (sessionBlock.Length > 0)
        {
            return sessionBlock;
        }

        var values = Enum.GetValues<VisualHudMode>();
        var game = _app.Settings.Current.DefaultGameSettings;
        var index = Array.IndexOf(values, game.VisualHud);
        if (index < 0)
        {
            index = 0;
        }

        game.VisualHud = values[(index + 1) % values.Length];
        _app.Settings.Save();
        var label = game.VisualHud switch
        {
            VisualHudMode.None => "Off",
            VisualHudMode.Performance => "Performance",
            VisualHudMode.AppRenderTiming => "App render timing",
            VisualHudMode.CompositorTiming => "Compositor timing",
            VisualHudMode.PerformanceHeadroom => "Performance headroom",
            VisualHudMode.Version => "Version",
            VisualHudMode.AsynchronousSpacewarp => "ASW",
            _ => game.VisualHud.ToString()
        };
        return ApplyGameSettings($"Perf HUD → {label}");
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

    private void AnnounceCommandResult(HotKeyCommandSource source, string actionName, string? summary)
    {
        if (source == HotKeyCommandSource.VoiceCommand)
        {
            _app.HeadsetAnnouncer.AnnounceVoiceCommandResult(actionName, summary);
            return;
        }

        _app.HeadsetAnnouncer.AnnounceHotKeyResult(actionName, summary);
    }

    private static string DescribeSource(HotKeyCommandSource source) =>
        source == HotKeyCommandSource.VoiceCommand ? "voice" : "hotkey";

    private static string Truncate(string text, int max = 180) =>
        text.Length <= max ? text : text[..max] + "…";

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

public enum HotKeyCommandSource
{
    HotKey,
    VoiceCommand
}
