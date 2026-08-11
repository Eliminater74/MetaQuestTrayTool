using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using MetaQuestTrayTool.Views;

namespace MetaQuestTrayTool.Tray;

public sealed class TrayIconHost : IDisposable
{
    private readonly App _app;
    private readonly NotifyIcon _notifyIcon;
    private Icon? _icon;
    private DashboardWindow? _dashboard;
    private AboutWindow? _about;

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
        _notifyIcon.DoubleClick += (_, _) => ShowDashboard();

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
            ShowDashboard();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false
        };

        menu.Opening += (_, _) => RefreshDynamicItems(menu);

        menu.Items.Add(new ToolStripMenuItem("Open Dashboard", null, (_, _) => ShowDashboard()));
        menu.Items.Add(new ToolStripSeparator());

        var serviceMenu = new ToolStripMenuItem("Oculus Service");
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Start", null, (_, _) => RunServiceAction(_app.Oculus.Start)));
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Stop", null, (_, _) => RunServiceAction(_app.Oculus.Stop)));
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Restart", null, (_, _) => RunServiceAction(_app.Oculus.Restart)));
        serviceMenu.DropDownItems.Add(new ToolStripSeparator());
        serviceMenu.DropDownItems.Add(new ToolStripMenuItem("Status: Unknown") { Enabled = false, Name = "ServiceStatus" });
        menu.Items.Add(serviceMenu);

        var gameSettings = new ToolStripMenuItem("Game Settings");
        gameSettings.DropDownItems.Add(PlaceholderItem("Super Sampling"));
        gameSettings.DropDownItems.Add(PlaceholderItem("ASW Mode"));
        gameSettings.DropDownItems.Add(PlaceholderItem("CPU Priority"));
        menu.Items.Add(gameSettings);

        menu.Items.Add(PlaceholderItem("Profiles"));
        menu.Items.Add(PlaceholderItem("Quest Link / Air Link"));
        menu.Items.Add(PlaceholderItem("Audio Switching"));
        menu.Items.Add(PlaceholderItem("Power Plan"));
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
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => _app.Shutdown()));

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

        _notifyIcon.Text = $"{App.AppName}\nOVRService: {_app.Oculus.ServiceStatus}";
    }

    private void RunServiceAction(Func<string> action)
    {
        var result = action();
        _app.Log.Info(result);
        Notify("Oculus Service", result);
        _dashboard?.RefreshStatus();
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

    private void ShowDashboard()
    {
        if (_dashboard is null || !_dashboard.IsLoaded)
        {
            _dashboard = new DashboardWindow();
            _dashboard.Closed += (_, _) => _dashboard = null;
        }

        _dashboard.Show();
        _dashboard.Activate();
        _dashboard.WindowState = WindowState.Normal;
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
