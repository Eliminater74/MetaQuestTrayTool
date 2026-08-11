using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class LibraryPickerWindow : Window
{
    private IReadOnlyList<LibraryGame> _all = [];

    public LibraryGame? SelectedGame { get; private set; }

    public LibraryPickerWindow()
    {
        InitializeComponent();
        FilterBox.Items.Add("All platforms");
        FilterBox.Items.Add("Steam");
        FilterBox.Items.Add("Meta");
        FilterBox.SelectedIndex = 0;
        Reload();
    }

    private void Reload()
    {
        _all = App.Instance.Library.GetAllGames();
        ApplyFilter();
        StatusText.Text = $"{_all.Count(game => game.Platform == GamePlatform.Steam)} Steam · {_all.Count(game => game.Platform == GamePlatform.Meta)} Meta";
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
    private void GameList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AcceptSelection();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

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
