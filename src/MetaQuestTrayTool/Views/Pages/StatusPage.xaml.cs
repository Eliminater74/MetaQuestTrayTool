using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MetaQuestTrayTool.Views.Pages;

public partial class StatusPage : System.Windows.Controls.UserControl, IShellPage
{
    private readonly ObservableCollection<StatusChipVm> _chips = [];
    private readonly DispatcherTimer _timer;

    public StatusPage()
    {
        InitializeComponent();
        ChipsList.ItemsSource = _chips;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _timer.Tick += (_, _) =>
        {
            if (IsVisible && IsLoaded)
            {
                Refresh();
            }
        };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public void Refresh()
    {
        var chips = App.Instance.StatusDashboard.BuildChips();
        _chips.Clear();
        foreach (var chip in chips)
        {
            _chips.Add(chip);
        }

        var ready = chips.FirstOrDefault(c => c.Title == "PCVR Ready");
        var steam = chips.FirstOrDefault(c => c.Title == "SteamVR");
        var session = chips.FirstOrDefault(c => c.Title == "PCVR session");

        HeadlineText.Text = ready?.Detail ?? ready?.Value ?? "Status";
        HeadlineText.Foreground = BrushFor(ready?.Kind ?? StatusChipKind.Off);

        var bits = new List<string>();
        if (steam is not null)
        {
            bits.Add($"SteamVR: {steam.Value}");
        }

        if (session is not null)
        {
            bits.Add($"Session: {session.Value}");
        }

        var openXr = chips.FirstOrDefault(c => c.Title == "OpenXR");
        if (openXr is not null)
        {
            bits.Add($"OpenXR: {openXr.Value}");
        }

        SubheadText.Text = bits.Count == 0
            ? "Live status updates while this page is open."
            : string.Join("  ·  ", bits);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void ChipAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string actionId })
        {
            return;
        }

        try
        {
            var summary = App.Instance.StatusDashboard.RunAction(actionId);
            App.Instance.Log.Info($"Status action [{actionId}]: {summary}");
            Refresh();
            System.Windows.MessageBox.Show(Window.GetWindow(this), summary, App.AppName);
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn($"Status action failed: {ex.Message}");
            System.Windows.MessageBox.Show(Window.GetWindow(this), ex.Message, App.AppName);
        }
    }

    private static MediaBrush BrushFor(StatusChipKind kind) => kind switch
    {
        StatusChipKind.On => new SolidColorBrush(MediaColor.FromRgb(0x3D, 0xDC, 0xFF)),
        StatusChipKind.Warn => new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xB0, 0x20)),
        StatusChipKind.Fail => new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x5C, 0x6C)),
        _ => (MediaBrush)System.Windows.Application.Current.FindResource("AppMutedBrush")
    };
}
