using System.Text;

namespace MetaQuestTrayTool.Services;

public static class SystemInfoService
{
    public static string BuildReport()
    {
        var app = App.Instance;
        app.Oculus.Refresh();
        var openXr = app.OpenXr.ReadActiveKind();
        var headset = app.Headset.ReadIdentity(app.Settings.Current.Headset);
        var link = app.LinkConnection.Probe();
        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        text.AppendLine($"By {AppInfo.Author}");
        text.AppendLine($"Elevated: {app.StartupRegistration.IsProcessElevated}");
        text.AppendLine($"OpenXR: {OpenXrRuntimeService.Label(openXr)}");
        text.AppendLine($"OpenXR JSON: {app.OpenXr.ReadActivePath() ?? "(none)"}");
        text.AppendLine(app.Oculus.DescribeStatus());
        text.AppendLine($"PCVR connection: {link.Describe()}");
        text.AppendLine($"Debug Tool: {(app.DebugTool.IsAvailable ? app.DebugTool.CliPath : "not found")}");
        text.AppendLine($"ADB: {app.Adb.AdbPath ?? "not found"}");
        text.AppendLine();
        text.AppendLine("Headset");
        text.AppendLine($"  VR headset: {headset.IsVrHeadset}");
        text.AppendLine($"  Connected: {headset.IsReady}");
        text.AppendLine($"  Ignored (phone/tablet/emulator): {headset.IsIgnored}");
        if (!string.IsNullOrWhiteSpace(headset.IgnoreReason))
        {
            text.AppendLine($"  Why: {headset.IgnoreReason}");
        }
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

        text.AppendLine();
        text.AppendLine("PCVR connection probe");
        text.AppendLine($"  Kind: {link.Kind}");
        text.AppendLine($"  Summary: {link.Summary}");
        if (!string.IsNullOrWhiteSpace(link.Detail))
        {
            text.AppendLine($"  Detail: {link.Detail}");
        }
        text.AppendLine($"  Session active: {link.SessionActive}");
        text.AppendLine($"  Meta DeviceCache isUsingAirLink: {FormatNullableBool(link.IsUsingAirLink)}");
        text.AppendLine($"  DeviceCache connectionState: {link.DeviceCacheConnectionState ?? "—"}");
        text.AppendLine($"  DeviceCache headset serial: {link.HeadsetSerial ?? "—"}");
        text.AppendLine($"  Oculus/Meta USB VID present: {link.UsbHeadsetPresent}");
        text.AppendLine($"  Meta HMD (EnumHmd): {link.MetaHmdReported}");
        text.AppendLine($"  SteamVR running: {link.SteamVrRunning}");
        text.AppendLine($"  Virtual Desktop running: {link.VirtualDesktopRunning}");

        var caps = VrSessionCapabilities.From(link);
        text.AppendLine();
        text.AppendLine("Session capabilities");
        text.AppendLine($"  Meta Link registry: {caps.AllowsMetaLinkRegistry}");
        text.AppendLine($"  Oculus Debug Tool (SS/ASW): {caps.AllowsOculusDebugTool}");
        text.AppendLine($"  OpenXR switch: {caps.AllowsOpenXrSwitch}");
        text.AppendLine($"  Headset ADB: {caps.AllowsHeadsetAdb}");
        if (!string.IsNullOrWhiteSpace(caps.Banner))
        {
            text.AppendLine($"  Note: {caps.Banner}");
        }

        return text.ToString().TrimEnd();
    }

    private static string FormatNullableBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "—"
    };
}
