using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class TelemetryExportTests
{
    [Fact]
    public void CsvAndJsonKeepUnavailableValuesAndSources()
    {
        var when = DateTimeOffset.Parse("2026-11-24T15:00:00Z");
        var metrics = new[]
        {
            TelemetryMetric.AvailableNumber("PcGpuUtilization", "%", 40, MetricQuality.Measured, "nvidia-smi", "pc", when),
            TelemetryMetric.Unavailable("RuntimeFps", "FPS", "pasted-log", "headset", when, "not in this paste")
        };
        var csv = TelemetryExport.ToCsv(metrics);
        var json = TelemetryExport.ToJson(metrics);
        Assert.Contains("PcGpuUtilization", csv, StringComparison.Ordinal);
        Assert.Contains("Unavailable", csv, StringComparison.Ordinal);
        Assert.Contains("pasted-log", json, StringComparison.Ordinal);
        Assert.Contains("not in this paste", json, StringComparison.Ordinal);
        Assert.Contains("nvidia-smi", csv, StringComparison.Ordinal);
    }
}
