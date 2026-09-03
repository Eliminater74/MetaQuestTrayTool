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
    public void AdbEndpoint_EmbeddedPortIsRevalidated()
    {
        Assert.Equal("192.168.1.40:5556", AdbService.FormatEndpoint("192.168.1.40:5556", 5555));
        Assert.Throws<ArgumentOutOfRangeException>(() => AdbService.FormatEndpoint("192.168.1.40:99999", 5555));
    }

    [Theory]
    [InlineData("192.168.1.40 -s other")]
    [InlineData("192.168.1.40:abc")]
    [InlineData("\"192.168.1.40\"")]
    public void AdbEndpoint_RejectsHostTextThatWouldSplitArguments(string host)
    {
        Assert.Throws<ArgumentException>(() => AdbService.FormatEndpoint(host, 5555));
    }

    [Fact]
    public void AdbScreenshot_ValidatesPngSignature()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var valid = Path.Combine(dir, "valid.png");
            var invalid = Path.Combine(dir, "invalid.png");
            File.WriteAllBytes(
                valid,
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 });
            File.WriteAllText(invalid, "not a png");

            Assert.True(AdbService.IsValidPngFile(valid));
            Assert.False(AdbService.IsValidPngFile(invalid));
            Assert.False(AdbService.IsValidPngFile(Path.Combine(dir, "missing.png")));
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
    public void HeadsetScreenshot_BuildsSafeFileName()
    {
        var capturedAt = new DateTimeOffset(2026, 9, 2, 15, 4, 5, TimeSpan.Zero);

        Assert.Equal(
            "QuestScreenshot-20260902-150405-Meta-Quest-3-Beta.png",
            HeadsetSettingsService.BuildScreenshotFileName(capturedAt, "Meta Quest 3:Beta"));
        Assert.Equal(
            "QuestScreenshot-20260902-150405-Quest-2.png",
            HeadsetSettingsService.BuildScreenshotFileName(capturedAt, "  ", duplicateIndex: 2));
    }

    [Fact]
    public void HotKeySettings_DefaultsIncludeHeadsetScreenshot()
    {
        var binding = Assert.Single(
            HotKeySettings.CreateDefaultBindings(),
            item => item.Action == HotKeyAction.TakeHeadsetScreenshot);

        Assert.Equal(HotKeyModifiers.Control | HotKeyModifiers.Shift, binding.Modifiers);
        Assert.Equal("NumPad9", binding.Key);
    }

    [Theory]
    [InlineData("take screenshot")]
    [InlineData("capture screenshot")]
    [InlineData("please save screenshot")]
    [InlineData("quest screenshot")]
    public void VoicePhraseCatalog_MatchesHeadsetScreenshot(string phrase)
    {
        Assert.True(VoicePhraseCatalog.TryMatch(phrase, out var action));
        Assert.Equal(HotKeyAction.TakeHeadsetScreenshot, action);
    }

    [Theory]
    [InlineData("Access denied while changing OVRService.")]
    [InlineData("Could not change OVRService: timed out")]
    [InlineData("OVRService was not found.")]
    public void OculusRestart_TreatsFailedStopSummariesAsTerminal(string summary)
    {
        Assert.True(OculusRuntimeService.IsServiceActionFailure(summary));
    }

    [Theory]
    [InlineData("OVRService is now Stopped.")]
    [InlineData("OVRService is already stopped.")]
    public void OculusRestart_AllowsSuccessfulStopSummaries(string summary)
    {
        Assert.False(OculusRuntimeService.IsServiceActionFailure(summary));
    }

    [Fact]
    public void HotKeyConflict_IgnoresDisabledVoicePushToTalk()
    {
        var voice = new VoiceSettings
        {
            Enabled = false,
            PushToTalkOnly = true,
            PushToTalkModifiers = HotKeyModifiers.Control,
            PushToTalkKey = "NumPad1"
        };
        var hotKeys = new HotKeySettings
        {
            Enabled = true,
            Bindings =
            [
                new HotKeyBinding
                {
                    Action = HotKeyAction.AswOff,
                    Modifiers = HotKeyModifiers.Control,
                    Key = "NumPad1"
                }
            ]
        };

        Assert.False(HotKeyChordHelper.ConflictsWithHotKeys(voice, hotKeys));
    }

    [Fact]
    public void HotKeyConflict_IgnoresContinuousVoiceMode()
    {
        var voice = new VoiceSettings
        {
            Enabled = true,
            PushToTalkOnly = false,
            PushToTalkModifiers = HotKeyModifiers.Control,
            PushToTalkKey = "NumPad1"
        };
        var hotKeys = new HotKeySettings
        {
            Enabled = true,
            Bindings =
            [
                new HotKeyBinding
                {
                    Action = HotKeyAction.AswOff,
                    Modifiers = HotKeyModifiers.Control,
                    Key = "NumPad1"
                }
            ]
        };

        Assert.False(HotKeyChordHelper.ConflictsWithHotKeys(voice, hotKeys));
    }

    [Fact]
    public void HotKeyConflict_DetectsEnabledPushToTalkCollision()
    {
        var voice = new VoiceSettings
        {
            Enabled = true,
            PushToTalkOnly = true,
            PushToTalkModifiers = HotKeyModifiers.Control,
            PushToTalkKey = "NumPad1"
        };
        var hotKeys = new HotKeySettings
        {
            Enabled = true,
            Bindings =
            [
                new HotKeyBinding
                {
                    Action = HotKeyAction.AswOff,
                    Modifiers = HotKeyModifiers.Control,
                    Key = "NumPad1"
                }
            ]
        };

        Assert.True(HotKeyChordHelper.ConflictsWithHotKeys(voice, hotKeys));
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

    [Fact]
    public void UriLaunchArguments_KeepUrlAsSingleExplorerArgument()
    {
        var arguments = UnelevatedProcessLauncher.BuildUriLaunchArguments(
            "https://example.com/donate?amount=5&note=Quest Link");

        Assert.Equal("\"https://example.com/donate?amount=5&note=Quest Link\"", arguments);
        Assert.DoesNotContain("/c start", arguments, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionHelper_ParentPidArgumentRoundTrips()
    {
        var arguments = SessionHelperHost.BuildArgumentsForParent(12345).Split(' ');

        Assert.Contains(SessionHelperHost.Switch, arguments);
        Assert.Equal(12345, SessionHelperHost.ParseParentProcessId(arguments));
        Assert.Equal(67890, SessionHelperHost.ParseParentProcessId(
        [
            SessionHelperHost.Switch,
            SessionHelperHost.ParentPidSwitch + "=67890"
        ]));
    }

    [Fact]
    public void SessionHelper_RecordedStateTrustsCurrentOrDeadParentOnly()
    {
        var currentExe = Path.Combine(Path.GetTempPath(), "MetaQuestTrayTool.exe");
        var state = new SessionHelperHost.HelperState
        {
            ProcessId = 200,
            ParentProcessId = 100,
            ExecutablePath = currentExe
        };

        Assert.True(SessionHelperClient.IsTrustedRecordedHelperState(
            state,
            currentPid: 100,
            currentProcessPath: currentExe,
            isProcessRunning: _ => true));
        Assert.True(SessionHelperClient.IsTrustedRecordedHelperState(
            state,
            currentPid: 300,
            currentProcessPath: currentExe,
            isProcessRunning: _ => false));
        Assert.False(SessionHelperClient.IsTrustedRecordedHelperState(
            state,
            currentPid: 300,
            currentProcessPath: currentExe,
            isProcessRunning: _ => true));
        Assert.False(SessionHelperClient.IsTrustedRecordedHelperState(
            state with { ProcessId = 300 },
            currentPid: 300,
            currentProcessPath: currentExe,
            isProcessRunning: _ => false));
        Assert.False(SessionHelperClient.IsTrustedRecordedHelperState(
            state with { ParentProcessId = 0 },
            currentPid: 300,
            currentProcessPath: currentExe,
            isProcessRunning: _ => false));
        Assert.False(SessionHelperClient.IsTrustedRecordedHelperState(
            state with { ExecutablePath = Path.Combine(Path.GetTempPath(), "Other.exe") },
            currentPid: 100,
            currentProcessPath: currentExe,
            isProcessRunning: _ => true));
    }

    [Fact]
    public void LogRedaction_CoversHyphenatedWifiAndQuotedValues()
    {
        var redacted = LogService.RedactSensitiveData(
            "serial=ABC123 fingerprint: meta/build/value Wi-Fi SSID: \"Living Room\" Wi\u2011Fi SSID: \u201cQuest Net\u201d");

        Assert.DoesNotContain("ABC123", redacted);
        Assert.DoesNotContain("meta/build/value", redacted);
        Assert.DoesNotContain("Living Room", redacted);
        Assert.DoesNotContain("Quest Net", redacted);
        Assert.Equal(4, redacted.Split("<redacted>").Length - 1);
    }

    [Theory]
    [InlineData("https://github.com/Eliminater74/MetaQuestTrayTool")]
    [InlineData("http://example.com")]
    public void UrlLaunch_AllowsOnlyAbsoluteWebUrls(string url)
    {
        Assert.True(UrlLaunchService.IsAllowedWebUrl(url));
    }

    [Theory]
    [InlineData("httpx://example.com")]
    [InlineData("steam://run/250820")]
    [InlineData("www.example.com")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void UrlLaunch_RejectsNonWebOrRelativeUrls(string url)
    {
        Assert.False(UrlLaunchService.IsAllowedWebUrl(url));
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
