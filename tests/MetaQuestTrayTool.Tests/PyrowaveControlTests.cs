using System.Text.Json.Nodes;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PyrowaveControlTests
{
    private const string Session = """
    {
      "session_settings": {
        "video": {
          "preferred_fps": 90,
          "bitrate": { "mode": { "variant": "ConstantMbps", "ConstantMbps": 100 } },
          "pyrowave": { "wavelet": { "variant": "Cdf97" }, "chroma_444": true, "transport": { "variant": "Tcp" }, "latency_stamp": false },
          "transcoding_view_resolution": { "variant": "Absolute", "Absolute": { "width": 2048, "height": { "set": true, "content": 1056 } } },
          "emulated_headset_view_resolution": { "variant": "Scale", "Scale": 1 }
        }
      }
    }
    """;

    [Fact]
    public void ReadReportsVerifiedSessionFieldsAndKeepsFileWritesRefused()
    {
        var report = PyrowaveSession.Read(Session);
        Assert.Equal("90", report.Refresh);
        Assert.Contains("ConstantMbps", report.Bitrate, StringComparison.Ordinal);
        Assert.Equal("Cdf97", report.Wavelet);
        Assert.Equal("444", report.Chroma);
        Assert.Equal("Tcp", report.WiredConnections);
        Assert.Contains("2048", report.StreamResolution, StringComparison.Ordinal);
        Assert.Contains("latency_stamp", report.Telemetry, StringComparison.Ordinal);
        Assert.Null(PyrowaveSession.TryApply(Session, "bitrate", "80", true, out var detail));
        Assert.Contains("nothing was written", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetValuesUsesControlPyPathsAndNeverIncludesRestart()
    {
        Assert.Null(PyrowaveControl.TryBuild("bitrate", "100", false, out var blocked));
        Assert.Contains("Nothing was sent", blocked, StringComparison.Ordinal);

        var body = PyrowaveControl.TryBuild("bitrate", "100", true, out _);
        Assert.Contains("session_settings", body, StringComparison.Ordinal);
        Assert.Contains("ConstantMbps", body, StringComparison.Ordinal);
        Assert.DoesNotContain("RestartSteamvr", body, StringComparison.Ordinal);

        var haar = PyrowaveControl.TryBuild("wavelet", "Haar,Compute", true, out _);
        Assert.Contains("decode_path", haar, StringComparison.Ordinal);
        Assert.Null(PyrowaveControl.TryBuild("wavelet", "Haar,Fragment", true, out var fragment));
        Assert.Contains("Fragment is refused", fragment, StringComparison.Ordinal);

        var render = PyrowaveControl.TryBuild("render", "2112,1056", true, out var renderDetail);
        Assert.Contains("emulated_headset_view_resolution", render, StringComparison.Ordinal);
        Assert.Contains("Absolute", render, StringComparison.Ordinal);
        Assert.Contains("does not restart", renderDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(PyrowaveControl.TryBuild("refresh", "90", true, out _));
        Assert.Null(PyrowaveControl.TryBuild("automatic-bitrate", "on", true, out _));
        Assert.Null(PyrowaveControl.TryBuild("wired", "on", true, out var wired));
        Assert.Contains("UpdateClientList", wired, StringComparison.Ordinal);
        Assert.Contains("unavailable", wired, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SendPostsOnlyTheDashboardUrl()
    {
        string? seen = null;
        var transport = new RecordingTransport(body => seen = body);
        var body = PyrowaveControl.TryBuild("chroma", "420", true, out _)!;
        PyrowaveControl.Send(transport, body);
        Assert.Equal(PyrowaveControl.DashboardUrl, transport.Url);
        Assert.Contains("chroma_444", seen, StringComparison.Ordinal);
        Assert.Contains("false", seen, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => PyrowaveControl.Send(transport, "{\"RestartSteamvr\":true}"));
    }

    private sealed class RecordingTransport : IPyrowaveTransport
    {
        private readonly Action<string> _record;
        public RecordingTransport(Action<string> record) => _record = record;
        public string Url { get; private set; } = string.Empty;
        public string Post(string url, string body)
        {
            Url = url;
            _record(body);
            JsonNode.Parse(body);
            return "ok";
        }
    }
}
