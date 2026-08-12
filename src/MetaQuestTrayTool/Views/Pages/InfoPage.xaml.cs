using System.Windows;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class InfoPage : System.Windows.Controls.UserControl, IShellPage
{
    public InfoPage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        var openXr = App.Instance.OpenXr.ReadActiveKind();
        OpenXrBanner.Text = $"OpenXR: {OpenXrRuntimeService.Label(openXr)}";
        var headset = App.Instance.Headset.ReadIdentity(App.Instance.Settings.Current.Headset);
        HeadsetBanner.Text = headset.Summary;
        HeadsetBanner.Foreground = headset.IsRogue
            ? System.Windows.Media.Brushes.OrangeRed
            : (System.Windows.Media.Brush)FindResource("AppTextBrush");
        ReportBox.Text = SystemInfoService.BuildReport();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Donate_Click(object sender, RoutedEventArgs e) =>
        DonateService.Open(Window.GetWindow(this));

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(ReportBox.Text ?? string.Empty);
        App.Instance.Log.Info("Copied Info report to the clipboard.");
    }

    private void Trust_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = App.Instance.Headset.TrustCurrentHeadset(App.Instance.Settings.Current.Headset);
            App.Instance.Settings.Save();
            App.Instance.Log.Info(result);
            Refresh();
            System.Windows.MessageBox.Show(Window.GetWindow(this), result, App.AppName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this), ex.Message, App.AppName);
        }
    }
}
