using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views.Pages;

public partial class GameSettingsPage : System.Windows.Controls.UserControl, IShellPage
{
    public GameSettingsPage()
    {
        InitializeComponent();

        foreach (var value in GameSettings.SuperSamplingPresets)
        {
            SuperSamplingBox.Items.Add(new ComboBoxItem
            {
                Content = value <= 0 ? "Off (no override)" : value.ToString("0.0"),
                Tag = value
            });
        }

        foreach (AswMode mode in Enum.GetValues<AswMode>())
        {
            AswBox.Items.Add(new ComboBoxItem { Content = FormatAsw(mode), Tag = mode });
        }

        AdaptiveGpuBox.Items.Add(new ComboBoxItem { Content = "On", Tag = true });
        AdaptiveGpuBox.Items.Add(new ComboBoxItem { Content = "Off", Tag = false });

        foreach (var priority in new[] { "Idle", "BelowNormal", "Normal", "AboveNormal", "High", "Realtime" })
        {
            PriorityBox.Items.Add(new ComboBoxItem { Content = priority, Tag = priority });
        }

        ForceMipBox.Items.Add(new ComboBoxItem { Content = "False", Tag = false });
        ForceMipBox.Items.Add(new ComboBoxItem { Content = "True", Tag = true });

        OffsetMipBox.Items.Add(new ComboBoxItem { Content = "False", Tag = 0.0 });
        OffsetMipBox.Items.Add(new ComboBoxItem { Content = "True", Tag = 1.0 });

        FovStencilBox.Items.Add(new ComboBoxItem { Content = "On", Tag = true });
        FovStencilBox.Items.Add(new ComboBoxItem { Content = "Off", Tag = false });

        foreach (VisualHudMode hud in Enum.GetValues<VisualHudMode>())
        {
            HudBox.Items.Add(new ComboBoxItem { Content = FormatHud(hud), Tag = hud });
        }

        OpenXrBox.Items.Add(new ComboBoxItem { Content = "Meta / Oculus", Tag = OpenXrRuntimeKind.Meta });
        OpenXrBox.Items.Add(new ComboBoxItem { Content = "SteamVR", Tag = OpenXrRuntimeKind.SteamVr });

        SuperSamplingBox.SelectionChanged += (_, _) => PersistIfReady();
        AswBox.SelectionChanged += (_, _) => PersistIfReady();
        AdaptiveGpuBox.SelectionChanged += (_, _) => PersistIfReady();
        PriorityBox.SelectionChanged += (_, _) => PersistIfReady();
        ForceMipBox.SelectionChanged += (_, _) => PersistIfReady();
        OffsetMipBox.SelectionChanged += (_, _) => PersistIfReady();
        FovStencilBox.SelectionChanged += (_, _) => PersistIfReady();
        HudBox.SelectionChanged += (_, _) => PersistIfReady();
        OpenXrBox.SelectionChanged += (_, _) => PersistOpenXr();
        OpenXrOnStartBox.Checked += (_, _) => PersistOpenXr();
        OpenXrOnStartBox.Unchecked += (_, _) => PersistOpenXr();
        ApplyOnStartBox.Checked += (_, _) => PersistFlags();
        ApplyOnStartBox.Unchecked += (_, _) => PersistFlags();
        AutoApplyBox.Checked += (_, _) => PersistFlags();
        AutoApplyBox.Unchecked += (_, _) => PersistFlags();
    }

    public void Refresh()
    {
        var settings = App.Instance.Settings.Current;
        var game = settings.DefaultGameSettings;
        SelectByTag(SuperSamplingBox, game.SuperSampling);
        SelectByTag(AswBox, game.AswMode);
        SelectByTag(AdaptiveGpuBox, game.AdaptiveGpuScaling);
        SelectByTag(PriorityBox, game.OvrServerPriority);
        SelectByTag(ForceMipBox, game.ForceMipMapOnLayers);
        SelectByTag(OffsetMipBox, Math.Abs(game.OffsetMipMapOnLayers) > 0.001 ? 1.0 : 0.0);
        SelectByTag(FovStencilBox, game.UseFovStencil);
        SelectByTag(HudBox, game.VisualHud);
        FovHBox.Text = game.FovMultiplierHorizontal.ToString("0.00", CultureInfo.InvariantCulture);
        FovVBox.Text = game.FovMultiplierVertical.ToString("0.00", CultureInfo.InvariantCulture);
        ApplyOnStartBox.IsChecked = settings.ApplyGameSettingsOnStart;
        AutoApplyBox.IsChecked = settings.AutoApplyProfiles;
        SelectByTag(OpenXrBox, settings.OpenXr.PreferredRuntime == OpenXrRuntimeKind.Inherit
            ? OpenXrRuntimeKind.Meta
            : settings.OpenXr.PreferredRuntime);
        OpenXrOnStartBox.IsChecked = settings.OpenXr.ApplyOnStart;
        OpenXrStatusText.Text = App.Instance.OpenXr.Describe()
            + "  Writes HKLM\\SOFTWARE\\Khronos\\OpenXR\\1\\ActiveRuntime (may prompt for Administrator).";
        CliCommandsBox.Text = settings.CustomCommands.ToCliText();
        AdbCommandsBox.Text = settings.CustomCommands.ToAdbText();
        ApplySessionCapabilities();
        StatusText.Text = BuildStatus();
    }

    private void ApplySessionCapabilities()
    {
        var caps = App.Instance.LinkConnection.GetCapabilities();
        MetaOdtPanel.IsEnabled = caps.AllowsOculusDebugTool;
        CliCommandsLabel.IsEnabled = caps.AllowsOculusDebugTool;
        CliCommandsHint.IsEnabled = caps.AllowsOculusDebugTool;
        CliCommandsBox.IsEnabled = caps.AllowsOculusDebugTool;
        // Apply now still runs ADB when ODT is gated.
        ApplyNowButton.IsEnabled = true;

        if (string.IsNullOrWhiteSpace(caps.Banner) || !caps.RestrictsMetaPipeline)
        {
            SessionBanner.Visibility = Visibility.Collapsed;
            SessionBanner.Text = string.Empty;
        }
        else
        {
            SessionBanner.Visibility = Visibility.Visible;
            SessionBanner.Text = caps.Banner;
        }
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProfilesWindow { Owner = Window.GetWindow(this) };
        window.Show();
    }

    private void GlobalDefaults_Click(object sender, RoutedEventArgs e)
    {
        var window = new GlobalDefaultsWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            Refresh();
        }
    }

    private void SaveFov_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteFov())
        {
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved FOV multipliers.");
        StatusText.Text = "FOV saved.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteAll())
        {
            return;
        }

        App.Instance.Settings.Save();
        App.Instance.Log.Info("Saved game setting defaults.");
        StatusText.Text = "Defaults saved.";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryWriteAll())
        {
            return;
        }

        App.Instance.Settings.Save();
        var summary = App.Instance.ApplyGlobalGameSettings();
        App.Instance.Log.Info(summary);
        StatusText.Text = summary;
    }

    private void CustomCommands_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var commands = App.Instance.Settings.Current.CustomCommands;
        commands.SetCliFromText(CliCommandsBox.Text);
        commands.SetAdbFromText(AdbCommandsBox.Text);
        App.Instance.Settings.Save();
    }

    private void PersistIfReady()
    {
        if (!IsLoaded)
        {
            return;
        }

        TryWriteAll(showErrors: false);
        App.Instance.Settings.Save();
    }

    private void SwitchOpenXr_Click(object sender, RoutedEventArgs e)
    {
        PersistOpenXr();
        if (OpenXrBox.SelectedItem is not ComboBoxItem { Tag: OpenXrRuntimeKind kind }
            || kind is OpenXrRuntimeKind.Inherit)
        {
            return;
        }

        var result = App.Instance.OpenXr.Set(kind);
        App.Instance.Log.Info(result);
        OpenXrStatusText.Text = App.Instance.OpenXr.Describe() + "  " + result;
        System.Windows.MessageBox.Show(Window.GetWindow(this), result, App.AppName);
    }

    private void PersistOpenXr()
    {
        if (!IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current.OpenXr;
        if (OpenXrBox.SelectedItem is ComboBoxItem { Tag: OpenXrRuntimeKind kind }
            && kind is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            settings.PreferredRuntime = kind;
        }

        settings.ApplyOnStart = OpenXrOnStartBox.IsChecked == true;
        App.Instance.Settings.Save();
    }

    private void PersistFlags()
    {
        if (!IsLoaded)
        {
            return;
        }

        var settings = App.Instance.Settings.Current;
        settings.ApplyGameSettingsOnStart = ApplyOnStartBox.IsChecked == true;
        settings.AutoApplyProfiles = AutoApplyBox.IsChecked == true;
        App.Instance.Settings.Save();
    }

    private bool TryWriteAll(bool showErrors = true)
    {
        if (!TryWriteFov(showErrors))
        {
            return false;
        }

        var game = App.Instance.Settings.Current.DefaultGameSettings;
        if (SuperSamplingBox.SelectedItem is ComboBoxItem { Tag: double ss })
        {
            game.SuperSampling = ss;
        }

        if (AswBox.SelectedItem is ComboBoxItem { Tag: AswMode asw })
        {
            game.AswMode = asw;
        }

        if (AdaptiveGpuBox.SelectedItem is ComboBoxItem { Tag: bool adaptive })
        {
            game.AdaptiveGpuScaling = adaptive;
        }

        if (PriorityBox.SelectedItem is ComboBoxItem { Tag: string priority })
        {
            game.OvrServerPriority = priority;
        }

        if (ForceMipBox.SelectedItem is ComboBoxItem { Tag: bool forceMip })
        {
            game.ForceMipMapOnLayers = forceMip;
        }

        if (OffsetMipBox.SelectedItem is ComboBoxItem { Tag: double offset })
        {
            game.OffsetMipMapOnLayers = offset;
        }

        if (FovStencilBox.SelectedItem is ComboBoxItem { Tag: bool stencil })
        {
            game.UseFovStencil = stencil;
        }

        if (HudBox.SelectedItem is ComboBoxItem { Tag: VisualHudMode hud })
        {
            game.VisualHud = hud;
        }

        PersistFlags();
        var commands = App.Instance.Settings.Current.CustomCommands;
        commands.SetCliFromText(CliCommandsBox.Text);
        commands.SetAdbFromText(AdbCommandsBox.Text);
        return true;
    }

    private bool TryWriteFov(bool showErrors = true)
    {
        if (!double.TryParse(FovHBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || !double.TryParse(FovVBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            || h < 0.5 || h > 1.5 || v < 0.5 || v > 1.5)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(
                    Window.GetWindow(this),
                    "FOV multipliers must be between 0.50 and 1.50.",
                    App.AppName);
            }

            return false;
        }

        var game = App.Instance.Settings.Current.DefaultGameSettings;
        game.FovMultiplierHorizontal = h;
        game.FovMultiplierVertical = v;
        return true;
    }

    private static string FormatAsw(AswMode mode) => mode switch
    {
        AswMode.Inherit => "Inherit",
        AswMode.Off => "Off",
        AswMode.Auto => "Auto",
        AswMode.Clock45 => "45 FPS",
        AswMode.Clock30 => "30 FPS",
        AswMode.Clock18 => "18 FPS",
        _ => mode.ToString()
    };

    private static string FormatHud(VisualHudMode mode) => mode switch
    {
        VisualHudMode.None => "None",
        VisualHudMode.Performance => "Performance",
        VisualHudMode.AppRenderTiming => "App render timing",
        VisualHudMode.CompositorTiming => "Compositor timing",
        VisualHudMode.PerformanceHeadroom => "Performance headroom",
        VisualHudMode.Version => "Version",
        VisualHudMode.AsynchronousSpacewarp => "ASW",
        _ => mode.ToString()
    };

    private static string BuildStatus()
    {
        var debug = App.Instance.DebugTool;
        if (!debug.IsAvailable)
        {
            return "OculusDebugToolCLI not found. Install Meta Quest PC software.";
        }

        var headsets = debug.LastHeadsetSerials;
        var headsetText = headsets.Count == 0
            ? "No headset serial cached yet (probed at startup when OVRService is up)."
            : "Headset serials: " + string.Join(", ", headsets);
        var aswText = string.IsNullOrWhiteSpace(debug.LastAswMode) ? "ASW mode unknown" : $"Live ASW: {debug.LastAswMode}";
        return $"{headsetText}  ·  {aswText}  ·  CLI: {debug.CliPath}";
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object? tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag)
                || (item.Tag is double a && tag is double b && Math.Abs(a - b) < 0.001)
                || (item.Tag is string s && tag is string t && string.Equals(s, t, StringComparison.OrdinalIgnoreCase)))
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }
}
