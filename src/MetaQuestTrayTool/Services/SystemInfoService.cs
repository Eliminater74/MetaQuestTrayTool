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
        var link = app.LinkConnection.Probe(includeEnumHmd: includeEnumHmd);
        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.ProductName} {AppInfo.Version}");
        text.AppendLine($"By {AppInfo.Author}");
        text.AppendLine($"Elevated: {app.StartupRegistration.IsProcessElevated}");
        text.AppendLine(app.OpenXr.Describe());
        text.AppendLine($"OpenXR JSON (64-bit): {app.OpenXr.ReadActivePath() ?? "(none)"}");
        text.AppendLine(app.Oculus.DescribeStatus());
        text.AppendLine($"Debug Tool CLI: {(app.DebugTool.IsAvailable ? app.DebugTool.CliPath : "not found")}");
        text.AppendLine($"Debug Tool GUI: {(app.Oculus.DebugToolGuiPath is { } gui && File.Exists(gui) ? gui : "not found")}");
        text.AppendLine($"ADB binary: {app.Adb.AdbPath ?? "not found"}");
        text.AppendLine();

        text.AppendLine("App internals");
        foreach (var line in SessionHelperClient.DescribeHelperDiagnostics()
                     .Split(Environment.NewLine, StringSplitOptions.None))
        {
            text.AppendLine("  " + line);
        }

        text.AppendLine($"  Settings file: {AppPaths.SettingsFile}");
        text.AppendLine($"  Profiles file: {ProfileStore.ProfilesFile}");
        text.AppendLine();

        var steamVr = app.SteamVrInstall.Probe(force: true);
        text.AppendLine("SteamVR");
        text.AppendLine($"  Installed: {steamVr.IsInstalled}");
        text.AppendLine($"  Channel: {steamVr.ChannelLabel}");
        text.AppendLine($"  Version: {steamVr.Version ?? "—"}");
        text.AppendLine($"  Steam build id: {steamVr.BuildId ?? "—"}");
        text.AppendLine($"  Beta key: {steamVr.BetaKey ?? "(none — stable)"}");
        text.AppendLine($"  Install path: {steamVr.InstallPath ?? "—"}");
        text.AppendLine($"  Manifest: {steamVr.ManifestPath ?? "—"}");
        text.AppendLine($"  Running: {steamVr.IsRunning}");
        if (!steamVr.IsInstalled)
        {
            text.AppendLine($"  Install: {SteamVrInstallService.SteamInstallUri} or {SteamVrInstallService.StoreUrl}");
        }

        text.AppendLine();

        var ready = app.PcvrReady.Evaluate();
        text.AppendLine("PCVR Ready");
        text.AppendLine($"  {ready.Summary}");
        foreach (var item in ready.Items)
        {
            text.AppendLine($"  [{item.Level}] {item.Title}: {item.Detail}");
        }

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
        text.AppendLine("ADB (on demand — this report does not query the headset)");
        text.AppendLine($"  Status: {app.Adb.DescribeCachedStatus()}");
        text.AppendLine($"  Activity: {app.Adb.ActivityMode}");
        text.AppendLine($"  Trusted serial: {app.Settings.Current.Headset.TrustedSerial ?? "(none yet)"}");
        text.AppendLine($"  Trusted model: {app.Settings.Current.Headset.TrustedModel ?? "—"}");
        text.AppendLine("  Exclusive wireless disconnect: retired");
        text.AppendLine($"  Trust required: {app.Settings.Current.Headset.RequireTrustedHeadset}");
        text.AppendLine("  Live identity, battery, and Wi‑Fi: use Headset → Check ADB now");

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
        text.AppendLine(
            $"  Headset ADB tweaks: {(link.SessionActive && caps.AllowsHeadsetAdb ? "allowed for this live session" : "idle / on demand")}");
        if (!string.IsNullOrWhiteSpace(caps.Banner))
        {
            text.AppendLine($"  Note: {caps.Banner}");
        }

        var steamTip = app.SteamLinkAssist.DescribeOpenXrMismatch(link);
        if (!string.IsNullOrWhiteSpace(steamTip))
        {
            text.AppendLine($"  Steam Link tip: {steamTip}");
        }

        var steamVrHint = SteamVrSettingsHintService.DescribeHints(link);
        if (!string.IsNullOrWhiteSpace(steamVrHint))
        {
            text.AppendLine($"  {steamVrHint}");
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
