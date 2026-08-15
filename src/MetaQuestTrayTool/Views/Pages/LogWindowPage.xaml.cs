using System.IO;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Services;
using MetaQuestTrayTool.Views;

namespace MetaQuestTrayTool.Views.Pages;

public partial class LogWindowPage : System.Windows.Controls.UserControl, IShellPage
{
    public LogWindowPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LogList.ItemsSource = App.Instance.Log.Entries;
            App.Instance.Log.EntryAdded += OnLogAdded;
            ScrollToEnd();
        };
        Unloaded += (_, _) => App.Instance.Log.EntryAdded -= OnLogAdded;
    }

    public void Refresh()
    {
        LogList.ItemsSource = App.Instance.Log.Entries;
        ScrollToEnd();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            "Clear the activity log?\n\nThis removes on-screen entries and empties app.log on disk.",
            App.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        App.Instance.Log.Clear();
        App.Instance.Log.Info("Log cleared.");
        Refresh();
        if (Window.GetWindow(this) is MainShellWindow shell)
        {
            shell.RefreshActivePage();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureAppDataDirectory();
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        SessionHelperClient.TryLaunchExe(explorer, "\"" + AppPaths.AppDataDirectory + "\"", workingDirectory: null, out _);
    }

    private void OnLogAdded(object? sender, Models.LogEntry e) => ScrollToEnd();

    private void ScrollToEnd()
    {
        if (LogList.Items.Count == 0)
        {
            return;
        }

        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
