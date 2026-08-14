namespace MetaQuestTrayTool.Models;

public static class HotKeyCatalog
{
    public static string DescribeAction(HotKeyAction action) => action switch
    {
        HotKeyAction.ApplyGlobal => "Apply global defaults",
        HotKeyAction.RestartOvrService => "Restart OVRService",
        HotKeyAction.AswOff => "ASW Off",
        HotKeyAction.AswAuto => "ASW Auto",
        HotKeyAction.AswClock45 => "ASW 45 FPS",
        HotKeyAction.AswCycle => "Cycle ASW (Off → Auto → 45 → 30 → 18)",
        HotKeyAction.SuperSamplingCycle => "Cycle super sampling",
        HotKeyAction.TogglePerfHud => "Toggle Performance HUD",
        HotKeyAction.VoicePushToTalk => "Voice push-to-talk (listen once)",
        HotKeyAction.OpenMetaLink => "Open Meta Horizon Link",
        HotKeyAction.OpenDebugTool => "Open Oculus Debug Tool",
        HotKeyAction.DashToSteamVr => "Start SteamVR over Link (PreventDashLaunch)",
        HotKeyAction.OpenSteamVrHome => "Open SteamVR Home",
        HotKeyAction.RecoverPcvr => "Recover PCVR",
        HotKeyAction.RestoreDesktopAudio => "Restore desktop audio",
        HotKeyAction.SwitchToVrAudio => "Switch to VR audio",
        HotKeyAction.OpenXrMeta => "Switch OpenXR to Meta",
        HotKeyAction.OpenXrSteamVr => "Switch OpenXR to SteamVR",
        HotKeyAction.CloseOverlays => "Close configured overlays",
        HotKeyAction.ApplyGpuPresets => "Apply GPU recommended presets",
        _ => action.ToString()
    };

    public static string DescribeKey(string keyName)
    {
        if (!Enum.TryParse<System.Windows.Input.Key>(keyName, ignoreCase: true, out var key))
        {
            return keyName;
        }

        return key switch
        {
            System.Windows.Input.Key.NumPad0 => "Num 0",
            System.Windows.Input.Key.NumPad1 => "Num 1",
            System.Windows.Input.Key.NumPad2 => "Num 2",
            System.Windows.Input.Key.NumPad3 => "Num 3",
            System.Windows.Input.Key.NumPad4 => "Num 4",
            System.Windows.Input.Key.NumPad5 => "Num 5",
            System.Windows.Input.Key.NumPad6 => "Num 6",
            System.Windows.Input.Key.NumPad7 => "Num 7",
            System.Windows.Input.Key.NumPad8 => "Num 8",
            System.Windows.Input.Key.NumPad9 => "Num 9",
            System.Windows.Input.Key.OemPlus => "+",
            System.Windows.Input.Key.OemMinus => "-",
            System.Windows.Input.Key.OemComma => ",",
            System.Windows.Input.Key.OemPeriod => ".",
            _ => key.ToString()
        };
    }

    public static IReadOnlyList<HotKeyAction> AllActions { get; } =
        Enum.GetValues<HotKeyAction>()
            .Where(action => action != HotKeyAction.VoicePushToTalk)
            .ToList();
}
