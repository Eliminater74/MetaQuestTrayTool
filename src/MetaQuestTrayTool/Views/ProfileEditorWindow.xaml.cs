using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views;

public partial class ProfileEditorWindow : Window
{
    private static readonly string[] Priorities = ["Normal", "AboveNormal", "High"];

    public GameProfile Profile { get; }

    public ProfileEditorWindow(GameProfile profile)
    {
        InitializeComponent();
        Profile = profile;

        foreach (var value in GameSettings.SuperSamplingPresets)
        {
            SuperSamplingBox.Items.Add(new ComboBoxItem
            {
                Content = value <= 0 ? "Off (no override)" : value.ToString("0.0"),
                Tag = value
            });
        }

        foreach (AswMode mode in Enum.GetValues<AswMode>())
        {
            AswBox.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
        }

        foreach (var priority in Priorities)
        {
            PriorityBox.Items.Add(priority);
        }

        NameBox.Text = profile.Name;
        ProcessBox.Text = profile.ProcessName;
        FovBox.Text = profile.Settings.FovMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
        CommentsBox.Text = profile.Comments ?? string.Empty;
        SelectByTag(SuperSamplingBox, profile.Settings.SuperSampling);
        SelectByTag(AswBox, profile.Settings.AswMode);
        PriorityBox.SelectedItem = Priorities.Contains(profile.CpuPriority) ? profile.CpuPriority : "Normal";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Give the profile a name.", App.AppName);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProcessBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Enter the game process name, for example BeatSaber.", App.AppName);
            return;
        }

        if (!double.TryParse(FovBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fov)
            || fov < 0.5 || fov > 1.5)
        {
            System.Windows.MessageBox.Show(this, "FOV multiplier must be a number between 0.50 and 1.50.", App.AppName);
            return;
        }

        Profile.Name = NameBox.Text.Trim();
        Profile.ProcessName = ProfileService.NormalizeProcessName(ProcessBox.Text);
        Profile.Settings.SuperSampling = SuperSamplingBox.SelectedItem is ComboBoxItem ssItem && ssItem.Tag is double ss
            ? ss
            : 1.0;
        Profile.Settings.AswMode = AswBox.SelectedItem is ComboBoxItem aswItem && aswItem.Tag is AswMode mode
            ? mode
            : AswMode.Inherit;
        Profile.Settings.FovMultiplier = fov;
        Profile.CpuPriority = PriorityBox.SelectedItem as string ?? "Normal";
        Profile.Comments = string.IsNullOrWhiteSpace(CommentsBox.Text) ? null : CommentsBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (item.Tag is double left && value is double right && Math.Abs(left - right) < 0.001)
            {
                box.SelectedItem = item;
                return;
            }

            if (Equals(item.Tag, value))
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }
}
