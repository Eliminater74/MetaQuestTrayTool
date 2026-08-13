using System.Globalization;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;
using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Detects the primary discrete GPU and maps it to Link / global game presets.
/// Does not write NVIDIA driver registry power tweaks — only Meta Link + ODT defaults.
/// </summary>
public sealed class GpuDetectionService
{
    private static readonly Regex Digits = new(@"(\d{3,4})", RegexOptions.Compiled);
    private const string DisplayClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private GpuAdapterInfo? _cached;
    private bool _probed;

    public GpuAdapterInfo? DetectPrimary(bool forceRefresh = false)
    {
        if (_probed && !forceRefresh)
        {
            return _cached;
        }

        _probed = true;
        _cached = ProbeAdapters()
            .OrderByDescending(Score)
            .FirstOrDefault();
        return _cached;
    }

    public GpuRecommendation? GetRecommendation(bool forceRefresh = false)
    {
        var adapter = DetectPrimary(forceRefresh);
        if (adapter is null)
        {
            return null;
        }

        return BuildRecommendation(adapter);
    }

    public string Describe(bool forceRefresh = false)
    {
        var rec = GetRecommendation(forceRefresh);
        if (rec is null)
        {
            return "GPU: not detected";
        }

        return $"GPU: {rec.Banner}";
    }

    public IReadOnlyList<GpuAdapterInfo> ListAdapters(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            _probed = false;
            _cached = null;
        }

        return ProbeAdapters()
            .OrderByDescending(Score)
            .ToList();
    }

    private static IEnumerable<GpuAdapterInfo> ProbeAdapters()
    {
        using var root = Registry.LocalMachine.OpenSubKey(DisplayClassPath);
        if (root is null)
        {
            yield break;
        }

        foreach (var name in root.GetSubKeyNames())
        {
            if (name.Length != 4 || !name.All(char.IsDigit))
            {
                continue;
            }

            using var key = root.OpenSubKey(name);
            if (key is null)
            {
                continue;
            }

            var desc = key.GetValue("DriverDesc") as string
                       ?? key.GetValue("HardwareInformation.AdapterString") as string;
            if (string.IsNullOrWhiteSpace(desc) || IsIgnoredAdapter(desc))
            {
                continue;
            }

            var vendor = ClassifyVendor(desc, key.GetValue("ProviderName") as string);
            var memory = ReadDedicatedMemory(key);
            yield return new GpuAdapterInfo
            {
                Name = desc.Trim(),
                Vendor = vendor,
                Tier = ClassifyTier(desc, vendor, memory),
                DedicatedMemoryBytes = memory,
                RegistryKey = name
            };
        }
    }

    private static bool IsIgnoredAdapter(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("microsoft basic")
               || n.Contains("remote desktop")
               || n.Contains("parsec")
               || n.Contains("virtual")
               || n.Contains("citrix")
               || n.Contains("vmware")
               || n.Contains("hyper-v")
               || n.Contains("orayid")
               || n.Contains("usb display")
               || n.Contains("mirage");
    }

    private static GpuVendor ClassifyVendor(string name, string? provider)
    {
        var hay = $"{name} {provider}".ToLowerInvariant();
        if (hay.Contains("nvidia") || hay.Contains("geforce") || hay.Contains("quadro") || hay.Contains("rtx") || hay.Contains("gtx"))
        {
            return GpuVendor.Nvidia;
        }

        if (hay.Contains("amd") || hay.Contains("radeon") || hay.Contains("ati "))
        {
            return GpuVendor.Amd;
        }

        if (hay.Contains("intel") || hay.Contains("arc "))
        {
            return GpuVendor.Intel;
        }

        return GpuVendor.Unknown;
    }

    private static long? ReadDedicatedMemory(RegistryKey key)
    {
        try
        {
            if (key.GetValue("HardwareInformation.qwMemorySize") is byte[] bytes && bytes.Length >= 8)
            {
                return BitConverter.ToInt64(bytes, 0);
            }

            if (key.GetValue("HardwareInformation.MemorySize") is int i && i > 0)
            {
                return i;
            }

            if (key.GetValue("HardwareInformation.MemorySize") is long l && l > 0)
            {
                return l;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static GpuPerformanceTier ClassifyTier(string name, GpuVendor vendor, long? memoryBytes)
    {
        var n = name.ToLowerInvariant();
        var gb = memoryBytes is > 0 ? memoryBytes.Value / (1024d * 1024d * 1024d) : 0;

        // Explicit flagships first.
        if (ContainsAny(n, "4090", "4080", "5090", "5080", "7900", "7900 xtx", "titan"))
        {
            return GpuPerformanceTier.Ultra;
        }

        if (ContainsAny(n, "4070 ti", "4070 super", "4070", "3080", "3090", "3070 ti", "3070",
                "6800", "6900", "7800", "7700", "a770"))
        {
            return GpuPerformanceTier.High;
        }

        if (ContainsAny(n, "4060 ti", "4060", "3060 ti", "3060", "2080", "2070", "6700", "6750",
                "7600", "a750", "a580"))
        {
            return GpuPerformanceTier.Mid;
        }

        if (ContainsAny(n, "4050", "3050", "1660", "1650", "2060", "6600", "6500", "6400",
                "a380", "iris", "uhd"))
        {
            return GpuPerformanceTier.Entry;
        }

        // Numeric RTX/GTX / RX fallback from model number.
        var series = GuessSeriesNumber(n, vendor);
        if (series is int s)
        {
            if (vendor == GpuVendor.Nvidia)
            {
                return ClassifyNvidiaSeries(s);
            }

            if (vendor == GpuVendor.Amd)
            {
                return ClassifyAmdSeries(s);
            }

            if (vendor == GpuVendor.Intel)
            {
                return s >= 770 ? GpuPerformanceTier.High
                    : s >= 580 ? GpuPerformanceTier.Mid
                    : GpuPerformanceTier.Entry;
            }
        }

        if (gb >= 16)
        {
            return GpuPerformanceTier.Ultra;
        }

        if (gb >= 10)
        {
            return GpuPerformanceTier.High;
        }

        if (gb >= 8)
        {
            return GpuPerformanceTier.Mid;
        }

        if (gb > 0)
        {
            return GpuPerformanceTier.Entry;
        }

        // Discrete NVIDIA/AMD without a clear model — assume mid rather than entry.
        if (vendor is GpuVendor.Nvidia or GpuVendor.Amd)
        {
            return GpuPerformanceTier.Mid;
        }

        return GpuPerformanceTier.Unknown;
    }

    private static int? GuessSeriesNumber(string nameLower, GpuVendor vendor)
    {
        foreach (Match match in Digits.Matches(nameLower))
        {
            if (!int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (vendor == GpuVendor.Nvidia && value is >= 1050 and <= 5090)
            {
                return value;
            }

            if (vendor == GpuVendor.Amd && value is >= 5500 and <= 8990)
            {
                return value;
            }

            if (vendor == GpuVendor.Intel && value is >= 350 and <= 999)
            {
                return value;
            }
        }

        return null;
    }

    private static GpuPerformanceTier ClassifyNvidiaSeries(int series)
    {
        // Keep generation bands separate so 5060 is not treated like 4080+.
        if (series >= 5000)
        {
            if (series >= 5080)
            {
                return GpuPerformanceTier.Ultra;
            }

            if (series >= 5070)
            {
                return GpuPerformanceTier.High;
            }

            return series >= 5060 ? GpuPerformanceTier.Mid : GpuPerformanceTier.Entry;
        }

        if (series >= 4000)
        {
            if (series >= 4080)
            {
                return GpuPerformanceTier.Ultra;
            }

            if (series >= 4070)
            {
                return GpuPerformanceTier.High;
            }

            return series >= 4060 ? GpuPerformanceTier.Mid : GpuPerformanceTier.Entry;
        }

        if (series >= 3000)
        {
            if (series >= 3080)
            {
                return GpuPerformanceTier.High;
            }

            return series >= 3060 ? GpuPerformanceTier.Mid : GpuPerformanceTier.Entry;
        }

        if (series >= 2000)
        {
            return series >= 2070 ? GpuPerformanceTier.Mid : GpuPerformanceTier.Entry;
        }

        return GpuPerformanceTier.Entry;
    }

    private static GpuPerformanceTier ClassifyAmdSeries(int series)
    {
        if (series >= 7900)
        {
            return GpuPerformanceTier.Ultra;
        }

        if (series >= 7700 || series is >= 6800 and < 7000)
        {
            return GpuPerformanceTier.High;
        }

        if (series >= 7600 || series is >= 6600 and < 7000)
        {
            return GpuPerformanceTier.Mid;
        }

        return GpuPerformanceTier.Entry;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    private static int Score(GpuAdapterInfo adapter)
    {
        var score = adapter.Tier switch
        {
            GpuPerformanceTier.Ultra => 400,
            GpuPerformanceTier.High => 300,
            GpuPerformanceTier.Mid => 200,
            GpuPerformanceTier.Entry => 100,
            _ => 0
        };

        score += adapter.Vendor switch
        {
            GpuVendor.Nvidia => 30,
            GpuVendor.Amd => 20,
            GpuVendor.Intel => 5,
            _ => 0
        };

        if (adapter.DedicatedMemoryBytes is > 0)
        {
            score += (int)Math.Min(adapter.DedicatedMemoryBytes.Value / (256L * 1024 * 1024), 40);
        }

        var n = adapter.Name.ToLowerInvariant();
        if (n.Contains("laptop") || n.Contains("mobile") || n.Contains("max-q"))
        {
            score -= 15;
        }

        return score;
    }

    private static GpuRecommendation BuildRecommendation(GpuAdapterInfo adapter)
    {
        // Reuse existing Link + global preset catalogs so UI labels stay consistent.
        var (linkId, globalId, rationale) = adapter.Tier switch
        {
            GpuPerformanceTier.Ultra => (
                "link-quality",
                "global-quality",
                "High-end GPU — Quality Link encode and sharper global SS defaults."),
            GpuPerformanceTier.High => (
                "link-quality",
                "global-quality",
                "Strong GPU (e.g. 4070-class) — Quality Link with HEVC + DBR, SS 1.3 globals."),
            GpuPerformanceTier.Mid => (
                "link-balanced",
                "global-balanced",
                "Mid-range GPU — Balanced Link bitrate/encode and everyday SS defaults."),
            GpuPerformanceTier.Entry => (
                "link-performance",
                "global-performance",
                "Entry GPU — Performance Link (lower encode load) and lighter globals."),
            _ => (
                "link-balanced",
                "global-balanced",
                "GPU tier unclear — Balanced Link / global defaults as a safe starting point.")
        };

        // Prefer Air Link HEVC preset when wireless-friendly mid cards still have NVENC headroom.
        if (adapter.Tier == GpuPerformanceTier.Mid && adapter.Vendor == GpuVendor.Nvidia)
        {
            linkId = "link-air-hevc";
            rationale =
                "Mid-range NVIDIA — Air Link HEVC preset (good wireless encode) with Balanced globals.";
        }

        var linkPreset = LinkPresetCatalog.All.First(p => p.Id == linkId);
        var globalPreset = ProfilePresetCatalog.Find(globalId)
                           ?? ProfilePresetCatalog.GlobalPresets.First();

        return new GpuRecommendation
        {
            Adapter = adapter,
            LinkPresetName = linkPreset.Name,
            GlobalPresetName = globalPreset.Name,
            Link = linkPreset.Settings.Clone(),
            Game = globalPreset.Settings.Clone(),
            Rationale = rationale
        };
    }
}
