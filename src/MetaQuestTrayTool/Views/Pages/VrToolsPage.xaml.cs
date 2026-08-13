using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class VrToolsPage : System.Windows.Controls.UserControl, IShellPage
{
    public VrToolsPage()
    {
        InitializeComponent();
        CategoriesList.ItemsSource = VrToolCatalog.ByCategory().ToList();
    }

    public void Refresh()
    {
        // Static catalog — nothing to re-read.
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string url })
        {
            return;
        }

        try
        {
            UrlLaunchService.Open(url);
            App.Instance.Log.Info($"Opened VR tool link: {url}");
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn($"Could not open link: {ex.Message}");
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                ex.Message,
                AppInfo.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
