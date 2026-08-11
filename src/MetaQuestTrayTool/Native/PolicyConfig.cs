using System.Runtime.InteropServices;

namespace MetaQuestTrayTool.Native;

/// <summary>
/// Undocumented Windows PolicyConfig COM used by the Sound control panel
/// to change the default audio endpoint. Same approach OTT used.
/// </summary>
internal static class PolicyConfig
{
    public enum ERole : uint
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    public static void SetDefaultEndpoint(string deviceId, bool includeCommunications)
    {
        object? comObject = null;
        try
        {
            comObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"))
                                                 ?? throw new InvalidOperationException("PolicyConfig COM class was not found."));

            if (comObject is not IPolicyConfig policy)
            {
                throw new InvalidOperationException("Could not cast PolicyConfig COM object to IPolicyConfig.");
            }

            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Console));
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Multimedia));
            if (includeCommunications)
            {
                Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Communications));
            }
        }
        finally
        {
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(IntPtr unused1, IntPtr unused2);
        [PreserveSig] int GetDeviceFormat(IntPtr unused1, IntPtr unused2, IntPtr unused3);
        [PreserveSig] int ResetDeviceFormat(IntPtr unused1);
        [PreserveSig] int SetDeviceFormat(IntPtr unused1, IntPtr unused2, IntPtr unused3);
        [PreserveSig] int GetProcessingPeriod(IntPtr unused1, IntPtr unused2, IntPtr unused3, IntPtr unused4);
        [PreserveSig] int SetProcessingPeriod(IntPtr unused1, IntPtr unused2);
        [PreserveSig] int GetShareMode(IntPtr unused1, IntPtr unused2);
        [PreserveSig] int SetShareMode(IntPtr unused1, IntPtr unused2);
        [PreserveSig] int GetPropertyValue(IntPtr unused1, IntPtr unused2, IntPtr unused3);
        [PreserveSig] int SetPropertyValue(IntPtr unused1, IntPtr unused2, IntPtr unused3);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        [PreserveSig] int SetEndpointVisibility(IntPtr unused1, IntPtr unused2);
    }
}
