using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class LibraryPickerWindow : Window
{
    private IReadOnlyList<LibraryGame> _all = [];
    private CancellationTokenSource? _prefetch;

    public LibraryGame? SelectedGame { get; private set; }

    public LibraryPickerWindow()
    {
        InitializeComponent();
        FilterBox.Items.Add("All platforms");
        FilterBox.Items.Add("Steam");
        FilterBox.Items.Add("Meta");
        FilterBox.SelectedIndex = 0;
        Closed += (_, _) => _prefetch?.Cancel();
        Reload();
    }

    private void Reload()
    {
        _prefetch?.Cancel();
        _all = App.Instance.Library.GetAllGames();
        ApplyFilter();
        var missingArt = _all.Count(game => string.IsNullOrWhiteSpace(game.ArtworkPath));
        StatusText.Text = $"{_all.Count(game => game.Platform == GamePlatform.Steam)} Steam · {_all.Count(game => game.Platform == GamePlatform.Meta)} Meta"
                          + (missingArt > 0 ? " · fetching missing Steam covers…" : string.Empty);
        _prefetch = new CancellationTokenSource();
        _ = PrefetchMissingSteamArtAsync(_prefetch.Token);
    }

    private async Task PrefetchMissingSteamArtAsync(CancellationToken cancellationToken)
    {
        try
        {
            await App.Instance.Library.Artwork.PrefetchSteamAsync(
                _all,
                (game, path) => Dispatcher.Invoke(() => game.ArtworkPath = path),
                cancellationToken).ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested)
            {
                StatusText.Text = $"{_all.Count(game => game.Platform == GamePlatform.Steam)} Steam · {_all.Count(game => game.Platform == GamePlatform.Meta)} Meta";
            }
        }
        catch (OperationCanceledException)
        {
            // window closed or refreshed
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn($"Library artwork download failed: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<LibraryGame> query = _all;
        query = FilterBox.SelectedIndex switch
        {
            1 => query.Where(game => game.Platform == GamePlatform.Steam),
            2 => query.Where(game => game.Platform == GamePlatform.Meta),
            _ => query
        };

        var search = SearchBox.Text?.Trim() ?? string.Empty;
        if (search.Length > 0)
        {
            query = query.Where(game =>
                game.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || game.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        GameList.ItemsSource = query.ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private void Add_Click(object sender, RoutedEventArgs e) => AcceptSelection();
    private void GameList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Launch_Click(sender, e);
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (GameList.SelectedItem is not LibraryGame game)
        {
            System.Windows.MessageBox.Show(this, "Select a game first.", App.AppName);
            return;
        }

        try
        {
            var summary = App.Instance.GameLaunch.LaunchLibraryGame(game);
            App.Instance.Log.Info(summary);
            SelectedGame = game;
            System.Windows.MessageBox.Show(this, summary, App.AppName);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn(ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, App.AppName);
        }
    }

    private void AcceptSelection()
    {
        if (GameList.SelectedItem is not LibraryGame game)
        {
            System.Windows.MessageBox.Show(this, "Select a game first.", App.AppName);
            return;
        }

        SelectedGame = game;
        DialogResult = true;
    }
}
