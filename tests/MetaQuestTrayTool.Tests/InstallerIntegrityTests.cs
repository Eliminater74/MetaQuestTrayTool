using System.IO;
using System.Security.Cryptography;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class InstallerIntegrityTests
{
    [Fact]
    public void RejectsSameLengthTamperingAndLocksVerifiedBytes()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            File.WriteAllText(path, "tampered");
            Assert.Throws<InvalidOperationException>(() => InstallerIntegrity.VerifyAndLock(path, hash, 8));
            File.WriteAllText(path, "original");
            using var locked = InstallerIntegrity.VerifyAndLock(path, hash, 8);
            Assert.Throws<IOException>(() => File.WriteAllText(path, "tampered"));
            Assert.Throws<IOException>(() => File.Delete(path));
        }
        finally { File.Delete(path); }
    }
}
