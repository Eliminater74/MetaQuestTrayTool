using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Services;

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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureAppDataDirectory();
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.AppDataDirectory,
            UseShellExecute = true
        });
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
