using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Views;

namespace MetaQuestTrayTool.Tray;

public sealed class TrayIconHost : IDisposable
{
    private readonly App _app;
    private readonly NotifyIcon _notifyIcon;
    private Icon? _icon;
    private MainShellWindow? _shell;
    private AboutWindow? _about;
    private ProfilesWindow? _profiles;
    private LinkSettingsWindow? _linkSettings;
    private AudioSettingsWindow? _audioSettings;
    private PowerSettingsWindow? _powerSettings;

    public TrayIconHost(App app)
    {
        _app = app;
        _notifyIcon = new NotifyIcon
        {
            Visible = false,
            Text = App.AppName
        };
    }

    public void Show()
    {
        _icon = LoadIcon();
        _notifyIcon.Icon = _icon;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        _notifyIcon.Visible = true;
        _notifyIcon.MouseClick += OnMouseClick;
        _notifyIcon.DoubleClick += (_, _) => ShowShell();

        if (!_app.Settings.Current.Tray.StartMinimized)
        {
            ShowShell();
        }

        if (_app.Settings.Current.ShowNotifications)
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                App.AppName,
                "Running in the notification area. Right-click the tray icon for actions.",
                ToolTipIcon.Info);
        }
    }

    public void Notify(string title, string message)
    {
        if (!_app.Settings.Current.ShowNotifications)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ShowShell();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false
        };

        menu.Opening += (_, _) => RefreshDynamicItems(menu);

        menu.Items.Add(new ToolStripMenuItem("Open Settings", null, (_, _) => ShowShell()));
        menu.Items.Add(new ToolStripSeparator());

        var serviceMenu = new ToolStripMenuItem("Oculus Service");
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Start", null, (_, _) => RunServiceAction(_app.Oculus.Start)));
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Stop", null, (_, _) => RunServiceAction(_app.Oculus.Stop)));
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Restart", null, (_, _) => RunServiceAction(_app.Oculus.Restart)));
        serviceMenu.DropDownItems.Add(new ToolStripSeparator());
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Status: Unknown") { Enabled = false, Name = "ServiceStatus" });
        menu.Items.Add(serviceMenu);

        menu.Items.Add(BuildGameSettingsMenu());

        menu.Items.Add(BuildProfilesMenu());
        menu.Items.Add(BuildLinkMenu());
        menu.Items.Add(BuildAudioMenu());
        menu.Items.Add(BuildPowerMenu());
        menu.Items.Add(new ToolStripSeparator());

        var startWithWindows = new ToolStripMenuItem("Start with Windows")
        {
            Name = "StartWithWindows",
            CheckOnClick = true,
            Checked = _app.Settings.Current.StartWithWindows
        };
        startWithWindows.CheckedChanged += (_, _) => ToggleStartWithWindows(startWithWindows.Checked);
        menu.Items.Add(startWithWindows);

        var notifications = new ToolStripMenuItem("Show notifications")
        {
            Name = "ShowNotifications",
            CheckOnClick = true,
            Checked = _app.Settings.Current.ShowNotifications
        };
        notifications.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.ShowNotifications = notifications.Checked;
            _app.Settings.Save();
        };
        menu.Items.Add(notifications);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("About", null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) =>
        {
            _shell?.ForceClose();
            _app.Shutdown();
        }));

        return menu;
    }

    private void RefreshDynamicItems(ContextMenuStrip menu)
    {
        _app.Oculus.Refresh();

        if (FindItem(menu.Items, "ServiceStatus") is ToolStripMenuItem status)
        {
            status.Text = $"Status: {_app.Oculus.ServiceStatus}";
        }

        if (FindItem(menu.Items, "StartWithWindows") is ToolStripMenuItem startup)
        {
            startup.Checked = _app.Settings.Current.StartWithWindows;
        }

        if (FindItem(menu.Items, "ShowNotifications") is ToolStripMenuItem notifications)
        {
            notifications.Checked = _app.Settings.Current.ShowNotifications;
        }

        if (FindItem(menu.Items, "ApplyOnStart") is ToolStripMenuItem applyOnStart)
        {
            applyOnStart.Checked = _app.Settings.Current.ApplyGameSettingsOnStart;
        }

        if (FindItem(menu.Items, "AutoApplyProfiles") is ToolStripMenuItem autoApply)
        {
            autoApply.Checked = _app.Settings.Current.AutoApplyProfiles;
        }

        SyncGameSettingChecks(menu);
        RebuildProfileItems(menu);
        SyncLinkChecks(menu);
        SyncAudioChecks(menu);
        SyncPowerChecks(menu);
        _notifyIcon.Text = $"{App.AppName}\nOVRService: {_app.Oculus.ServiceStatus}";
    }

    private ToolStripMenuItem BuildGameSettingsMenu()
    {
        var gameSettings = new ToolStripMenuItem("Game Settings");

        var ssMenu = new ToolStripMenuItem("Super Sampling") { Name = "SuperSamplingMenu" };
        foreach (var value in GameSettings.SuperSamplingPresets)
        {
            var label = FormatSuperSampling(value);
            var item = new ToolStripMenuItem(label)
            {
                Name = SuperSamplingItemName(value),
                Tag = value
            };
            var captured = value;
            item.Click += (_, _) =>
            {
                _app.Settings.Current.DefaultGameSettings.SuperSampling = captured;
                ApplyGameSettings();
            };
            ssMenu.DropDownItems.Add(item);
        }

        var aswMenu = new ToolStripMenuItem("ASW Mode") { Name = "AswModeMenu" };
        foreach (AswMode mode in Enum.GetValues<AswMode>())
        {
            var item = new ToolStripMenuItem(FormatAswMode(mode))
            {
                Name = AswItemName(mode),
                Tag = mode
            };
            var captured = mode;
            item.Click += (_, _) =>
            {
                _app.Settings.Current.DefaultGameSettings.AswMode = captured;
                ApplyGameSettings();
            };
            aswMenu.DropDownItems.Add(item);
        }

        var applyOnStart = new ToolStripMenuItem("Apply defaults when the app starts")
        {
            Name = "ApplyOnStart",
            CheckOnClick = true,
            Checked = _app.Settings.Current.ApplyGameSettingsOnStart
        };
        applyOnStart.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.ApplyGameSettingsOnStart = applyOnStart.Checked;
            _app.Settings.Save();
        };

        var autoApply = new ToolStripMenuItem("Auto-apply when a game launches")
        {
            Name = "AutoApplyProfiles",
            CheckOnClick = true,
            Checked = _app.Settings.Current.AutoApplyProfiles
        };
        autoApply.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.AutoApplyProfiles = autoApply.Checked;
            _app.Settings.Save();
            _app.Log.Info(autoApply.Checked
                ? "Profile auto-apply enabled."
                : "Profile auto-apply disabled.");
        };

        gameSettings.DropDownItems.Add(ssMenu);
        gameSettings.DropDownItems.Add(aswMenu);
        gameSettings.DropDownItems.Add(PlaceholderItem("CPU Priority"));
        gameSettings.DropDownItems.Add(new ToolStripSeparator());
        gameSettings.DropDownItems.Add(new ToolStripMenuItem("Apply now", null, (_, _) => ApplyGameSettings()));
        gameSettings.DropDownItems.Add(applyOnStart);
        gameSettings.DropDownItems.Add(autoApply);
        return gameSettings;
    }

    private void SyncGameSettingChecks(ContextMenuStrip menu)
    {
        var current = _app.Settings.Current.DefaultGameSettings;

        if (FindItem(menu.Items, "SuperSamplingMenu") is ToolStripMenuItem ssMenu)
        {
            foreach (ToolStripItem item in ssMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is double value)
                {
                    menuItem.Checked = Math.Abs(value - current.SuperSampling) < 0.001;
                }
            }
        }

        if (FindItem(menu.Items, "AswModeMenu") is ToolStripMenuItem aswMenu)
        {
            foreach (ToolStripItem item in aswMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is AswMode mode)
                {
                    menuItem.Checked = mode == current.AswMode;
                }
            }
        }
    }

    private void ApplyGameSettings()
    {
        _app.Settings.Save();
        var result = _app.DebugTool.Apply(_app.Settings.Current.DefaultGameSettings);

        if (!result.CliFound || !result.Started)
        {
            _app.Log.Error(result.Summary);
        }
        else if (result.LooksRejected)
        {
            _app.Log.Warn(result.Summary);
        }
        else
        {
            _app.Log.Info(result.Summary);
        }

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            _app.Log.Info("ODT: " + Truncate(result.Output));
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            _app.Log.Warn("ODT error: " + Truncate(result.Error));
        }

        Notify("Game Settings", result.Summary);
        _shell?.RefreshActivePage();
        _profiles?.Reload();
    }

    private ToolStripMenuItem BuildProfilesMenu()
    {
        var menu = new ToolStripMenuItem("Profiles") { Name = "ProfilesMenu" };
        menu.DropDownItems.Add(new ToolStripMenuItem("Manage profiles…", null, (_, _) => ShowProfiles()));
        menu.DropDownItems.Add(new ToolStripSeparator { Name = "ProfilesSeparator" });
        return menu;
    }

    private void RebuildProfileItems(ContextMenuStrip root)
    {
        if (FindItem(root.Items, "ProfilesMenu") is not ToolStripMenuItem menu)
        {
            return;
        }

        for (var i = menu.DropDownItems.Count - 1; i >= 0; i--)
        {
            if (menu.DropDownItems[i].Name?.StartsWith("Profile_", StringComparison.Ordinal) == true)
            {
                menu.DropDownItems.RemoveAt(i);
            }
        }

        foreach (var profile in _app.Profiles.All.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var captured = profile;
            menu.DropDownItems.Add(new ToolStripMenuItem($"{profile.Name}  ({profile.ProcessName})", null, (_, _) =>
            {
                var summary = _app.ApplyProfile(captured);
                _app.Log.Info($"Applied profile '{captured.Name}': {summary}");
                Notify("Profile", summary);
                _shell?.RefreshActivePage();
            })
            {
                Name = "Profile_" + profile.Name.Replace(' ', '_')
            });
        }

        if (_app.Profiles.All.Count == 0)
        {
            menu.DropDownItems.Add(new ToolStripMenuItem("No profiles yet")
            {
                Name = "Profile_Empty",
                Enabled = false
            });
        }
    }

    private void ShowProfiles()
    {
        if (_profiles is null || !_profiles.IsLoaded)
        {
            _profiles = new ProfilesWindow();
            _profiles.Closed += (_, _) => _profiles = null;
        }

        _profiles.Show();
        _profiles.Activate();
        _profiles.WindowState = WindowState.Normal;
    }

    private ToolStripMenuItem BuildLinkMenu()
    {
        var menu = new ToolStripMenuItem("Quest Link / Air Link") { Name = "LinkMenu" };
        menu.DropDownItems.Add(new ToolStripMenuItem("Open Link settings…", null, (_, _) => ShowLinkSettings()));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var bitrateMenu = new ToolStripMenuItem("Bitrate") { Name = "LinkBitrateMenu" };
        foreach (var bitrate in LinkSettings.BitratePresets)
        {
            var captured = bitrate;
            bitrateMenu.DropDownItems.Add(new ToolStripMenuItem(
                bitrate <= 0 ? "Default" : $"{bitrate} Mbps",
                null,
                (_, _) =>
                {
                    _app.Settings.Current.LinkSettings.BitrateMbps = captured;
                    ApplyLinkSettings();
                })
            {
                Name = $"LinkBitrate_{bitrate}",
                Tag = bitrate
            });
        }

        var encodeMenu = new ToolStripMenuItem("Encode width") { Name = "LinkEncodeMenu" };
        foreach (var width in LinkSettings.EncodeWidthPresets)
        {
            var captured = width;
            encodeMenu.DropDownItems.Add(new ToolStripMenuItem(
                width <= 0 ? "Auto / default" : width.ToString(),
                null,
                (_, _) =>
                {
                    _app.Settings.Current.LinkSettings.EncodeResolutionWidth = captured;
                    ApplyLinkSettings();
                })
            {
                Name = $"LinkEncode_{width}",
                Tag = width
            });
        }

        var hevc = new ToolStripMenuItem("Prefer HEVC")
        {
            Name = "LinkHevc",
            CheckOnClick = true,
            Checked = _app.Settings.Current.LinkSettings.PreferHevc
        };
        hevc.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.LinkSettings.PreferHevc = hevc.Checked;
            ApplyLinkSettings();
        };

        var slices = new ToolStripMenuItem("Disable sliced encoding")
        {
            Name = "LinkSlices",
            CheckOnClick = true,
            Checked = _app.Settings.Current.LinkSettings.DisableSlicedEncoding
        };
        slices.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.LinkSettings.DisableSlicedEncoding = slices.Checked;
            ApplyLinkSettings();
        };

        var applyOnStart = new ToolStripMenuItem("Apply on app start")
        {
            Name = "LinkApplyOnStart",
            CheckOnClick = true,
            Checked = _app.Settings.Current.ApplyLinkSettingsOnStart
        };
        applyOnStart.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.ApplyLinkSettingsOnStart = applyOnStart.Checked;
            _app.Settings.Save();
        };

        menu.DropDownItems.Add(bitrateMenu);
        menu.DropDownItems.Add(encodeMenu);
        menu.DropDownItems.Add(hevc);
        menu.DropDownItems.Add(slices);
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add(new ToolStripMenuItem("Apply now", null, (_, _) => ApplyLinkSettings()));
        menu.DropDownItems.Add(new ToolStripMenuItem("Apply + restart OVRService", null, (_, _) =>
        {
            ApplyLinkSettings();
            RunServiceAction(_app.Oculus.Restart);
        }));
        menu.DropDownItems.Add(applyOnStart);
        return menu;
    }

    private void SyncLinkChecks(ContextMenuStrip root)
    {
        var link = _app.Settings.Current.LinkSettings;

        if (FindItem(root.Items, "LinkBitrateMenu") is ToolStripMenuItem bitrateMenu)
        {
            foreach (ToolStripItem item in bitrateMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is int bitrate)
                {
                    menuItem.Checked = bitrate == link.BitrateMbps;
                }
            }
        }

        if (FindItem(root.Items, "LinkEncodeMenu") is ToolStripMenuItem encodeMenu)
        {
            foreach (ToolStripItem item in encodeMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is int width)
                {
                    menuItem.Checked = width == link.EncodeResolutionWidth;
                }
            }
        }

        if (FindItem(root.Items, "LinkHevc") is ToolStripMenuItem hevc)
        {
            hevc.Checked = link.PreferHevc;
        }

        if (FindItem(root.Items, "LinkSlices") is ToolStripMenuItem slices)
        {
            slices.Checked = link.DisableSlicedEncoding;
        }

        if (FindItem(root.Items, "LinkApplyOnStart") is ToolStripMenuItem applyOnStart)
        {
            applyOnStart.Checked = _app.Settings.Current.ApplyLinkSettingsOnStart;
        }
    }

    private void ApplyLinkSettings()
    {
        _app.Settings.Save();
        var result = _app.Link.Apply(_app.Settings.Current.LinkSettings, deleteUnsetOverrides: true);
        if (result.Succeeded)
        {
            _app.Log.Info(result.Summary);
        }
        else
        {
            _app.Log.Error(result.Summary);
        }

        Notify("Quest Link", result.Summary);
        _shell?.RefreshActivePage();
    }

    private void ShowLinkSettings()
    {
        if (_linkSettings is null || !_linkSettings.IsLoaded)
        {
            _linkSettings = new LinkSettingsWindow();
            _linkSettings.Closed += (_, _) => _linkSettings = null;
        }

        _linkSettings.Show();
        _linkSettings.Activate();
        _linkSettings.WindowState = WindowState.Normal;
    }

    private ToolStripMenuItem BuildAudioMenu()
    {
        var menu = new ToolStripMenuItem("Audio Switching") { Name = "AudioMenu" };
        menu.DropDownItems.Add(new ToolStripMenuItem("Open audio settings…", null, (_, _) => ShowAudioSettings()));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var auto = new ToolStripMenuItem("Auto-switch on Link active/inactive")
        {
            Name = "AudioAutoSwitch",
            CheckOnClick = true,
            Checked = _app.Settings.Current.Audio.AutoSwitchEnabled
        };
        auto.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.Audio.AutoSwitchEnabled = auto.Checked;
            _app.Settings.Save();
            _app.Log.Info(auto.Checked ? "Audio auto-switch enabled." : "Audio auto-switch disabled.");
        };

        menu.DropDownItems.Add(auto);
        menu.DropDownItems.Add(new ToolStripMenuItem("Switch to VR devices now", null, (_, _) =>
        {
            var result = _app.Audio.ApplyVrDevices(_app.Settings.Current.Audio);
            _app.Log.Info(result);
            Notify("Audio", result);
        }));
        menu.DropDownItems.Add(new ToolStripMenuItem("Restore fallback devices", null, (_, _) =>
        {
            var result = _app.Audio.RestoreFallbackDevices(_app.Settings.Current.Audio);
            _app.Log.Info(result);
            Notify("Audio", result);
        }));
        menu.DropDownItems.Add(new ToolStripMenuItem("Capture current defaults as fallback", null, (_, _) =>
        {
            var result = _app.Audio.CaptureCurrentAsFallback(_app.Settings.Current.Audio);
            _app.Settings.Save();
            _app.Log.Info(result);
            Notify("Audio", result);
        }));
        return menu;
    }

    private void SyncAudioChecks(ContextMenuStrip root)
    {
        if (FindItem(root.Items, "AudioAutoSwitch") is ToolStripMenuItem auto)
        {
            auto.Checked = _app.Settings.Current.Audio.AutoSwitchEnabled;
        }
    }

    private void ShowAudioSettings()
    {
        if (_audioSettings is null || !_audioSettings.IsLoaded)
        {
            _audioSettings = new AudioSettingsWindow();
            _audioSettings.Closed += (_, _) => _audioSettings = null;
        }

        _audioSettings.Show();
        _audioSettings.Activate();
        _audioSettings.WindowState = WindowState.Normal;
    }

    private ToolStripMenuItem BuildPowerMenu()
    {
        var menu = new ToolStripMenuItem("Power Plan") { Name = "PowerMenu" };
        menu.DropDownItems.Add(new ToolStripMenuItem("Open power settings…", null, (_, _) => ShowPowerSettings()));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var auto = new ToolStripMenuItem("Auto-switch with Oculus service")
        {
            Name = "PowerAutoSwitch",
            CheckOnClick = true,
            Checked = _app.Settings.Current.Power.AutoSwitchEnabled
        };
        auto.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.Power.AutoSwitchEnabled = auto.Checked;
            _app.Settings.Save();
            _app.Log.Info(auto.Checked ? "Power plan auto-switch enabled." : "Power plan auto-switch disabled.");
        };

        var sleep = new ToolStripMenuItem("Restart OVRService after sleep")
        {
            Name = "PowerRestartAfterSleep",
            CheckOnClick = true,
            Checked = _app.Settings.Current.Power.RestartServiceAfterSleep
        };
        sleep.CheckedChanged += (_, _) =>
        {
            _app.Settings.Current.Power.RestartServiceAfterSleep = sleep.Checked;
            _app.Settings.Save();
        };

        menu.DropDownItems.Add(auto);
        menu.DropDownItems.Add(sleep);
        menu.DropDownItems.Add(new ToolStripMenuItem("Apply VR plan now", null, (_, _) =>
        {
            var result = _app.Power.ApplyVrPlan(_app.Settings.Current.Power);
            _app.Settings.Save();
            _app.Log.Info(result);
            Notify("Power", result);
        }));
        menu.DropDownItems.Add(new ToolStripMenuItem("Restore fallback plan", null, (_, _) =>
        {
            var result = _app.Power.RestoreFallbackPlan(_app.Settings.Current.Power);
            _app.Log.Info(result);
            Notify("Power", result);
        }));
        return menu;
    }

    private void SyncPowerChecks(ContextMenuStrip root)
    {
        if (FindItem(root.Items, "PowerAutoSwitch") is ToolStripMenuItem auto)
        {
            auto.Checked = _app.Settings.Current.Power.AutoSwitchEnabled;
        }

        if (FindItem(root.Items, "PowerRestartAfterSleep") is ToolStripMenuItem sleep)
        {
            sleep.Checked = _app.Settings.Current.Power.RestartServiceAfterSleep;
        }
    }

    private void ShowPowerSettings()
    {
        if (_powerSettings is null || !_powerSettings.IsLoaded)
        {
            _powerSettings = new PowerSettingsWindow();
            _powerSettings.Closed += (_, _) => _powerSettings = null;
        }

        _powerSettings.Show();
        _powerSettings.Activate();
        _powerSettings.WindowState = WindowState.Normal;
    }

    private static string FormatSuperSampling(double value) =>
        value <= 0 ? "Off (no override)" : value.ToString("0.0");

    private static string FormatAswMode(AswMode mode) => mode switch
    {
        AswMode.Inherit => "Inherit (leave unchanged)",
        AswMode.Off => "Off",
        AswMode.Auto => "Auto",
        AswMode.Clock45 => "45 FPS",
        AswMode.Clock30 => "30 FPS",
        AswMode.Clock18 => "18 FPS",
        _ => mode.ToString()
    };

    private static string SuperSamplingItemName(double value) => $"SS_{value:0.0}";
    private static string AswItemName(AswMode mode) => $"ASW_{mode}";

    private static string Truncate(string text, int max = 240) =>
        text.Length <= max ? text : text[..max] + "…";

    private void RunServiceAction(Func<string> action)
    {
        var result = action();
        _app.Log.Info(result);
        Notify("Oculus Service", result);
        _shell?.RefreshActivePage();
    }

    private void ToggleStartWithWindows(bool enabled)
    {
        try
        {
            _app.StartupRegistration.SetEnabled(enabled);
            _app.Settings.Current.StartWithWindows = enabled;
            _app.Settings.Save();
            _app.Log.Info(enabled
                ? "Enabled Start with Windows."
                : "Disabled Start with Windows.");
        }
        catch (Exception ex)
        {
            _app.Log.Error("Could not update Start with Windows.", ex);
            System.Windows.MessageBox.Show(
                $"Could not update the Windows startup entry.\n\n{ex.Message}",
                App.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ShowShell()
    {
        if (_shell is null || !_shell.IsLoaded)
        {
            _shell = new MainShellWindow();
            _shell.Closed += (_, _) => _shell = null;
        }

        _shell.Show();
        _shell.Activate();
        _shell.WindowState = WindowState.Normal;
        _shell.RefreshActivePage();
    }

    private void ShowAbout()
    {
        if (_about is null || !_about.IsLoaded)
        {
            _about = new AboutWindow();
            _about.Closed += (_, _) => _about = null;
        }

        _about.Show();
        _about.Activate();
    }

    private static ToolStripMenuItem PlaceholderItem(string text)
    {
        return new ToolStripMenuItem($"{text}  (coming next)")
        {
            Enabled = false
        };
    }

    private static ToolStripItem? FindItem(ToolStripItemCollection items, string name)
    {
        foreach (ToolStripItem item in items)
        {
            if (item.Name == name)
            {
                return item;
            }

            if (item is ToolStripMenuItem menuItem)
            {
                var nested = FindItem(menuItem.DropDownItems, name);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static Icon LoadIcon()
    {
        var streamInfo = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/Icons/App.ico"));

        if (streamInfo?.Stream is null)
        {
            return SystemIcons.Application;
        }

        using var stream = streamInfo.Stream;
        return new Icon(stream);
    }
}
