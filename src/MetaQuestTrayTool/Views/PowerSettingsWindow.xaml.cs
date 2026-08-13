using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views;

public partial class PowerSettingsWindow : Window
{
    private bool _loading;

    public PowerSettingsWindow()
    {
        InitializeComponent();

        VrPlanBox.SelectionChanged += (_, _) => Persist_Changed();
        FallbackPlanBox.SelectionChanged += (_, _) => Persist_Changed();
        AutoSwitchBox.Checked += (_, _) => Persist_Changed();
        AutoSwitchBox.Unchecked += (_, _) => Persist_Changed();
        UsbBox.Checked += (_, _) => Persist_Changed();
        UsbBox.Unchecked += (_, _) => Persist_Changed();
        SleepBox.Checked += (_, _) => Persist_Changed();
        SleepBox.Unchecked += (_, _) => Persist_Changed();

        Reload();
    }

    private void Reload()
    {
        _loading = true;
        var settings = App.Instance.Settings.Current.Power;
        var plans = App.Instance.Power.ListPlans();
        Populate(VrPlanBox, plans, settings.VrPlanGuid);
        Populate(FallbackPlanBox, plans, settings.FallbackPlanGuid);
        AutoSwitchBox.IsChecked = settings.AutoSwitchEnabled;
        UsbBox.IsChecked = settings.DisableUsbSelectiveSuspendWhileRunning;
        SleepBox.IsChecked = settings.RestartServiceAfterSleep;
        ActivePlanText.Text = "Active plan: " + (App.Instance.Power.GetActivePlan()?.Name ?? "unknown");
        _loading = false;
    }

    private static void Populate(System.Windows.Controls.ComboBox box, IReadOnlyList<PowerPlanInfo> plans, string? selectedGuid)
    {
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = "(none)", Tag = null });
        foreach (var plan in plans)
        {
            box.Items.Add(new ComboBoxItem
            {
                Content = plan.ToString(),
                Tag = plan.Guid.ToString("D")
            });
        }

        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, selectedGuid) || (item.Tag is null && string.IsNullOrWhiteSpace(selectedGuid)))
            {
                box.SelectedItem = item;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private void Persist_Changed()
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        WriteToSettings();
        App.Instance.Settings.Save();
        App.Instance.Log.Info("Power settings saved.");
    }

    private void ApplyVr_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var result = App.Instance.Power.ApplyVrPlan(App.Instance.Settings.Current.Power);
        App.Instance.Log.Info(result);
        if (App.Instance.Settings.Current.Power.DisableUsbSelectiveSuspendWhileRunning)
        {
            App.Instance.Log.Info(App.Instance.Power.SetUsbSelectiveSuspend(enabled: false));
        }

        Reload();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var result = App.Instance.Power.RestoreFallbackPlan(App.Instance.Settings.Current.Power);
        App.Instance.Log.Info(result);
        Reload();
    }

    private void CaptureFallback_Click(object sender, RoutedEventArgs e)
    {
        var result = App.Instance.Power.CaptureCurrentAsFallback(App.Instance.Settings.Current.Power);
        App.Instance.Settings.Save();
        App.Instance.Log.Info(result);
        Reload();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void WriteToSettings()
    {
        var settings = App.Instance.Settings.Current.Power;
        settings.AutoSwitchEnabled = AutoSwitchBox.IsChecked == true;
        settings.DisableUsbSelectiveSuspendWhileRunning = UsbBox.IsChecked == true;
        settings.RestartServiceAfterSleep = SleepBox.IsChecked == true;
        settings.VrPlanGuid = SelectedGuid(VrPlanBox);
        settings.FallbackPlanGuid = SelectedGuid(FallbackPlanBox);
    }

    private static string? SelectedGuid(System.Windows.Controls.ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Tag as string : null;
}
