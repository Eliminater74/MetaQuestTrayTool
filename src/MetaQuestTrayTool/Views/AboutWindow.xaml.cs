using System.Windows;

namespace MetaQuestTrayTool.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {App.GetVersion()}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
