using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class BottleneckAnalyzerTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.UnixEpoch;

    [Fact]
    public void HeadsetGpuSaturationWithPcHeadroomIsAdvisory()
    {
        var findings = BottleneckAnalyzer.Analyze([
            Metric("HeadsetGpuUtilization", 97),
            Metric("PcGpuUtilization", 65),
            Metric("HeadsetCpuUtilization", 42)
        ]);
        var finding = Assert.Single(findings);
        Assert.Equal("headset-gpu", finding.Code);
        Assert.Equal(FindingConfidence.Medium, finding.Confidence);
        Assert.Contains("Headset GPU", finding.ToDisplayText(), StringComparison.Ordinal);
        Assert.DoesNotContain("definitive", finding.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleSignalsStayNonDefinitive()
    {
        var findings = BottleneckAnalyzer.Analyze([
            Metric("PcGpuUtilization", 99),
            Metric("PcVideoEncoderUtilization", 95),
            Metric("HeadsetBatteryTemperature", 46)
        ]);
        Assert.Equal("multiple", findings[0].Code);
        Assert.Contains(findings, finding => finding.Code == "pc-gpu");
        Assert.Contains(findings, finding => finding.Code == "encoder");
        Assert.Contains(findings, finding => finding.Code == "thermal");
    }

    [Fact]
    public void MissingMetricsDoNotInventACause()
    {
        var findings = BottleneckAnalyzer.Analyze([
            TelemetryMetric.Unavailable("HeadsetGpuUtilization", "%", "headset-logcat", null, When, "missing")
        ]);
        var finding = Assert.Single(findings);
        Assert.Equal("insufficient", finding.Code);
        Assert.Equal(FindingConfidence.Insufficient, finding.Confidence);
    }

    [Fact]
    public void RuntimeFpsBelowRefreshIsNotCalledDeliveredVideoFps()
    {
        var text = BottleneckAnalyzer.ToDisplayText(BottleneckAnalyzer.Analyze([
            Metric("RuntimeFps", 92),
            Metric("RefreshHz", 120)
        ]));
        Assert.Contains("not proof of the delivered video frame rate", text, StringComparison.Ordinal);
    }

    private static TelemetryMetric Metric(string name, double value) =>
        TelemetryMetric.AvailableNumber(name, "unit", value, MetricQuality.Measured, "test", null, When, null);
}
