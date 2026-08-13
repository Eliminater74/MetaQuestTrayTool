using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Views.Pages;

public partial class PowerOptionsPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public PowerOptionsPage()
    {
        InitializeComponent();

        TriggerBox.Items.Add(new ComboBoxItem { Content = "Link audio session", Tag = PowerPlanTrigger.LinkAudioSession });
        TriggerBox.Items.Add(new ComboBoxItem { Content = "Oculus service", Tag = PowerPlanTrigger.OculusService });
        TriggerBox.Items.Add(new ComboBoxItem { Content = "Tool start / exit", Tag = PowerPlanTrigger.ToolStartExit });

        UsbBox.Items.Add(new ComboBoxItem { Content = "Disabled while VR plan active", Tag = true });
        UsbBox.Items.Add(new ComboBoxItem { Content = "Leave Windows default", Tag = false });

        VrPlanBox.SelectionChanged += (_, _) => Persist_Changed();
        FallbackPlanBox.SelectionChanged += (_, _) => Persist_Changed();
        TriggerBox.SelectionChanged += (_, _) => Persist_Changed();
        UsbBox.SelectionChanged += (_, _) => Persist_Changed();
        AutoSwitchBox.Checked += (_, _) => Persist_Changed();
        AutoSwitchBox.Unchecked += (_, _) => Persist_Changed();
        SleepBox.Checked += (_, _) => Persist_Changed();
        SleepBox.Unchecked += (_, _) => Persist_Changed();
    }

    public void Refresh()
    {
        _loading = true;
        var settings = App.Instance.Settings.Current.Power;
        var plans = App.Instance.Power.ListPlans();
        PopulatePlans(VrPlanBox, plans, settings.VrPlanGuid);
        PopulatePlans(FallbackPlanBox, plans, settings.FallbackPlanGuid);
        SelectByTag(TriggerBox, settings.ApplyOn);
        SelectByTag(UsbBox, settings.DisableUsbSelectiveSuspendWhileRunning);
        AutoSwitchBox.IsChecked = settings.AutoSwitchEnabled;
        SleepBox.IsChecked = settings.RestartServiceAfterSleep;
        ActivePlanText.Text = "Active plan: " + (App.Instance.Power.GetActivePlan()?.Name ?? "unknown");
        _loading = false;
    }

    private void Persist_Changed()
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        WriteToSettings();
        App.Instance.Settings.Save();
        App.Instance.Log.Info("Power options saved.");
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

        Refresh();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        WriteToSettings();
        App.Instance.Settings.Save();
        var result = App.Instance.Power.RestoreFallbackPlan(App.Instance.Settings.Current.Power);
        App.Instance.Log.Info(result);
        Refresh();
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        var result = App.Instance.Power.CaptureCurrentAsFallback(App.Instance.Settings.Current.Power);
        App.Instance.Settings.Save();
        App.Instance.Log.Info(result);
        Refresh();
    }

    private void WriteToSettings()
    {
        var settings = App.Instance.Settings.Current.Power;
        settings.AutoSwitchEnabled = AutoSwitchBox.IsChecked == true;
        settings.RestartServiceAfterSleep = SleepBox.IsChecked == true;
        settings.VrPlanGuid = SelectedGuid(VrPlanBox);
        settings.FallbackPlanGuid = SelectedGuid(FallbackPlanBox);
        if (TriggerBox.SelectedItem is ComboBoxItem { Tag: PowerPlanTrigger trigger })
        {
            settings.ApplyOn = trigger;
        }

        if (UsbBox.SelectedItem is ComboBoxItem { Tag: bool usb })
        {
            settings.DisableUsbSelectiveSuspendWhileRunning = usb;
        }

        App.Instance.Settings.Current.Service.RestartServiceWhenComputerWakes = settings.RestartServiceAfterSleep;
    }

    private static void PopulatePlans(System.Windows.Controls.ComboBox box, IReadOnlyList<PowerPlanInfo> plans, string? selectedGuid)
    {
        box.Items.Clear();
        box.Items.Add(new ComboBoxItem { Content = "Not Used", Tag = null });
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

    private static string? SelectedGuid(System.Windows.Controls.ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Tag as string : null;

    private static void SelectByTag(System.Windows.Controls.ComboBox box, object tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (Equals(item.Tag, tag))
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
