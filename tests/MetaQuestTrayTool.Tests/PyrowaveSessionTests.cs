using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PyrowaveSessionTests
{
    [Fact]
    public void ReadsAKnownProfileNameAndRefusesUnverifiedWrites()
    {
        var report = PyrowaveSession.Read("preset Quality 120 Hz only");
        Assert.Contains("Quality 120 Hz", report.ActiveProfile, StringComparison.Ordinal);
        Assert.Contains("unavailable", report.Bitrate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", report.Wavelet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unavailable", report.Chroma, StringComparison.OrdinalIgnoreCase);
        Assert.Null(PyrowaveSession.TryApply("{\"note\":\"pyrowave\"}", "bitrate", "100", false, out var detail));
        Assert.Contains("nothing was written", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(PyrowaveSession.TryApply("{\"session_settings\":{}}", "bitrate", "100", false, out var stock));
        Assert.Contains("Stock ALVR", stock, StringComparison.Ordinal);
    }
}
