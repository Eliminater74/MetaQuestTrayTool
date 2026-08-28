using System.Windows;
using System.Windows.Controls;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class HeadsetPage : System.Windows.Controls.UserControl, IShellPage
{
    private bool _loading;

    public HeadsetPage()
    {
        InitializeComponent();
        Add(CpuGpuBox, "Set by app (default)", HeadsetCpuGpuLevel.AppDefault);
        Add(CpuGpuBox, "Level 2", HeadsetCpuGpuLevel.Level2);
        Add(CpuGpuBox, "Level 4", HeadsetCpuGpuLevel.Level4);

        Add(TextureBox, "Device default", HeadsetTexturePreset.DeviceDefault);
        Add(TextureBox, "Default — Quest 1 (1216×1344)", HeadsetTexturePreset.Quest1);
        Add(TextureBox, "Default — Quest 2 (1440×1584)", HeadsetTexturePreset.Quest2);
        Add(TextureBox, "Default — Quest 3 (1680×1760)", HeadsetTexturePreset.Quest3);
        Add(TextureBox, "512", HeadsetTexturePreset.Square512);
        Add(TextureBox, "768", HeadsetTexturePreset.Square768);
        Add(TextureBox, "1024", HeadsetTexturePreset.Square1024);
        Add(TextureBox, "1280", HeadsetTexturePreset.Square1280);
        Add(TextureBox, "1536", HeadsetTexturePreset.Square1536);
        Add(TextureBox, "2048", HeadsetTexturePreset.Square2048);
        Add(TextureBox, "2560", HeadsetTexturePreset.Square2560);
        Add(TextureBox, "3072", HeadsetTexturePreset.Square3072);

        Add(RefreshBox, "Device default (usually 72Hz)", HeadsetRefreshRate.DeviceDefault);
        Add(RefreshBox, "60Hz", HeadsetRefreshRate.Hz60);
        Add(RefreshBox, "72Hz", HeadsetRefreshRate.Hz72);
        Add(RefreshBox, "80Hz", HeadsetRefreshRate.Hz80);
        Add(RefreshBox, "90Hz", HeadsetRefreshRate.Hz90);
        Add(RefreshBox, "120Hz", HeadsetRefreshRate.Hz120);

        Add(FfrBox, "Device / app default", HeadsetFfrLevel.DeviceDefault);
        Add(FfrBox, "Off (best quality)", HeadsetFfrLevel.Off);
        Add(FfrBox, "Low", HeadsetFfrLevel.Low);
        Add(FfrBox, "Medium", HeadsetFfrLevel.Medium);
        Add(FfrBox, "High", HeadsetFfrLevel.High);
        Add(FfrBox, "High Top (best performance)", HeadsetFfrLevel.HighTop);

        Add(ChromaBox, "App selected (default)", HeadsetChromaMode.AppSelected);
        Add(ChromaBox, "On", HeadsetChromaMode.On);
        Add(ChromaBox, "Off", HeadsetChromaMode.Off);

        Add(CaptureSizeBox, "Device default (1024×1024)", HeadsetCaptureSize.DeviceDefault);
        Add(CaptureSizeBox, "640 × 480", HeadsetCaptureSize.Size640x480);
        Add(CaptureSizeBox, "1280 × 720", HeadsetCaptureSize.Size1280x720);
        Add(CaptureSizeBox, "1920 × 1080", HeadsetCaptureSize.Size1920x1080);
        Add(CaptureSizeBox, "1024 × 1024", HeadsetCaptureSize.Size1024x1024);
        Add(CaptureSizeBox, "1600 × 1600", HeadsetCaptureSize.Size1600x1600);

        Add(CaptureFpsBox, "Device default", HeadsetCaptureFps.DeviceDefault);
        Add(CaptureFpsBox, "24 fps", HeadsetCaptureFps.Fps24);
        Add(CaptureFpsBox, "30 fps", HeadsetCaptureFps.Fps30);
        Add(CaptureFpsBox, "60 fps", HeadsetCaptureFps.Fps60);

        Add(CaptureBitrateBox, "Device default (5 Mbps)", HeadsetCaptureBitrate.DeviceDefault);
        Add(CaptureBitrateBox, "5 Mbps", HeadsetCaptureBitrate.Mbps5);
        Add(CaptureBitrateBox, "10 Mbps", HeadsetCaptureBitrate.Mbps10);
        Add(CaptureBitrateBox, "15 Mbps", HeadsetCaptureBitrate.Mbps15);
        Add(CaptureBitrateBox, "20 Mbps", HeadsetCaptureBitrate.Mbps20);
    }

    public void Refresh()
    {
        _loading = true;
        var headset = App.Instance.Settings.Current.Headset;
        ApplyOnConnectBox.IsChecked = headset.ApplyWhenHeadsetConnects;
        Select(CpuGpuBox, headset.CpuGpuLevel);
        Select(TextureBox, headset.TextureSize);
        Select(RefreshBox, headset.RefreshRate);
        Select(FfrBox, headset.Ffr);
        Select(ChromaBox, headset.ChromaticAberration);
        Select(CaptureSizeBox, headset.CaptureSize);
        Select(CaptureFpsBox, headset.CaptureFps);
        Select(CaptureBitrateBox, headset.CaptureBitrate);
        StereoBox.IsChecked = headset.StereoCapture;
        FullRateBox.IsChecked = headset.FullRateCapture;
        RequireTrustBox.IsChecked = headset.RequireTrustedHeadset;
        CustomAdbBox.Text = string.Join(Environment.NewLine, headset.CustomAdbCommands);
        WirelessHostBox.Text = headset.WirelessHost ?? string.Empty;
        WirelessPortBox.Text = headset.WirelessPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        WirelessAutoBox.IsChecked = headset.WirelessAutoReconnect;
        HeadsetOnlyWirelessBox.IsChecked = headset.HeadsetOnlyWirelessAdb;
        _loading = false;

        // ADB identity is slow — don't block the first paint of this page.
        StatusText.Text = "Checking ADB…";
        TrustText.Text = "…";
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            UpdateTrustBanner);
    }

    private void ComboPersist_Changed(object sender, SelectionChangedEventArgs e) => Persist_Changed(sender, e);

    private void Persist_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded)
        {
            return;
        }

        var headset = App.Instance.Settings.Current.Headset;
        headset.ApplyWhenHeadsetConnects = ApplyOnConnectBox.IsChecked == true;
        headset.CpuGpuLevel = Read<HeadsetCpuGpuLevel>(CpuGpuBox, headset.CpuGpuLevel);
        headset.TextureSize = Read<HeadsetTexturePreset>(TextureBox, headset.TextureSize);
        headset.RefreshRate = Read<HeadsetRefreshRate>(RefreshBox, headset.RefreshRate);
        headset.Ffr = Read<HeadsetFfrLevel>(FfrBox, headset.Ffr);
        headset.ChromaticAberration = Read<HeadsetChromaMode>(ChromaBox, headset.ChromaticAberration);
        headset.CaptureSize = Read<HeadsetCaptureSize>(CaptureSizeBox, headset.CaptureSize);
        headset.CaptureFps = Read<HeadsetCaptureFps>(CaptureFpsBox, headset.CaptureFps);
        headset.CaptureBitrate = Read<HeadsetCaptureBitrate>(CaptureBitrateBox, headset.CaptureBitrate);
        headset.StereoCapture = StereoBox.IsChecked == true;
        headset.FullRateCapture = FullRateBox.IsChecked == true;
        headset.RequireTrustedHeadset = RequireTrustBox.IsChecked == true;
        headset.CustomAdbCommands = CustomCommandSet.ParseLines(CustomAdbBox.Text);
        headset.WirelessHost = string.IsNullOrWhiteSpace(WirelessHostBox.Text) ? null : WirelessHostBox.Text.Trim();
        if (int.TryParse(WirelessPortBox.Text?.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535)
        {
            headset.WirelessPort = port;
        }
        else
        {
            WirelessPortBox.Text = headset.WirelessPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        headset.WirelessAutoReconnect = WirelessAutoBox.IsChecked == true;
        headset.HeadsetOnlyWirelessAdb = HeadsetOnlyWirelessBox.IsChecked == true;
        App.Instance.Settings.Save();
        App.Instance.HeadsetWatch?.SyncWatch();
    }

    private void WirelessConnect_Click(object sender, RoutedEventArgs e)
    {
        Persist_Changed(this, new RoutedEventArgs());
        Run(() =>
        {
            var headset = App.Instance.Settings.Current.Headset;
            var host = (WirelessHostBox.Text ?? string.Empty).Trim();
            if (host.Length == 0)
            {
                throw new InvalidOperationException("Enter the headset LAN IP first.");
            }

            var port = headset.WirelessPort;
            var endpoint = AdbService.FormatEndpoint(host, port);
            var parts = endpoint.Split(':');
            headset.WirelessHost = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedPort))
            {
                headset.WirelessPort = parsedPort;
            }

            WirelessHostBox.Text = headset.WirelessHost ?? string.Empty;
            WirelessPortBox.Text = headset.WirelessPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return App.Instance.Adb.ConnectWirelessHeadset(headset.WirelessHost!, headset.WirelessPort, headset);
        });
    }

    private void WirelessPair_Click(object sender, RoutedEventArgs e)
    {
        Persist_Changed(this, new RoutedEventArgs());
        Run(() =>
        {
            var host = (WirelessHostBox.Text ?? string.Empty).Trim();
            if (host.Length == 0)
            {
                throw new InvalidOperationException("Enter the headset LAN IP first.");
            }

            if (!int.TryParse(PairingPortBox.Text?.Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var pairingPort)
                || pairingPort is < 1 or > 65535)
            {
                throw new InvalidOperationException(
                    "Enter the pairing port from Wireless debugging → Pair device with pairing code.");
            }

            var code = (PairingCodeBox.Text ?? string.Empty).Trim();
            var summary = App.Instance.Adb.PairWireless(host, pairingPort, code);
            App.Instance.Settings.Current.Headset.WirelessHost = host;
            App.Instance.Settings.Save();
            PairingCodeBox.Text = string.Empty;
            return summary;
        });
    }

    private void WirelessDisconnect_Click(object sender, RoutedEventArgs e)
    {
        Persist_Changed(this, new RoutedEventArgs());
        Run(() =>
        {
            var headset = App.Instance.Settings.Current.Headset;
            return string.IsNullOrWhiteSpace(headset.WirelessHost)
                ? App.Instance.Adb.DisconnectWireless()
                : App.Instance.Adb.DisconnectWireless(headset.WirelessHost, headset.WirelessPort);
        });
    }

    private async void WirelessTcpip_Click(object sender, RoutedEventArgs e)
    {
        Persist_Changed(this, new RoutedEventArgs());
        try
        {
            var headset = App.Instance.Settings.Current.Headset;
            var (summary, suggested) = await Task.Run(() =>
            {
                var text = App.Instance.Adb.EnableTcpipMode(headset.WirelessPort, out var host);
                return (text, host);
            }).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(suggested))
            {
                headset.WirelessHost = suggested;
                WirelessHostBox.Text = suggested;
            }

            App.Instance.Log.Info(summary);
            ResultText.Text = summary;
            App.Instance.Settings.Save();
            UpdateTrustBanner();
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn(ex.Message);
            App.Instance.HeadsetAnnouncer.AnnounceHeadsetAction("Headset action failed. Check Log.");
            ResultText.Text = ex.Message;
            UpdateTrustBanner();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.Apply(App.Instance.Settings.Current.Headset));
    private void ProxOn_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.SetProximitySensor(true, App.Instance.Settings.Current.Headset));
    private void ProxOff_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.SetProximitySensor(false, App.Instance.Settings.Current.Headset));
    private void GuardianPause_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.SetGuardianPaused(true, App.Instance.Settings.Current.Headset));
    private void GuardianResume_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.SetGuardianPaused(false, App.Instance.Settings.Current.Headset));

    private void SendText_Click(object sender, RoutedEventArgs e) =>
        Run(() => App.Instance.Headset.SendText(PasteBox.Text ?? string.Empty, App.Instance.Settings.Current.Headset));

    private void Trust_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Persist_Changed(this, new RoutedEventArgs());
            var result = App.Instance.Headset.TrustCurrentHeadset(App.Instance.Settings.Current.Headset);
            App.Instance.Settings.Save();
            App.Instance.Log.Info(result);
            App.Instance.HeadsetAnnouncer.AnnounceHeadsetAction(result);
            ResultText.Text = result;
            UpdateTrustBanner();
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn(ex.Message);
            App.Instance.HeadsetAnnouncer.AnnounceHeadsetAction("Headset action failed. Check Log.");
            ResultText.Text = ex.Message;
        }
    }

    private void ClearTrust_Click(object sender, RoutedEventArgs e)
    {
        var headset = App.Instance.Settings.Current.Headset;
        headset.TrustedSerial = null;
        headset.TrustedModel = null;
        App.Instance.Settings.Save();
        UpdateTrustBanner();
        ResultText.Text = "Trusted headset cleared. The next connected Quest will be remembered.";
    }

    private void UpdateTrustBanner()
    {
        var identity = App.Instance.Headset.ReadIdentity(App.Instance.Settings.Current.Headset);
        TrustText.Text = identity.Summary;
        StatusText.Text = App.Instance.Adb.DescribeStatus();
        RuntimeText.Text = identity.IsReady && identity.IsVrHeadset
            ? "Battery / Wi‑Fi: " + (identity.Runtime?.Summary ?? "reading…")
            : "Battery / Wi‑Fi: connect USB or wireless ADB to read.";
    }

    private async void Run(Func<string> action)
    {
        try
        {
            Persist_Changed(this, new RoutedEventArgs());
            var result = await Task.Run(action).ConfigureAwait(true);
            App.Instance.Log.Info(result);
            App.Instance.HeadsetAnnouncer.AnnounceHeadsetAction(result);
            ResultText.Text = result;
            App.Instance.Settings.Save();
            UpdateTrustBanner();
        }
        catch (Exception ex)
        {
            App.Instance.Log.Warn(ex.Message);
            App.Instance.HeadsetAnnouncer.AnnounceHeadsetAction("Headset action failed. Check Log.");
            ResultText.Text = ex.Message;
            UpdateTrustBanner();
        }
    }

    private static void Add(System.Windows.Controls.ComboBox box, string label, object tag) =>
        box.Items.Add(new ComboBoxItem { Content = label, Tag = tag });

    private static void Select(System.Windows.Controls.ComboBox box, object value)
    {
        foreach (ComboBoxItem item in box.Items)
        {
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

    private static T Read<T>(System.Windows.Controls.ComboBox box, T fallback) =>
        box.SelectedItem is ComboBoxItem { Tag: T value } ? value : fallback;
}
