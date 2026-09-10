using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class AdbSelectionTests
{
    private static AdbDevice Device(string serial, string state = "device", string model = "Quest 3") =>
        new() { Serial = serial, State = state, Model = model };

    [Fact]
    public void StartInfoUsesArgumentListForOuterAdbTokens()
    {
        var startInfo = AdbService.CreateAdbStartInfo(
            @"C:\Android Tools\adb.exe",
            ["-s", "Quest Serial 123", "shell", "setprop", "debug.oculus.capture.bitrate", "40000000"]);

        Assert.Equal(@"C:\Android Tools\adb.exe", startInfo.FileName);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal("Quest Serial 123", startInfo.ArgumentList[1]);
        Assert.Equal("setprop", startInfo.ArgumentList[3]);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void TrustedSecondQuestWinsRegardlessOfEnumerationOrder()
    {
        var a = Device("QUEST-A"); var b = Device("QUEST-B");
        Assert.Same(b, AdbService.SelectQuest([a, b], "QUEST-B", _ => true, d => d.Serial));
        Assert.Same(b, AdbService.SelectQuest([b, a], "QUEST-B", _ => true, d => d.Serial));
    }

    [Fact]
    public void WirelessHardwareIdentityWinsOverTransportSerial()
    {
        var a = Device("QUEST-A"); var b = Device("192.168.1.10:5555");
        Assert.Same(b, AdbService.SelectQuest([a, b], "QUEST-B", _ => true,
            d => d == b ? "QUEST-B" : d.Serial));
    }

    [Fact]
    public void ReadinessPreferenceNeverSelectsUnrecognizedPhone()
    {
        var offline = Device("QUEST-A", "offline"); var ready = Device("QUEST-B");
        var phone = Device("PHONE", model: "Pixel");
        Assert.Same(ready, AdbService.SelectQuest([phone, offline, ready], null,
            d => d.Model == "Quest 3", d => d.Serial));
        Assert.Null(AdbService.SelectQuest([phone], "PHONE", _ => false, d => d.Serial));
        Assert.Same(offline, AdbService.SelectQuest([ready, offline], "QUEST-A", _ => true, d => d.Serial));
    }
}
