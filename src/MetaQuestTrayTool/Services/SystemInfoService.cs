using System.IO;
using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class SystemInfoService
{
    public static string BuildReport(bool includeEnumHmd = true)
    {
        var app = App.Instance;
        app.Oculus.Refresh();
        var openXr = app.OpenXr.ReadActiveKind();
        var headset = app.Headset.ReadIdentity(app.Settings.Current.Headset);
        var link = app.LinkConnection.Probe(includeEnumHmd: includeEnumHmd);
        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        text.AppendLine($"By {AppInfo.Author}");
        text.AppendLine($"Elevated: {app.StartupRegistration.IsProcessElevated}");
        text.AppendLine($"OpenXR: {OpenXrRuntimeService.Label(openXr)}");
        text.AppendLine($"OpenXR JSON: {app.OpenXr.ReadActivePath() ?? "(none)"}");
        text.AppendLine(app.Oculus.DescribeStatus());
        text.AppendLine($"Debug Tool CLI: {(app.DebugTool.IsAvailable ? app.DebugTool.CliPath : "not found")}");
        text.AppendLine($"Debug Tool GUI: {(app.Oculus.DebugToolGuiPath is { } gui && File.Exists(gui) ? gui : "not found")}");
        text.AppendLine($"ADB binary: {app.Adb.AdbPath ?? "not found"}");
        text.AppendLine();

        text.AppendLine("Graphics");
        var gpuRec = app.Gpu.GetRecommendation();
        if (gpuRec is null)
        {
            text.AppendLine("  Adapter: not detected");
        }
        else
        {
            text.AppendLine($"  Adapter: {gpuRec.Adapter.Name}");
            text.AppendLine($"  Vendor: {gpuRec.Adapter.VendorLabel}");
            text.AppendLine($"  Tier: {gpuRec.Adapter.TierLabel}");
            text.AppendLine($"  VRAM: {gpuRec.Adapter.DedicatedMemoryLabel}");
            text.AppendLine($"  Recommended Link preset: {gpuRec.LinkPresetName}");
            text.AppendLine($"  Recommended global preset: {gpuRec.GlobalPresetName}");
            text.AppendLine($"  Note: {gpuRec.Rationale}");
        }

        var otherGpus = app.Gpu.ListAdapters()
            .Where(a => gpuRec is null || !string.Equals(a.Name, gpuRec.Adapter.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (otherGpus.Count > 0)
        {
            text.AppendLine($"  Other adapters: {string.Join("; ", otherGpus.Select(a => a.Summary))}");
        }

        text.AppendLine();

        text.AppendLine("Link session (Meta / Steam / VD)");
        text.AppendLine($"  Status: {link.InfoBanner}");
        text.AppendLine($"  Kind: {link.Kind}");
        text.AppendLine($"  Session active: {link.SessionActive}");
        text.AppendLine($"  Transport: {DescribeTransport(link)}");
        if (!string.IsNullOrWhiteSpace(link.Detail))
        {
            text.AppendLine($"  Detail: {link.Detail}");
        }
        text.AppendLine($"  DeviceCache isUsingAirLink: {FormatNullableBool(link.IsUsingAirLink)}");
        text.AppendLine($"  DeviceCache connectionState: {link.DeviceCacheConnectionState ?? "—"}");
        text.AppendLine($"  DeviceCache headset serial: {link.HeadsetSerial ?? "—"}");
        text.AppendLine($"  Oculus USB VID present: {link.UsbHeadsetPresent} (cable can be charge/ADB while on Air Link)");
        text.AppendLine($"  Meta HMD EnumHmd: {(includeEnumHmd ? link.MetaHmdReported.ToString() : "skipped (UI refresh)")}");
        text.AppendLine($"  SteamVR running: {link.SteamVrRunning}");
        text.AppendLine($"  Virtual Desktop running: {link.VirtualDesktopRunning}");

        text.AppendLine();
        text.AppendLine("ADB (USB / wireless debugging — optional)");
        text.AppendLine($"  Status: {headset.DescribeAdbBanner(link)}");
        text.AppendLine($"  VR headset on ADB: {headset.IsVrHeadset}");
        text.AppendLine($"  ADB ready: {headset.IsReady}");
        text.AppendLine($"  ADB state: {headset.State ?? "disconnected"}");
        if (headset.IsIgnored && !string.IsNullOrWhiteSpace(headset.IgnoreReason))
        {
            text.AppendLine($"  Ignored: {headset.IgnoreReason}");
        }
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
            text.AppendLine($"  Last Debug Tool serials: {string.Join(", ", app.DebugTool.LastHeadsetSerials)}");
        }

        var caps = VrSessionCapabilities.From(link);
        text.AppendLine();
        text.AppendLine("What this session allows");
        text.AppendLine($"  Meta Link registry: {caps.AllowsMetaLinkRegistry}");
        text.AppendLine($"  Oculus Debug Tool (SS/ASW): {caps.AllowsOculusDebugTool}");
        text.AppendLine($"  OpenXR switch: {caps.AllowsOpenXrSwitch}");
        text.AppendLine($"  Headset ADB tweaks: {caps.AllowsHeadsetAdb}");
        if (!string.IsNullOrWhiteSpace(caps.Banner))
        {
            text.AppendLine($"  Note: {caps.Banner}");
        }

        var steamTip = app.SteamLinkAssist.DescribeOpenXrMismatch(link);
        if (!string.IsNullOrWhiteSpace(steamTip))
        {
            text.AppendLine($"  Steam Link tip: {steamTip}");
        }

        text.AppendLine($"  Prefer SteamVR during Steam Link: {app.Settings.Current.OpenXr.PreferSteamVrDuringSteamLink}");

        return text.ToString().TrimEnd();
    }

    private static string DescribeTransport(VrConnectionStatus link) => link.Kind switch
    {
        VrConnectionKind.MetaAirLink => "Wireless Meta Air Link (not USB Link)",
        VrConnectionKind.MetaWiredLink => "USB Meta Quest Link",
        VrConnectionKind.SteamLinkOrSteamVr => "Steam Link / SteamVR stream",
        VrConnectionKind.VirtualDesktop => "Virtual Desktop stream",
        VrConnectionKind.MetaLinkUnknownTransport => "Meta Link (wired vs Air unclear)",
        _ => "None / idle"
    };

    private static string FormatNullableBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "—"
    };
}
