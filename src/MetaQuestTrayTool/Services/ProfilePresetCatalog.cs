using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public static class ProfilePresetCatalog
{
    private static readonly Lazy<IReadOnlyList<ProfilePreset>> AllPresets = new(BuildAll);

    public static IReadOnlyList<ProfilePreset> GlobalPresets =>
        AllPresets.Value.Where(p => p.Kind == ProfilePresetKind.Global).ToList();

    public static IReadOnlyList<ProfilePreset> GamePresets =>
        AllPresets.Value.Where(p => p.Kind == ProfilePresetKind.Game).ToList();

    public static IReadOnlyList<ProfilePreset> GameTemplates =>
        AllPresets.Value.Where(p => p.Kind == ProfilePresetKind.GameTemplate).ToList();

    public static ProfilePreset? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : AllPresets.Value.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static ProfilePreset? BestGamePresetForProcess(string? processName)
    {
        var normalized = ProfileService.NormalizeProcessName(processName);
        if (normalized.Length == 0)
        {
            return null;
        }

        return GamePresets.FirstOrDefault(p =>
                   ProfileService.NormalizeProcessName(p.ProcessName) == normalized)
               ?? GamePresets.FirstOrDefault(p =>
                   normalized.Contains(ProfileService.NormalizeProcessName(p.ProcessName), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<ProfilePreset> PresetsForEditor(string? processName)
    {
        var list = new List<ProfilePreset>();
        list.AddRange(GameTemplates);
        var match = BestGamePresetForProcess(processName);
        if (match is not null && !list.Any(p => p.Id == match.Id))
        {
            list.Insert(0, match);
        }

        foreach (var preset in GamePresets.Where(p => p.Id != match?.Id))
        {
            list.Add(preset);
        }

        return list;
    }

    public static void ApplyToProfile(GameProfile profile, ProfilePreset preset)
    {
        profile.Settings = preset.Settings.Clone();
        profile.Link = preset.Link.Clone();
        profile.OpenXrRuntime = preset.OpenXrRuntime;
        profile.CpuPriority = preset.CpuPriority;
        if (!string.IsNullOrWhiteSpace(preset.ProcessName)
            && string.IsNullOrWhiteSpace(profile.ProcessName))
        {
            profile.ProcessName = preset.ProcessName;
        }

        if (string.IsNullOrWhiteSpace(profile.Comments))
        {
            profile.Comments = preset.Description;
        }
    }

    public static void ApplyGlobalPreset(AppSettings settings, ProfilePreset preset)
    {
        settings.DefaultGameSettings = preset.Settings.Clone();
        if (preset.GlobalLink is not null)
        {
            settings.LinkSettings = preset.GlobalLink.Clone();
        }

        if (preset.OpenXrRuntime is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            settings.OpenXr.PreferredRuntime = preset.OpenXrRuntime;
        }
    }

    private static IReadOnlyList<ProfilePreset> BuildAll()
    {
        var presets = new List<ProfilePreset>
        {
            Global("global-balanced", "Balanced", "Everyday Link — SS 1.0, ASW Auto, 200 Mbps.",
                ss: 1.0, asw: AswMode.Auto, linkMbps: 200, sharpen: LinkSharpeningMode.Normal),
            Global("global-performance", "Performance", "Higher FPS — SS off, 150 Mbps, normal sharpen.",
                ss: 0, asw: AswMode.Auto, linkMbps: 150, sharpen: LinkSharpeningMode.Normal),
            Global("global-quality", "Quality", "Sharper image — SS 1.3, ASW off, 350 Mbps, quality sharpen.",
                ss: 1.3, asw: AswMode.Off, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3664),
            Global("global-sim", "Sim / Cockpit", "Flight & driving sims — SS 1.2, stable ASW, 400 Mbps, slight FOV trim.",
                ss: 1.2, asw: AswMode.Auto, fov: 0.92, linkMbps: 400, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "AboveNormal"),
            Global("global-competitive", "Competitive", "Low latency — SS 1.1, ASW off, 250 Mbps.",
                ss: 1.1, asw: AswMode.Off, linkMbps: 250, sharpen: LinkSharpeningMode.Normal, priority: "High"),

            Template("tpl-performance", "Template: Performance", "Light SS, ASW off, inherit global Link.",
                ss: 1.1, asw: AswMode.Off, priority: "AboveNormal"),
            Template("tpl-quality", "Template: Quality", "Higher SS, ASW off, quality Link overrides.",
                ss: 1.4, asw: AswMode.Off, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3664),
            Template("tpl-sim", "Template: Sim", "Stable sim tuning with high Link bitrate.",
                ss: 1.2, asw: AswMode.Auto, fov: 0.92, linkMbps: 400, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "AboveNormal"),

            Game("msfs2024", "Microsoft Flight Simulator 2024", "FlightSimulator2024",
                "Long sessions — high encode, quality sharpen, sim SS.",
                ss: 1.2, asw: AswMode.Auto, fov: 0.90, linkMbps: 500, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "AboveNormal"),
            Game("msfs2020", "Microsoft Flight Simulator", "FlightSimulator",
                "Same sim tuning for MSFS 2020.",
                ss: 1.2, asw: AswMode.Auto, fov: 0.90, linkMbps: 450, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "AboveNormal"),
            Game("dcs", "DCS World", "DCS",
                "Combat flight sim — high Link bitrate, quality sharpen.",
                ss: 1.2, asw: AswMode.Auto, fov: 0.92, linkMbps: 500, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "High"),
            Game("iracing", "iRacing", "iRacingSim64DX11",
                "Racing sim — stable ASW, high encode.",
                ss: 1.1, asw: AswMode.Auto, linkMbps: 400, sharpen: LinkSharpeningMode.Quality, encode: 3664,
                priority: "AboveNormal"),
            Game("elite", "Elite Dangerous", "EliteDangerous64",
                "Space sim — balanced quality.",
                ss: 1.2, asw: AswMode.Auto, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3136),
            Game("beatsaber", "Beat Saber", "BeatSaber",
                "Rhythm — low latency, SS 1.2, ASW off.",
                ss: 1.2, asw: AswMode.Off, linkMbps: 200, priority: "AboveNormal"),
            Game("hlalyx", "Half-Life: Alyx", "hlvr",
                "Story AAA — quality SS 1.3.",
                ss: 1.3, asw: AswMode.Off, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3664),
            Game("boneworks", "Boneworks", "Boneworks",
                "Physics sandbox — performance first.",
                ss: 1.0, asw: AswMode.Auto, linkMbps: 250, priority: "AboveNormal"),
            Game("skyrimvr", "Skyrim VR", "SkyrimVR",
                "Open world — higher SS, quality Link.",
                ss: 1.4, asw: AswMode.Off, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3664),
            Game("pavlov", "Pavlov VR", "Pavlov-Win64-Shipping",
                "Multiplayer shooter — competitive tuning.",
                ss: 1.1, asw: AswMode.Off, linkMbps: 250, priority: "High"),
            Game("bas", "Blade & Sorcery", "BladeAndSorcery",
                "Physics combat — performance.",
                ss: 1.1, asw: AswMode.Auto, linkMbps: 250, priority: "AboveNormal"),
            Game("nms", "No Man's Sky", "NMS",
                "Exploration — balanced quality.",
                ss: 1.2, asw: AswMode.Auto, linkMbps: 300, sharpen: LinkSharpeningMode.Quality),
            Game("loneecho", "Lone Echo", "LoneEcho",
                "Zero-G adventure — quality.",
                ss: 1.3, asw: AswMode.Off, linkMbps: 350, sharpen: LinkSharpeningMode.Quality),
            Game("pop1", "Population: ONE", "POPULATIONONE-Win64-Shipping",
                "Battle royale — competitive.",
                ss: 1.1, asw: AswMode.Off, linkMbps: 250, priority: "High"),
            Game("assettocorsa", "Assetto Corsa", "acs",
                "Racing — sim-style Link.",
                ss: 1.1, asw: AswMode.Auto, linkMbps: 350, sharpen: LinkSharpeningMode.Quality, encode: 3136,
                priority: "AboveNormal"),
            Game("ets2", "Euro Truck Simulator 2", "eurotrucks2",
                "Trucking sim — stable ASW, high bitrate.",
                ss: 1.1, asw: AswMode.Auto, linkMbps: 350, sharpen: LinkSharpeningMode.Normal, priority: "AboveNormal")
        };

        return presets;
    }

    private static ProfilePreset Global(string id, string name, string description,
        double ss, AswMode asw, int linkMbps = 200,
        double fov = 1.0, LinkSharpeningMode sharpen = LinkSharpeningMode.Normal,
        int encode = 0, string priority = "Normal") =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = ProfilePresetKind.Global,
            Settings = BuildSettings(ss, asw, fov),
            GlobalLink = BuildGlobalLink(name, linkMbps, sharpen, encode),
            CpuPriority = priority
        };

    private static ProfilePreset Template(string id, string name, string description,
        double ss, AswMode asw, int? linkMbps = null,
        double fov = 1.0, LinkSharpeningMode sharpen = LinkSharpeningMode.Default,
        int? encode = null, string priority = "Normal") =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = ProfilePresetKind.GameTemplate,
            Settings = BuildSettings(ss, asw, fov),
            Link = BuildLinkOverrides(linkMbps, sharpen, encode),
            CpuPriority = priority
        };

    private static ProfilePreset Game(string id, string name, string process, string description,
        double ss, AswMode asw, int? linkMbps = null,
        double fov = 1.0, LinkSharpeningMode sharpen = LinkSharpeningMode.Default,
        int? encode = null, string priority = "Normal",
        OpenXrRuntimeKind openXr = OpenXrRuntimeKind.Inherit) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = ProfilePresetKind.Game,
            ProcessName = process,
            Settings = BuildSettings(ss, asw, fov),
            Link = BuildLinkOverrides(linkMbps, sharpen, encode),
            OpenXrRuntime = openXr,
            CpuPriority = priority
        };

    private static GameSettings BuildSettings(double ss, AswMode asw, double fov) => new()
    {
        SuperSampling = ss,
        AswMode = asw,
        FovMultiplier = fov,
        AdaptiveGpuScaling = true,
        UseFovStencil = true
    };

    private static LinkSettings BuildGlobalLink(string presetName, int linkMbps,
        LinkSharpeningMode sharpen, int encode) => new()
        {
            PresetName = presetName,
            BitrateMbps = linkMbps,
            Sharpening = sharpen,
            EncodeResolutionWidth = encode
        };

    private static LinkProfileOverrides BuildLinkOverrides(int? linkMbps,
        LinkSharpeningMode sharpen, int? encode) =>
        new()
        {
            Sharpening = sharpen,
            BitrateMbps = linkMbps,
            EncodeResolutionWidth = encode
        };
}
