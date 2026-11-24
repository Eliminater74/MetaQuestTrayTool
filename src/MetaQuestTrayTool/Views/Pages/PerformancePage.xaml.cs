using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Views.Pages;

public partial class PerformancePage : System.Windows.Controls.UserControl, IShellPage
{
    private readonly DispatcherTimer _timer;
    private readonly List<double> _gpuHistory = [];
    private readonly List<TelemetryMetric> _latest = [];
    private bool _monitoring;
    private bool _sampleInFlight;
    private bool _loading = true;

    public PerformancePage()
    {
        InitializeComponent();
        IntervalBox.Items.Add(new ComboBoxItem { Content = "2 seconds", Tag = 2 });
        IntervalBox.Items.Add(new ComboBoxItem { Content = "5 seconds", Tag = 5 });
        IntervalBox.Items.Add(new ComboBoxItem { Content = "10 seconds", Tag = 10 });
        IntervalBox.SelectedIndex = 1;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => _ = SamplePcAsync();
        _loading = false;
        IsVisibleChanged += (_, _) => SyncTimer();
        SummaryText.Text = "Monitoring is stopped. No headset polling is running.";
    }

    public void Refresh()
    {
        if (!_monitoring)
        {
            SummaryText.Text = "Monitoring is stopped. Headset samples run only from the button.";
        }
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var when = DateTimeOffset.Now;
        var headset = HeadsetPerformanceSample.Parse(LogBox.Text ?? string.Empty, TimeSpan.FromSeconds(10));
        var metrics = new List<TelemetryMetric>();
        metrics.AddRange(HeadsetTelemetry.FromSample(headset, when, "pasted-log"));
        metrics.AddRange(StreamingTelemetry.Parse(LogBox.Text, "pasted-log", when));
        metrics.AddRange(_latest.Where(metric => metric.Source == "nvidia-smi" || metric.Name == "PcCpuUtilization"));
        Show(metrics, headset.ToDisplayText());
    }

    private async void HeadsetSample_Click(object sender, RoutedEventArgs e)
    {
        if (!TelemetryCollectionPolicy.AllowHeadsetAdb(pcvrSessionActive: false, userRequested: true))
        {
            return;
        }

        SummaryText.Text = "Sampling headset logcat for 10 seconds…";
        try
        {
            var sample = await Task.Run(() => App.Instance.Headset.CapturePerformanceSampleModel(
                App.Instance.Settings.Current.Headset, TimeSpan.FromSeconds(10)));
            var metrics = HeadsetTelemetry.FromSample(sample, DateTimeOffset.Now, "headset").ToList();
            metrics.AddRange(StreamingTelemetry.Parse(string.Join('\n', sample.Samples.Select(item => item.RawLine)), "headset", DateTimeOffset.Now));
            Show(metrics, sample.ToDisplayText());
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or IOException)
        {
            ReportBox.Text = ex.Message;
        }
    }

    private async void PcSample_Click(object sender, RoutedEventArgs e) => await SamplePcAsync();

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _monitoring = true;
        SyncTimer();
        SummaryText.Text = "PC history is running while this page is visible. Headset ADB is not polled.";
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _monitoring = false;
        SyncTimer();
        SummaryText.Text = "PC history paused.";
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _monitoring = false;
        _gpuHistory.Clear();
        DrawChart();
        SyncTimer();
        SummaryText.Text = "PC history stopped and cleared.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text report|*.txt",
            FileName = "pcvr-performance.txt"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, ReportBox.Text ?? string.Empty);
        SummaryText.Text = "Saved " + dialog.FileName;
    }

    private void Interval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || IntervalBox.SelectedItem is not ComboBoxItem { Tag: int seconds })
        {
            return;
        }

        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private void SyncTimer()
    {
        if (_monitoring && IsVisible)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async Task SamplePcAsync()
    {
        if (_sampleInFlight)
        {
            return;
        }

        _sampleInFlight = true;
        try
        {
            var csv = await Task.Run(() => PcTelemetry.TryQueryNvidiaSmi());
            var error = csv is null ? "nvidia-smi returned no data. The GPU may be unsupported or the tool is not installed." : null;
            _latest.Clear();
            _latest.AddRange(PcTelemetry.FromProbe(csv, error, null, DateTimeOffset.Now));
            var gpu = _latest.FirstOrDefault(metric => metric.Name == "PcGpuUtilization" && metric.Value is not null)?.Value;
            if (gpu is double value)
            {
                _gpuHistory.Add(value);
                if (_gpuHistory.Count > 120)
                {
                    _gpuHistory.RemoveAt(0);
                }

                DrawChart();
            }

            if (!_monitoring)
            {
                Show(_latest, csv ?? error ?? "No PC sample.");
            }
        }
        finally
        {
            _sampleInFlight = false;
        }
    }

    private void Show(IReadOnlyList<TelemetryMetric> metrics, string evidence)
    {
        var findings = BottleneckAnalyzer.Analyze(metrics);
        var lines = metrics.Select(metric => metric.Availability == MetricAvailability.Available
            ? $"{metric.Name}: {metric.Value?.ToString("0.##") ?? metric.Text} {metric.Unit} [{metric.Quality}, {metric.Source}]"
            : $"{metric.Name}: unavailable ({metric.Note})");
        ReportBox.Text = evidence + Environment.NewLine + Environment.NewLine
            + string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine
            + BottleneckAnalyzer.ToDisplayText(findings);
        SummaryText.Text = findings[0].Title;
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();
        if (_gpuHistory.Count < 2 || ChartCanvas.ActualWidth < 10)
        {
            ChartCanvas.Width = Math.Max(ChartCanvas.ActualWidth, 300);
        }

        var width = ChartCanvas.ActualWidth > 10 ? ChartCanvas.ActualWidth : 640;
        var height = 110d;
        ChartCanvas.Width = width;
        ChartCanvas.Height = height;
        var accent = TryFindResource("AppAccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.DeepSkyBlue;
        var line = new Polyline
        {
            Stroke = accent,
            StrokeThickness = 2
        };
        for (var i = 0; i < _gpuHistory.Count; i++)
        {
            var x = _gpuHistory.Count == 1 ? 0 : i * (width - 8) / (_gpuHistory.Count - 1);
            var y = height - 8 - Math.Clamp(_gpuHistory[i], 0, 100) / 100d * (height - 16);
            line.Points.Add(new System.Windows.Point(x + 4, y));
        }

        ChartCanvas.Children.Add(line);
    }
}
