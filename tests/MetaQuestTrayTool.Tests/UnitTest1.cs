using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class RuntimeSafetyTests
{
    [Fact]
    public void DebugToolResult_OnlySucceedsOnCleanExit()
    {
        var result = new DebugToolApplyResult
        {
            CliFound = true,
            Started = true,
            ExitCode = 0
        };

        Assert.True(result.Succeeded);
        Assert.False(new DebugToolApplyResult
        {
            CliFound = true,
            Started = true,
            ExitCode = 0,
            TimedOut = true
        }.Succeeded);
        Assert.False(new DebugToolApplyResult
        {
            CliFound = true,
            Started = true,
            ExitCode = 1
        }.Succeeded);
        Assert.False(new DebugToolApplyResult
        {
            CliFound = true,
            Started = true,
            ExitCode = 0,
            LooksRejected = true
        }.Succeeded);
    }

    [Fact]
    public void GameProfile_CloneDoesNotShareMutableState()
    {
        var original = new GameProfile
        {
            Name = "Original",
            ProcessName = "Game",
            Settings = new GameSettings { SuperSampling = 1.2 },
            Link = new LinkProfileOverrides { BitrateMbps = 500 },
            CustomCommands = new CustomCommandSet
            {
                CliCommands = ["one"],
                AdbCommands = ["two"]
            }
        };

        var clone = original.Clone();
        clone.Settings.SuperSampling = 1.5;
        clone.Link.BitrateMbps = 900;
        clone.CustomCommands.CliCommands.Add("three");

        Assert.Equal(1.2, original.Settings.SuperSampling);
        Assert.Equal(500, original.Link.BitrateMbps);
        Assert.Single(original.CustomCommands.CliCommands);
    }

    [Fact]
    public void GameProfile_CopyFromReplacesNestedValues()
    {
        var target = new GameProfile { Name = "Old", Settings = new GameSettings { SuperSampling = 1.0 } };
        var source = new GameProfile
        {
            Name = "New",
            ProcessName = "NewGame",
            Settings = new GameSettings { SuperSampling = 1.7 },
            CustomCommands = new CustomCommandSet { AdbCommands = ["settings put system foo bar"] }
        };

        target.CopyFrom(source);

        Assert.Equal("New", target.Name);
        Assert.Equal("NewGame", target.ProcessName);
        Assert.Equal(1.7, target.Settings.SuperSampling);
        Assert.Equal("settings put system foo bar", Assert.Single(target.CustomCommands.AdbCommands));
        Assert.NotSame(source.Settings, target.Settings);
    }

    [Fact]
    public void CustomCommandSet_ParseLinesRemovesCommentsAndBlankLines()
    {
        var commands = CustomCommandSet.ParseLines("  first  \r\n# comment\n\n second");

        Assert.Equal(["first", "second"], commands);
    }

    [Fact]
    public void AdbUpdateCleanup_OnlyOwnsPackagedAdb()
    {
        var baseDir = AppContext.BaseDirectory;

        Assert.True(AdbService.IsBundledAdbExecutable(
            Path.Combine(baseDir, "platform-tools", "adb.exe")));
        Assert.True(AdbService.IsBundledAdbExecutable(
            Path.Combine(baseDir, "adb.exe")));

        Assert.False(AdbService.IsBundledAdbExecutable(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Android",
                "Sdk",
                "platform-tools",
                "adb.exe")));
        Assert.False(AdbService.IsBundledAdbExecutable(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SideQuest",
                "resources",
                "app.asar.unpacked",
                "build",
                "platform-tools",
                "adb.exe")));
    }

    [Fact]
    public void ProfileStore_LoadKeepsIntentionalEmptyPrimary()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "profiles.json");
            var store = new ProfileStore(path);

            store.Save(
            [
                new GameProfile { Name = "Old profile", ProcessName = "OldGame" }
            ]);
            store.Save([]);

            var loaded = store.Load();

            Assert.Empty(loaded);
            Assert.False(store.RestoredFromBackup);
            Assert.False(store.LastLoadFailed);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ProfileStore_LoadRestoresCorruptPrimaryFromBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "profiles.json");
            File.WriteAllText(path, "{");
            File.WriteAllText(path + ".bak", """[{"Name":"Recovered","ProcessName":"RecoveredGame"}]""");
            var store = new ProfileStore(path);

            var loaded = store.Load();

            var profile = Assert.Single(loaded);
            Assert.Equal("Recovered", profile.Name);
            Assert.True(store.RestoredFromBackup);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3-beta", 1, 2, 3)]
    public void UpdateVersionParser_ParsesReleaseVersions(string input, int major, int minor, int build)
    {
        var version = UpdateService.ParseVersion(input);

        Assert.NotNull(version);
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1.2")]
    public void UpdateVersionParser_RejectsInvalidVersions(string? input)
    {
        Assert.Null(UpdateService.ParseVersion(input));
    }
}
