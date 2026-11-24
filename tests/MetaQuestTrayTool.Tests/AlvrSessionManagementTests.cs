using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class AlvrSessionManagementTests
{
    private const string Session = """
    {
      "client_connections": {
        "1039.client": { "display_name": "Quest 3", "connection_state": "Connected", "cabled": false, "current_ip": "10.0.0.8" }
      },
      "session_settings": {
        "video": {
          "preferred_fps": 72,
          "bitrate": { "mode": { "variant": "ConstantMbps", "ConstantMbps": 100, "Adaptive": { "saturation_multiplier": 1.0 } } },
          "transcoding_view_resolution": { "variant": "Absolute", "Scale": 1.0, "Absolute": { "width": 2048, "height": { "set": true, "content": 1056 } } },
          "emulated_headset_view_resolution": { "variant": "Scale", "Scale": 1.2, "Absolute": { "width": 2112, "height": { "set": false, "content": 1056 } } }
        },
        "extra": "keep"
      }
    }
    """;

    [Fact]
    public void ReadReportsClientsBitrateAndResolutionsWithoutInventingTelemetry()
    {
        var report = AlvrSessionManagement.Read(Session);
        Assert.Equal("72", report.PreferredFps);
        Assert.Contains("ConstantMbps 100", report.Bitrate, StringComparison.Ordinal);
        Assert.Contains("2048x1056", report.StreamResolution, StringComparison.Ordinal);
        Assert.Contains("Scale 1.2", report.RenderResolution, StringComparison.Ordinal);
        Assert.Equal("Connected", report.Clients[0].ConnectionState);
        Assert.Contains("not stored", report.Telemetry, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a field", report.Profiles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep", Session, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstantBitrateAndResolutionKeepUnknownFieldsAndRefuseARunningServer()
    {
        Assert.Null(AlvrSessionManagement.TrySetConstantBitrate(Session, 150, true, out var running));
        Assert.Contains("running", running, StringComparison.OrdinalIgnoreCase);

        var bitrate = AlvrSessionManagement.TrySetConstantBitrate(Session, 150, false, out var bitrateDetail);
        Assert.Contains("ConstantMbps\":150", bitrate, StringComparison.Ordinal);
        Assert.Contains("keep", bitrate, StringComparison.Ordinal);
        Assert.Contains("saturation_multiplier", bitrate, StringComparison.Ordinal);
        Assert.Contains("not restarted", bitrateDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(AlvrSessionManagement.TrySetConstantBitrate(Session, 5000, false, out _));

        var adaptive = Session.Replace("\"variant\": \"ConstantMbps\"", "\"variant\": \"Adaptive\"", StringComparison.Ordinal);
        Assert.Null(AlvrSessionManagement.TrySetConstantBitrate(adaptive, 150, false, out var refused));
        Assert.Contains("variant was not changed", refused, StringComparison.OrdinalIgnoreCase);

        var width = AlvrSessionManagement.TrySetResolution(Session, "transcoding_view_resolution", "width", 2080, false, out _);
        Assert.Contains("2080", width, StringComparison.Ordinal);
        Assert.Contains("keep", width, StringComparison.Ordinal);
        Assert.Null(AlvrSessionManagement.TrySetResolution(Session, "transcoding_view_resolution", "width", 100, false, out _));
        Assert.Null(AlvrSessionManagement.TrySetResolution(Session, "emulated_headset_view_resolution", "width", 2080, false, out var scale));
        Assert.Contains("not switched", scale, StringComparison.OrdinalIgnoreCase);
        Assert.Null(AlvrSessionManagement.TrySetResolution(Session, "other", "width", 2080, false, out _));
    }

    [Fact]
    public void BackupRestoreRefusesARunningServer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mqt-alvr-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "session.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, Session);
            File.WriteAllText(path + ".bak", Session.Replace("72", "90", StringComparison.Ordinal));
            Assert.Null(AlvrSessionManagement.RestoreBackup(path, true, out var running));
            Assert.Contains("running", running, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("72", File.ReadAllText(path), StringComparison.Ordinal);
            var restored = AlvrSessionManagement.RestoreBackup(path, false, out _);
            Assert.Contains("90", restored, StringComparison.Ordinal);
            Assert.Contains("90", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
