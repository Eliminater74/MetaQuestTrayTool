using System.IO;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>Launch Steam / Meta library titles and arm matching personal profiles (Steam-first).</summary>
public sealed class GameLaunchService
{
    private readonly App _app;

    public GameLaunchService(App app) => _app = app;

    public string LaunchLibraryGame(LibraryGame game, bool ensureProfile = true, bool applyNow = true)
    {
        ArgumentNullException.ThrowIfNull(game);

        GameProfile? profile = null;
        if (ensureProfile && !string.IsNullOrWhiteSpace(game.ProcessName))
        {
            profile = EnsureProfile(game);
        }

        _app.Settings.Current.AutoApplyProfiles = true;
        _app.Settings.Save();

        if (applyNow && profile is not null)
        {
            var applied = _app.ApplyProfile(profile);
            _app.Log.Info($"Armed profile '{profile.Name}' before launch: {applied}");
        }

        if (profile is not null && !string.IsNullOrWhiteSpace(game.ProcessName))
        {
            _app.ProcessWatcher?.ArmActiveProfile(profile, game.ProcessName);
        }

        var launch = StartGame(game);
        _app.TrayNotify("Launch", $"{game.Name}\n{launch}");
        _app.HeadsetAnnouncer.AnnounceGameLaunch(game.Name);
        return launch;
    }

    public string LaunchProfile(GameProfile profile, bool applyNow = true)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _app.Settings.Current.AutoApplyProfiles = true;
        _app.Settings.Save();

        if (applyNow)
        {
            _app.ApplyProfile(profile);
        }

        if (!string.IsNullOrWhiteSpace(profile.ProcessName))
        {
            _app.ProcessWatcher?.ArmActiveProfile(profile, profile.ProcessName);
        }

        // Prefer Steam protocol when we have an AppId.
        if (profile.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(profile.AppId))
        {
            StartSteam(profile.AppId!);
            var msg = $"Launched Steam app {profile.AppId} ({profile.Name}). Profile armed.";
            _app.Log.Info(msg);
            _app.TrayNotify("Launch", msg);
            _app.HeadsetAnnouncer.AnnounceGameLaunch(profile.Name);
            return msg;
        }

        if (!string.IsNullOrWhiteSpace(profile.InstallPath) && !string.IsNullOrWhiteSpace(profile.LaunchFile))
        {
            var exe = Path.Combine(profile.InstallPath!, profile.LaunchFile!);
            if (File.Exists(exe))
            {
                StartExe(exe, profile.InstallPath!);
                var msg = $"Launched {profile.Name} ({exe}). Profile armed.";
                _app.Log.Info(msg);
                _app.TrayNotify("Launch", msg);
                _app.HeadsetAnnouncer.AnnounceGameLaunch(profile.Name);
                return msg;
            }
        }

        // Resolve from live library by AppId / process name.
        var fromLibrary = _app.Library.GetAllGames().FirstOrDefault(game =>
            (!string.IsNullOrWhiteSpace(profile.AppId)
             && string.Equals(game.AppId, profile.AppId, StringComparison.OrdinalIgnoreCase))
            || ProfileService.NormalizeProcessName(game.ProcessName)
               == ProfileService.NormalizeProcessName(profile.ProcessName));

        if (fromLibrary is not null)
        {
            return LaunchLibraryGame(fromLibrary, ensureProfile: false, applyNow: false);
        }

        throw new InvalidOperationException(
            $"Could not launch '{profile.Name}'. Set Steam AppId or InstallPath/LaunchFile, or add it again from the library.");
    }

    private GameProfile EnsureProfile(LibraryGame game)
    {
        var existing = _app.Profiles.FindByProcess(game.ProcessName);
        if (existing is not null)
        {
            existing.AppId ??= game.AppId;
            existing.InstallPath ??= game.InstallPath;
            if (string.IsNullOrWhiteSpace(existing.LaunchFile))
            {
                existing.LaunchFile = game.LaunchFile;
            }

            if (existing.Platform == GamePlatform.Custom)
            {
                existing.Platform = game.Platform;
            }

            _app.Profiles.Save();
            return existing;
        }

        var profile = new GameProfile
        {
            Name = game.Name,
            ProcessName = game.ProcessName,
            Platform = game.Platform,
            Scope = ProfileScope.Personal,
            AppId = game.AppId,
            InstallPath = game.InstallPath,
            LaunchFile = game.LaunchFile,
            Settings = _app.Settings.Current.DefaultGameSettings.Clone(),
            Comments = $"{game.PlatformLabel} library launch"
        };
        var preset = ProfilePresetCatalog.BestGamePresetForProcess(game.ProcessName);
        if (preset is not null)
        {
            ProfilePresetCatalog.ApplyToProfile(profile, preset);
            profile.Comments = preset.Description;
            profile.LaunchFile ??= game.LaunchFile;
            profile.AppId ??= game.AppId;
            profile.InstallPath ??= game.InstallPath;
        }

        _app.Profiles.Add(profile);
        _app.Log.Info($"Created personal profile '{profile.Name}' for launch.");
        return profile;
    }

    private string StartGame(LibraryGame game)
    {
        if (game.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(game.AppId))
        {
            StartSteam(game.AppId!);
            return $"Started Steam title '{game.Name}' (app {game.AppId}).";
        }

        if (!string.IsNullOrWhiteSpace(game.InstallPath) && !string.IsNullOrWhiteSpace(game.LaunchFile))
        {
            var exe = Path.Combine(game.InstallPath!, game.LaunchFile!);
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException($"Launch file not found: {exe}");
            }

            StartExe(exe, game.InstallPath!);
            return $"Started '{game.Name}' ({exe}).";
        }

        throw new InvalidOperationException(
            $"No launch method for '{game.Name}'. Steam needs an AppId; Meta needs InstallPath + LaunchFile.");
    }

    private void StartSteam(string appId)
    {
        var uri = $"steam://run/{appId}";
        SessionHelperClient.EnsureRunning();
        if (SessionHelperClient.TryStartUri(uri, out _))
        {
            return;
        }

        if (!UnelevatedProcessLauncher.TryStartUri(
                uri,
                UnelevatedProcessLauncher.IsCurrentProcessElevated(),
                out var detail))
        {
            throw new InvalidOperationException("Could not start Steam title: " + detail);
        }
    }

    private void StartExe(string exe, string workingDirectory)
    {
        SessionHelperClient.EnsureRunning();
        if (SessionHelperClient.TryStartExe(exe, arguments: null, workingDirectory, out _))
        {
            return;
        }

        if (!UnelevatedProcessLauncher.TryStart(
                exe,
                arguments: null,
                workingDirectory,
                UnelevatedProcessLauncher.IsCurrentProcessElevated(),
                out var detail))
        {
            throw new InvalidOperationException("Could not start game: " + detail);
        }
    }
}
