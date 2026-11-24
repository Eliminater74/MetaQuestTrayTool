using System.Runtime.InteropServices;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using ListBox = System.Windows.Controls.ListBox;
using Panel = System.Windows.Controls.Panel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tray;

public sealed class NotificationPresenter : IDisposable
{
    private readonly App _app;
    private readonly Action<NoticeAction> _activate;
    private readonly Action<string, string> _balloon;
    private readonly DesktopNotificationCenter _center = new();
    private readonly List<NoticeWindow> _windows = [];
    private bool _disposed;

    public NotificationPresenter(App app, Action<NoticeAction> activate, Action<string, string> balloon)
    {
        _app = app;
        _activate = activate;
        _balloon = balloon;
    }

    public DesktopNotificationCenter Center => _center;

    public void PresentStartup()
    {
        ShowItem(preferences => _center.PresentStartup(preferences, _app.Settings.Current.ShowNotifications, DateTimeOffset.UtcNow), "startup");
    }

    public void Present(string title, string message, bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        var enabled = force || _app.Settings.Current.ShowNotifications;
        ShowItem(preferences => _center.Present(title, message, preferences, enabled, DateTimeOffset.UtcNow), title);
    }

    private void ShowItem(Func<NoticePreferences, NoticeItem?> create, string reason)
    {
        var preferences = NoticePreferencesMapper.From(_app.Settings.Current);
        var item = create(preferences);
        if (item is null)
        {
            return;
        }

        if (!preferences.ModernFlyouts)
        {
            _center.Dismiss(item);
            _app.Log.Info("Modern notifications are off. Windows balloon used for " + reason + ".");
            _balloon(item.Title, item.Message);
            return;
        }

        void Show()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var window = new NoticeWindow(item, preferences, ThemeService.Current, _activate, CloseWindow);
                _windows.Add(window);
                window.Show();
                Restack();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _center.Dismiss(item);
                _app.Log.Warn("Custom notification window failed (" + ex.GetType().Name + "). Windows balloon used for " + reason + ".");
                _balloon(item.Title, item.Message);
            }
        }

        if (_app.Dispatcher.CheckAccess())
        {
            Show();
        }
        else
        {
            _app.Dispatcher.BeginInvoke(Show);
        }
    }

    public void ShowHistory()
    {
        var window = new NoticeHistoryWindow(_center);
        window.Show();
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var window in _windows.ToArray())
        {
            window.Close();
        }

        _windows.Clear();
    }

    private void CloseWindow(NoticeWindow window)
    {
        _center.Dismiss(window.Item);
        _windows.Remove(window);
        window.Close();
        Restack();
    }

    private void Restack()
    {
        var index = 0;
        foreach (var window in _windows)
        {
            window.MoveTo(index);
            index++;
        }
    }
}

public static class NoticePreferencesMapper
{
    public static NoticePreferences From(AppSettings settings)
    {
        var notice = settings.Notices ?? new NoticeSettings();
        return new NoticePreferences
        {
            ModernFlyouts = notice.ModernFlyouts,
            Transparency = notice.Transparency,
            Animations = notice.Animations,
            DurationSeconds = notice.DurationSeconds,
            PlaySound = notice.PlaySound,
            KeepHistory = notice.KeepHistory,
            Startup = notice.Startup,
            Headset = notice.Headset,
            Profile = notice.Profile,
            Performance = notice.Performance,
            Benchmark = notice.Benchmark,
            Update = notice.Update,
            General = notice.General,
            LeftClickOpensQuickControls = notice.LeftClickOpensQuickControls
        };
    }
}

public sealed class NoticeWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly Action<NoticeWindow> _close;
    private readonly bool _animate;

    public NoticeWindow(NoticeItem item, NoticePreferences preferences, AppTheme theme, Action<NoticeAction> activate, Action<NoticeWindow> close)
    {
        Item = item;
        _close = close;
        _animate = preferences.Animations && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        var palette = NoticePalette.For(theme, item.Severity, preferences.Transparency && !SystemParameters.HighContrast);
        Width = 380;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        Focusable = false;

        var root = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Margin = new Thickness(8),
            Background = palette.Background,
            BorderBrush = palette.Accent,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.35,
                Color = Colors.Black
            }
        };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        var accent = new Border { Background = palette.Accent, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) };
        Grid.SetRowSpan(accent, 1);
        layout.Children.Add(accent);
        var body = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(body, 1);
        body.Children.Add(Header(item, palette, close));
        body.Children.Add(new TextBlock
        {
            Text = SeverityLabel(item.Severity) + " · " + item.Category,
            Foreground = palette.Muted,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 8)
        });
        body.Children.Add(new TextBlock
        {
            Text = item.Title,
            Foreground = palette.Text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new TextBlock
        {
            Text = item.Message,
            Foreground = palette.Text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        var actions = item.Actions.Count > 0
            ? item.Actions
            : item.Action == NoticeAction.None ? [] : new[] { item.Action };
        if (actions.Count > 0)
        {
            var buttons = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            foreach (var action in actions)
            {
                var captured = action;
                var button = new Button
                {
                    Content = ActionLabel(captured),
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(14, 7, 14, 7),
                    Background = palette.Accent,
                    Foreground = palette.OnAccent,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold
                };
                button.Click += (_, _) =>
                {
                    activate(captured);
                    close(this);
                };
                buttons.Children.Add(button);
            }

            body.Children.Add(buttons);
        }

        layout.Children.Add(body);
        root.RenderTransform = new TranslateTransform(0, _animate ? 18 : 0);
        root.Child = layout;
        Content = root;
        _timer = new DispatcherTimer { Interval = item.Duration };
        _timer.Tick += (_, _) => close(this);
        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => _timer.Start();
        Loaded += (_, _) =>
        {
            MoveTo(0);
            if (_animate && root.RenderTransform is TranslateTransform translate)
            {
                Opacity = 0;
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(180)));
            }

            _timer.Start();
        };
    }

    public NoticeItem Item { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, style | 0x00000080 | 0x08000000);
    }

    public void MoveTo(int stackIndex)
    {
        var area = SystemParameters.WorkArea;
        var taskbarTop = SystemParameters.WorkArea.Top > 0;
        var taskbarLeft = SystemParameters.WorkArea.Left > 0;
        UpdateLayout();
        var anchor = NoticePlacement.Place(area.Left, area.Top, area.Right, area.Bottom, ActualWidth, Math.Max(ActualHeight, 120), stackIndex, taskbarTop, taskbarLeft);
        Left = anchor.Left;
        Top = anchor.Top;
    }

    private static UIElement Header(NoticeItem item, NoticePalette palette, Action<NoticeWindow> close)
    {
        var row = new DockPanel();
        var icon = new Image
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 10, 0),
            Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/app-icon.png"))
        };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        var dismiss = new Button
        {
            Content = "Close",
            Padding = new Thickness(8, 2, 8, 2),
            Background = Brushes.Transparent,
            Foreground = palette.Text,
            BorderThickness = new Thickness(0)
        };
        DockPanel.SetDock(dismiss, Dock.Right);
        row.Children.Add(dismiss);
        row.Children.Add(new TextBlock
        {
            Text = "Meta Quest Tray Tool",
            Foreground = palette.Accent,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        dismiss.Click += (_, _) =>
        {
            if (Window.GetWindow(dismiss) is NoticeWindow window)
            {
                close(window);
            }
        };
        return row;
    }

    private static string SeverityLabel(NoticeSeverity severity) => severity switch
    {
        NoticeSeverity.Success => "Success",
        NoticeSeverity.Warning => "Warning",
        NoticeSeverity.Error => "Error",
        _ => "Information"
    };

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private static string ActionLabel(NoticeAction action) => action switch
    {
        NoticeAction.OpenPerformance => "Open Performance",
        NoticeAction.OpenHeadset => "Open Headset",
        NoticeAction.OpenProfiles => "Open Profiles",
        NoticeAction.ViewLogs => "View Logs",
        NoticeAction.OpenRecovery => "Open Recovery",
        _ => "Open Settings"
    };
}

public readonly record struct NoticePalette(System.Windows.Media.Brush Background, System.Windows.Media.Brush Text, System.Windows.Media.Brush Muted, System.Windows.Media.Brush Accent, System.Windows.Media.Brush OnAccent)
{
    public static NoticePalette For(AppTheme theme, NoticeSeverity severity, bool translucent)
    {
        var accent = severity switch
        {
            NoticeSeverity.Success => Color.FromRgb(0x3D, 0xDC, 0x97),
            NoticeSeverity.Warning => Color.FromRgb(0xE6, 0xA2, 0x3C),
            NoticeSeverity.Error => Color.FromRgb(0xE8, 0x5D, 0x5D),
            _ => Color.FromRgb(0x3D, 0xDC, 0xFF)
        };
        if (theme == AppTheme.Light)
        {
            return new NoticePalette(
                new SolidColorBrush(Color.FromArgb(translucent ? (byte)236 : (byte)255, 0xF7, 0xF9, 0xFC)),
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(0x3D, 0x4A, 0x5C)),
                new SolidColorBrush(accent),
                Brushes.Black);
        }

        var alpha = translucent ? theme == AppTheme.Black ? (byte)230 : (byte)220 : (byte)255;
        return new NoticePalette(
            new SolidColorBrush(Color.FromArgb(alpha, 0x0A, 0x0E, 0x14)),
            Brushes.White,
            new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC4)),
            new SolidColorBrush(accent),
            Brushes.Black);
    }
}

public sealed class QuickControlWindow : Window
{
    public QuickControlWindow(IReadOnlyList<(string Label, string Value)> rows, Action<string> command, AppTheme theme, bool translucent)
    {
        var palette = NoticePalette.For(theme, NoticeSeverity.Information, translucent);
        Title = "Meta Quest Tray Tool";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        var root = new Border
        {
            Margin = new Thickness(8),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Background = palette.Background,
            BorderBrush = palette.Accent,
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.35,
                Color = Colors.Black
            }
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Meta Quest Tray Tool",
            Foreground = palette.Accent,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });
        foreach (var row in rows)
        {
            stack.Children.Add(new TextBlock
            {
                Text = row.Label + ": " + row.Value,
                Foreground = palette.Text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
        }

        var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        Add(buttons, "Settings", "settings", command, palette);
        Add(buttons, "Performance", "performance", command, palette);
        Add(buttons, "Headset", "headset", command, palette);
        Add(buttons, "Profiles", "profiles", command, palette);
        Add(buttons, "SteamVR", "steamvr", command, palette);
        Add(buttons, "Recover", "recover", command, palette);
        Add(buttons, "Pause ADB", "adb", command, palette);
        Add(buttons, "History", "history", command, palette);
        Add(buttons, "Exit", "exit", command, palette);
        stack.Children.Add(buttons);
        root.Child = stack;
        Content = root;
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 12;
            Top = area.Bottom - ActualHeight - 12;
        };
    }

    private static void Add(System.Windows.Controls.Panel panel, string caption, string commandName, Action<string> command, NoticePalette palette)
    {
        var button = new Button
        {
            Content = caption,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 6, 10, 6),
            Background = palette.Accent,
            Foreground = palette.OnAccent,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        button.Click += (_, _) => command(commandName);
        panel.Children.Add(button);
    }
}

public sealed class NoticeHistoryWindow : Window
{
    public NoticeHistoryWindow(DesktopNotificationCenter center)
    {
        Title = "Notification history";
        Width = 460;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x16));
        var root = new DockPanel { Margin = new Thickness(16) };
        var clear = new Button { Content = "Clear history", Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(10, 6, 10, 6) };
        DockPanel.SetDock(clear, Dock.Top);
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        foreach (var item in center.History)
        {
            item.Read = true;
            list.Items.Add(item.CreatedUtc.ToLocalTime().ToString("t") + "  " + item.Severity + "  " + item.Category + "\n" + item.Title + "\n" + item.Message);
        }

        clear.Click += (_, _) =>
        {
            center.ClearHistory();
            list.Items.Clear();
        };
        root.Children.Add(clear);
        root.Children.Add(list);
        Content = root;
    }
}
