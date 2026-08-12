using System.Windows;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        TitleText.Text = AppInfo.ProductName;
        AuthorText.Text = $"By {AppInfo.Author}";
        VersionText.Text = $"Version {AppInfo.Version}";
        DonateBlurbText.Text = AppInfo.DonateBlurb;
    }

    private void Donate_Click(object sender, RoutedEventArgs e) => DonateService.Open(this);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
