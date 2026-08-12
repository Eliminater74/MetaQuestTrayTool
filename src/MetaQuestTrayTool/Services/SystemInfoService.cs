using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class SystemInfoService
{
    public static string BuildReport()
    {
        var app = App.Instance;
        app.Oculus.Refresh();
        var openXr = app.OpenXr.ReadActiveKind();
        var headset = app.Headset.ReadIdentity(app.Settings.Current.Headset);
        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        text.AppendLine($"By {AppInfo.Author}");
        text.AppendLine($"Elevated: {app.StartupRegistration.IsProcessElevated}");
        text.AppendLine($"OpenXR: {OpenXrRuntimeService.Label(openXr)}");
        text.AppendLine($"OpenXR JSON: {app.OpenXr.ReadActivePath() ?? "(none)"}");
        text.AppendLine(app.Oculus.DescribeStatus());
        text.AppendLine($"Debug Tool: {(app.DebugTool.IsAvailable ? app.DebugTool.CliPath : "not found")}");
        text.AppendLine($"ADB: {app.Adb.AdbPath ?? "not found"}");
        text.AppendLine();
        text.AppendLine("Headset");
        text.AppendLine($"  Connected: {headset.IsReady}");
        text.AppendLine($"  ADB state: {headset.State ?? "disconnected"}");
        text.AppendLine($"  Model: {headset.Model ?? "—"}");
        text.AppendLine($"  Device: {headset.Device ?? "—"}");
        text.AppendLine($"  Manufacturer: {headset.Manufacturer ?? "—"}");
        text.AppendLine($"  Hardware serial: {headset.Serial ?? "—"}");
        text.AppendLine($"  ADB serial: {headset.AdbSerial ?? "—"}");
        text.AppendLine($"  Android: {headset.AndroidVersion ?? "—"}");
        text.AppendLine($"  Build: {headset.BuildDisplay ?? "—"}");
        text.AppendLine($"  Fingerprint: {headset.Fingerprint ?? "—"}");
        text.AppendLine($"  Trusted serial: {app.Settings.Current.Headset.TrustedSerial ?? "(none yet)"}");
        text.AppendLine($"  Trust required: {app.Settings.Current.Headset.RequireTrustedHeadset}");
        text.AppendLine($"  This device trusted: {headset.IsTrusted}");
        text.AppendLine($"  Rogue / blocked: {headset.IsRogue}");
        if (app.DebugTool.LastHeadsetSerials.Count > 0)
        {
            text.AppendLine($"  Debug Tool serials: {string.Join(", ", app.DebugTool.LastHeadsetSerials)}");
        }

        return text.ToString().TrimEnd();
    }
}
