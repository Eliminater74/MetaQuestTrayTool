using System.Runtime.InteropServices;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Queries and sets advanced color through the documented DisplayConfig APIs.
/// <c>DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO</c> (9) reports support and
/// whether advanced color is enabled. <c>DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE</c> (10)
/// sets that state. <c>DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE</c> is not used; its packet
/// layout is not the documented advanced-color structure.
/// </summary>
public sealed class WindowsDisplayConfigHdrApi : IWindowsHdrDisplay
{
    private const uint QdcOnlyActivePaths = 0x00000002;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int GetAdvancedColorInfo = 9;
    private const int SetAdvancedColorState = 10;
    private const int GetTargetName = 2;

    public bool TryQueryActive(out IReadOnlyList<HdrDisplayState> displays, out string? error)
    {
        displays = Array.Empty<HdrDisplayState>();
        error = null;
        try
        {
            var paths = QueryActivePaths(out error);
            if (paths is null)
            {
                return false;
            }

            var found = new List<HdrDisplayState>();
            var seen = new HashSet<HdrDisplayId>();
            string? lastError = null;
            foreach (var path in paths)
            {
                var id = new HdrDisplayId(
                    HdrDisplayId.PackAdapter(path.targetInfo.adapterId.LowPart, path.targetInfo.adapterId.HighPart),
                    path.targetInfo.id);
                if (!seen.Add(id))
                {
                    continue;
                }

                if (!TryGetAdvancedColor(path.targetInfo.adapterId, path.targetInfo.id, out var supported, out var enabled, out var colorError))
                {
                    lastError = colorError;
                    continue;
                }

                found.Add(new HdrDisplayState(
                    id,
                    ReadFriendlyName(path.targetInfo.adapterId, path.targetInfo.id, id.TargetId),
                    supported,
                    enabled));
            }

            if (found.Count == 0 && lastError is not null)
            {
                error = lastError;
                return false;
            }

            displays = found;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetAdvancedColor(HdrDisplayId id, bool enabled, out string? error)
    {
        error = null;
        try
        {
            var packet = new DisplayConfigSetAdvancedColorState
            {
                header = new DisplayConfigDeviceInfoHeader
                {
                    type = SetAdvancedColorState,
                    size = (uint)Marshal.SizeOf<DisplayConfigSetAdvancedColorState>(),
                    adapterId = new Luid
                    {
                        LowPart = id.AdapterLowPart,
                        HighPart = id.AdapterHighPart
                    },
                    id = id.TargetId
                },
                value = enabled ? 1u : 0u
            };

            var result = DisplayConfigSetDeviceInfo(ref packet);
            if (result == ErrorSuccess)
            {
                return true;
            }

            error = "Win32 error " + result;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static DisplayConfigPathInfo[]? QueryActivePaths(out string? error)
    {
        error = null;
        var sizeResult = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount);
        if (sizeResult != ErrorSuccess)
        {
            error = "Win32 error " + sizeResult;
            return null;
        }

        if (pathCount == 0)
        {
            return Array.Empty<DisplayConfigPathInfo>();
        }

        var paths = new DisplayConfigPathInfo[pathCount];
        var modes = new DisplayConfigModeInfo[Math.Max(modeCount, 1)];
        var queryResult = QueryDisplayConfig(
            QdcOnlyActivePaths,
            ref pathCount,
            paths,
            ref modeCount,
            modes,
            IntPtr.Zero);
        if (queryResult == ErrorInsufficientBuffer)
        {
            sizeResult = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out pathCount, out modeCount);
            if (sizeResult != ErrorSuccess)
            {
                error = "Win32 error " + sizeResult;
                return null;
            }

            paths = new DisplayConfigPathInfo[pathCount];
            modes = new DisplayConfigModeInfo[Math.Max(modeCount, 1)];
            queryResult = QueryDisplayConfig(
                QdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);
        }

        if (queryResult != ErrorSuccess)
        {
            error = "Win32 error " + queryResult;
            return null;
        }

        if (pathCount > paths.Length)
        {
            error = "Display configuration returned more paths than requested.";
            return null;
        }

        if (pathCount == paths.Length)
        {
            return paths;
        }

        var trimmed = new DisplayConfigPathInfo[pathCount];
        Array.Copy(paths, trimmed, pathCount);
        return trimmed;
    }

    private static bool TryGetAdvancedColor(Luid adapterId, uint targetId, out bool supported, out bool enabled, out string? error)
    {
        supported = false;
        enabled = false;
        var packet = new DisplayConfigGetAdvancedColorInfo
        {
            header = new DisplayConfigDeviceInfoHeader
            {
                type = GetAdvancedColorInfo,
                size = (uint)Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(),
                adapterId = adapterId,
                id = targetId
            }
        };

        var result = DisplayConfigGetDeviceInfo(ref packet);
        if (result != ErrorSuccess)
        {
            error = "Win32 error " + result;
            return false;
        }

        supported = (packet.value & 0x1) != 0;
        enabled = (packet.value & 0x2) != 0;
        error = null;
        return true;
    }

    private static string ReadFriendlyName(Luid adapterId, uint targetId, uint fallbackId)
    {
        try
        {
            var packet = new DisplayConfigTargetDeviceName
            {
                header = new DisplayConfigDeviceInfoHeader
                {
                    type = GetTargetName,
                    size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };
            if (DisplayConfigGetDeviceInfo(ref packet) == ErrorSuccess
                && !string.IsNullOrWhiteSpace(packet.monitorFriendlyDeviceName))
            {
                return packet.monitorFriendlyDeviceName.Trim();
            }
        }
        catch
        {
            // The target id is enough to log and restore.
        }

        return "display target " + fallbackId;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DisplayConfigSetAdvancedColorState setPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public int type;
        public uint size;
        public Luid adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigGetAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigSetAdvancedColorState
    {
        public DisplayConfigDeviceInfoHeader header;
        public uint value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DisplayConfigRational refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo sourceInfo;
        public DisplayConfigPathTargetInfo targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DisplayConfigModeInfo
    {
        public uint infoType;
        public uint id;
        public Luid adapterId;
        public ulong reserved0;
        public ulong reserved1;
        public ulong reserved2;
        public ulong reserved3;
        public ulong reserved4;
        public uint reserved5;
    }
}
