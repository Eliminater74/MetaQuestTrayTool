using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace MetaQuestTrayTool.Services;

internal static class InstallerIntegrity
{
    internal static string CreatePrivateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("No Windows identity.");
        foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(acl);
        return path;
    }

    // Caller keeps this handle open through Process.Start to prevent replacement or writes.
    internal static FileStream VerifyAndLock(string path, string sha256, long size)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (stream.Length != size || !string.Equals(Convert.ToHexString(SHA256.HashData(stream)),
                    sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Installer changed after download. Download the update again.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
}
