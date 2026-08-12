using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>One-click Quest Link / Air Link registry presets for the Quest Link page.</summary>
public sealed class LinkPreset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required LinkSettings Settings { get; init; }
}

public static class LinkPresetCatalog
{
    public static IReadOnlyList<LinkPreset> All { get; } =
    [
        new()
        {
            Id = "link-default",
            Name = "Meta defaults",
            Description = "Clear overrides — let Meta pick bitrate / encode / sharpen.",
            Settings = new LinkSettings { PresetName = "Meta defaults" }
        },
        new()
        {
            Id = "link-balanced",
            Name = "Balanced",
            Description = "Everyday Link / Air Link — 200 Mbps, encode 2912, normal sharpen.",
            Settings = new LinkSettings
            {
                PresetName = "Balanced",
                BitrateMbps = 200,
                EncodeResolutionWidth = 2912,
                Sharpening = LinkSharpeningMode.Normal,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled,
                DynamicBitrateMax = 350,
                PreferHevc = true,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        },
        new()
        {
            Id = "link-performance",
            Name = "Performance",
            Description = "Lower encode load — 150 Mbps, encode 2352, sharpen off.",
            Settings = new LinkSettings
            {
                PresetName = "Performance",
                BitrateMbps = 150,
                EncodeResolutionWidth = 2352,
                Sharpening = LinkSharpeningMode.Disabled,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled,
                DynamicBitrateMax = 250,
                PreferHevc = true,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        },
        new()
        {
            Id = "link-quality",
            Name = "Quality",
            Description = "Sharper stream — 350 Mbps, encode 3664, quality sharpen.",
            Settings = new LinkSettings
            {
                PresetName = "Quality",
                BitrateMbps = 350,
                EncodeResolutionWidth = 3664,
                Sharpening = LinkSharpeningMode.Quality,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled,
                DynamicBitrateMax = 500,
                PreferHevc = true,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        },
        new()
        {
            Id = "link-air-hevc",
            Name = "Air Link (HEVC)",
            Description = "Wireless-friendly — HEVC, 250 Mbps, encode 3136, DBR on.",
            Settings = new LinkSettings
            {
                PresetName = "Air Link (HEVC)",
                BitrateMbps = 250,
                EncodeResolutionWidth = 3136,
                Sharpening = LinkSharpeningMode.Normal,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled,
                DynamicBitrateMax = 400,
                PreferHevc = true,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        },
        new()
        {
            Id = "link-wired-h264",
            Name = "Wired Link (H.264)",
            Description = "Cable path — H.264, sliced encoding off, 300 Mbps, encode 3664.",
            Settings = new LinkSettings
            {
                PresetName = "Wired Link (H.264)",
                BitrateMbps = 300,
                EncodeResolutionWidth = 3664,
                Sharpening = LinkSharpeningMode.Quality,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Disabled,
                PreferHevc = false,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        },
        new()
        {
            Id = "link-sim",
            Name = "Sim / clarity",
            Description = "Flight / racing clarity — 400 Mbps, encode 3664, quality sharpen, DBR.",
            Settings = new LinkSettings
            {
                PresetName = "Sim / clarity",
                BitrateMbps = 400,
                EncodeResolutionWidth = 3664,
                Sharpening = LinkSharpeningMode.Quality,
                EncodeDynamicBitrate = EncodeDynamicBitrateMode.Enabled,
                DynamicBitrateMax = 500,
                PreferHevc = true,
                DisableSlicedEncoding = true,
                DistortionCurvature = DistortionCurvature.Low
            }
        }
    ];
}
