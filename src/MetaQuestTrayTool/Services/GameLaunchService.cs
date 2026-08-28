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
        _app.ExperimentalMsfsVr.CancelScheduledToggle();

        GameProfile? profile = null;
        if (ensureProfile && !string.IsNullOrWhiteSpace(game.ProcessName))
        {
            profile = EnsureProfile(game);
        }

        _app.Settings.Current.AutoApplyProfiles = true;
        _app.Settings.Save();

        try
        {
            if (profile is not null && !string.IsNullOrWhiteSpace(game.ProcessName)
                && (_app.ProcessWatcher is null
                    || !_app.ProcessWatcher.ArmActiveProfile(profile, game.ProcessName)))
            {
                throw new InvalidOperationException(
                    $"Cannot launch '{game.Name}' while another game profile is active. Close the active game first.");
            }

            if (applyNow && profile is not null)
            {
                var applied = _app.ApplyProfile(profile);
                _app.Log.Info($"Armed profile '{profile.Name}' before launch: {applied}");
            }

            if (profile?.ExperimentalMsfsVr == true)
            {
                var preparation = _app.ExperimentalMsfsVr.Prepare(profile);
                if (!preparation.Succeeded)
                {
                    throw new InvalidOperationException(preparation.Summary);
                }
            }

            var launch = StartGame(game, EffectiveLaunchArguments(profile));
            _app.TrayNotify("Launch", $"{game.Name}\n{launch}");
            _app.HeadsetAnnouncer.AnnounceGameLaunch(
                game.Name,
                profile?.Name,
                game.PlatformLabel);
            if (profile is not null)
            {
                _app.ExperimentalMsfsVr.ScheduleToggle(profile);
            }
            return launch;
        }
        catch
        {
            _app.ProcessWatcher?.CancelArmedProfile();
            _app.HeadsetAnnouncer.AnnounceGameLaunchFailed(game.Name, profile?.Name);
            throw;
        }
    }

    public string LaunchProfile(GameProfile profile, bool applyNow = true)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (ProfileService.NormalizeProcessName(profile.ProcessName).Length == 0)
        {
            throw new InvalidOperationException(
                $"Cannot launch profile '{profile.Name}' without a process name.");
        }

        _app.ExperimentalMsfsVr.CancelScheduledToggle();

        _app.Settings.Current.AutoApplyProfiles = true;
        _app.Settings.Save();

        var delegatedToLibrary = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(profile.ProcessName)
                && (_app.ProcessWatcher is null
                    || !_app.ProcessWatcher.ArmActiveProfile(profile, profile.ProcessName)))
            {
                throw new InvalidOperationException(
                    $"Cannot launch '{profile.Name}' while another game profile is active. Close the active game first.");
            }

            if (applyNow)
            {
                _app.ApplyProfile(profile);
            }

            if (profile.ExperimentalMsfsVr)
            {
                var preparation = _app.ExperimentalMsfsVr.Prepare(profile);
                if (!preparation.Succeeded)
                {
                    throw new InvalidOperationException(preparation.Summary);
                }
            }

            // Prefer Steam protocol when we have an AppId.
            if (profile.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(profile.AppId))
            {
                StartSteam(profile.AppId!, EffectiveLaunchArguments(profile));
                var msg = $"Launched Steam app {profile.AppId} ({profile.Name}). Profile armed.";
                _app.Log.Info(msg);
                _app.TrayNotify("Launch", msg);
                _app.HeadsetAnnouncer.AnnounceGameLaunch(
                    profile.Name,
                    profile.Name,
                    DescribePlatform(profile.Platform));
                _app.ExperimentalMsfsVr.ScheduleToggle(profile);
                return msg;
            }

            if (!string.IsNullOrWhiteSpace(profile.InstallPath) && !string.IsNullOrWhiteSpace(profile.LaunchFile))
            {
                if (!TryResolveLaunchExecutable(profile.InstallPath, profile.LaunchFile, out var exe, out var pathError))
                {
                    throw new InvalidOperationException(pathError);
                }

                StartExe(exe, profile.InstallPath!, EffectiveLaunchArguments(profile));
                var msg = $"Launched {profile.Name} ({exe}). Profile armed.";
                _app.Log.Info(msg);
                _app.TrayNotify("Launch", msg);
                _app.HeadsetAnnouncer.AnnounceGameLaunch(
                    profile.Name,
                    profile.Name,
                    DescribePlatform(profile.Platform));
                _app.ExperimentalMsfsVr.ScheduleToggle(profile);
                return msg;
            }

            // Resolve from live library by AppId / process name.
            var fromLibrary = _app.Library.GetAllGames().FirstOrDefault(game =>
                (!string.IsNullOrWhiteSpace(profile.AppId)
                 && string.Equals(game.AppId, profile.AppId, StringComparison.OrdinalIgnoreCase))
                || ProfileService.NormalizeProcessName(game.ProcessName)
                   == ProfileService.NormalizeProcessName(profile.ProcessName));

            if (fromLibrary is not null)
            {
                delegatedToLibrary = true;
                return LaunchLibraryGame(fromLibrary, ensureProfile: true, applyNow: false);
            }

            throw new InvalidOperationException(
                $"Could not launch '{profile.Name}'. Set Steam AppId or InstallPath/LaunchFile, or add it again from the library.");
        }
        catch
        {
            if (!delegatedToLibrary)
            {
                _app.ProcessWatcher?.CancelArmedProfile();
                _app.HeadsetAnnouncer.AnnounceGameLaunchFailed(profile.Name, profile.Name);
            }

            throw;
        }
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

    private string StartGame(LibraryGame game, string? launchArguments)
    {
        if (game.Platform == GamePlatform.Steam && !string.IsNullOrWhiteSpace(game.AppId))
        {
            StartSteam(game.AppId!, launchArguments);
            return $"Started Steam title '{game.Name}' (app {game.AppId}).";
        }

        if (!string.IsNullOrWhiteSpace(game.InstallPath) && !string.IsNullOrWhiteSpace(game.LaunchFile))
        {
            if (!TryResolveLaunchExecutable(game.InstallPath, game.LaunchFile, out var exe, out var pathError))
            {
                throw new InvalidOperationException(pathError);
            }

            StartExe(exe, game.InstallPath!, launchArguments);
            return $"Started '{game.Name}' ({exe}).";
        }

        throw new InvalidOperationException(
            $"No launch method for '{game.Name}'. Steam needs an AppId; Meta needs InstallPath + LaunchFile.");
    }

    private void StartSteam(string appId, string? launchArguments = null)
    {
        var args = NormalizeLaunchArguments(launchArguments);
        var uri = args.Length == 0
            ? $"steam://run/{appId}"
            : $"steam://run/{appId}//{Uri.EscapeDataString(args)}";
        if (!SessionHelperClient.TryLaunchUri(uri, out var detail))
        {
            throw new InvalidOperationException("Could not start Steam title: " + detail);
        }
    }

    private void StartExe(string exe, string workingDirectory, string? launchArguments = null)
    {
        if (!SessionHelperClient.TryLaunchExe(
                exe,
                NormalizeLaunchArguments(launchArguments),
                workingDirectory,
                out var detail))
        {
            throw new InvalidOperationException("Could not start game: " + detail);
        }
    }

    private static string DescribePlatform(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => "Steam",
        GamePlatform.Meta => "Meta",
        _ => "Custom"
    };

    private static string? EffectiveLaunchArguments(GameProfile? profile)
    {
        if (profile is null)
        {
            return null;
        }

        var arguments = NormalizeLaunchArguments(profile.LaunchArguments);
        return arguments.Length > 0
            ? arguments
            : profile.ExperimentalMsfsVr
                ? "-FastLaunch"
                : null;
    }

    private static string NormalizeLaunchArguments(string? arguments) =>
        (arguments ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    private static bool TryResolveLaunchExecutable(
        string? installPath,
        string? launchFile,
        out string executable,
        out string error)
    {
        executable = string.Empty;
        error = "Install path and launch file are required.";
        if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(launchFile))
        {
            return false;
        }

        var relativeFile = launchFile.Trim();
        if (Path.IsPathRooted(relativeFile))
        {
            error = "Launch file must be a relative .exe under the install path.";
            return false;
        }

        try
        {
            var root = Path.GetFullPath(installPath.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(root, relativeFile));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "Launch file must stay inside the install path.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(candidate), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                error = "Launch file must be an .exe under the install path.";
                return false;
            }

            if (!File.Exists(candidate))
            {
                error = $"Launch file not found: {candidate}";
                return false;
            }

            executable = candidate;
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            error = "Launch file path is invalid: " + ex.Message;
            return false;
        }
    }
}
