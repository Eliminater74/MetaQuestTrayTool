using System.Diagnostics;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Polls running processes and applies the matching game profile.
/// When the game exits, global defaults are restored and the user is notified.
/// Idle: slow full-process scans; Active profile: cheap exit check only.
/// </summary>
public sealed class ProcessWatcherService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private string? _activeProcess;
    private string? _activeProfileName;
    private bool _awaitingLaunchProcess;
    private DateTime _armedAwaitingProcessUtc;
    private int _pollGate;
    private static readonly TimeSpan LaunchProcessGrace = TimeSpan.FromSeconds(90);

    public ProcessWatcherService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = IdleCadence.HeavyIdle
        };
        _timer.Tick += (_, _) => BeginPoll();
    }

    public string? ActiveProfileName => _activeProfileName;
    public string? ActiveProcessName => _activeProcess;
    public bool IsProfileActive => _activeProcess is not null;

    public GameProfile? GetActiveProfile()
    {
        if (_activeProcess is null)
        {
            return null;
        }

        return _app.Profiles.FindByProcess(_activeProcess)
               ?? _app.Profiles.All.FirstOrDefault(p =>
                   string.Equals(p.Name, _activeProfileName, StringComparison.OrdinalIgnoreCase));
    }

    public void Start()
    {
        if (!_app.Settings.Current.AutoApplyProfiles)
        {
            Stop();
            return;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _app.Log.Info("Profile watcher started.");
        }

        ApplyCadence();
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            _app.Log.Info("Profile watcher stopped.");
        }
    }

    /// <summary>
    /// Keep this profile latched until the launched process exits (Steam may take a while to spawn).
    /// </summary>
    public bool ArmActiveProfile(GameProfile profile, string processName)
    {
        var name = ProfileService.NormalizeProcessName(processName);
        if (name.Length == 0)
        {
            return false;
        }

        if (_activeProcess is not null)
        {
            if (_awaitingLaunchProcess
                && string.Equals(_activeProcess, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_activeProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_awaitingLaunchProcess
                || string.Equals(_activeProcess, name, StringComparison.OrdinalIgnoreCase)
                || IsProcessRunning(_activeProcess))
            {
                _app.Log.Warn(
                    $"Cannot arm profile '{profile.Name}' for {name}.exe while '{_activeProfileName ?? _activeProcess}' is active.");
                return false;
            }

            // The previous process ended between polls. Restore its baseline before
            // replacing the stale watcher state with this launch.
            try
            {
                _app.RestoreGlobalDefaults();
            }
            catch (Exception ex)
            {
                _app.Log.Warn($"Could not restore stale profile before a new launch: {ex.Message}");
            }

            _activeProcess = null;
            _activeProfileName = null;
        }

        _activeProcess = name;
        _activeProfileName = profile.Name;
        _awaitingLaunchProcess = true;
        _armedAwaitingProcessUtc = DateTime.UtcNow;
        Start();
        ApplyCadence();
        _app.Log.Info($"Library launch armed profile '{profile.Name}' for {name}.exe until it exits.");
        return true;
    }

    /// <summary>
    /// Cancel a profile armed for a launch that failed before its process appeared.
    /// This prevents a failed launch from leaving profile settings active for the
    /// full launch grace period.
    /// </summary>
    public string CancelArmedProfile()
    {
        if (!_awaitingLaunchProcess || _activeProcess is null)
        {
            return string.Empty;
        }

        var processName = _activeProcess;
        var profileName = _activeProfileName ?? "profile";
        _activeProcess = null;
        _activeProfileName = null;
        _awaitingLaunchProcess = false;
        var summary = _app.RestoreGlobalDefaults();
        _app.Log.Info(
            $"Launch of {processName}.exe failed before process start — cancelled profile '{profileName}' and restored globals. {summary}");
        ApplyCadence();
        return summary;
    }

    public void Dispose() => _timer.Stop();

    private void BeginPoll()
    {
        if (Interlocked.Exchange(ref _pollGate, 1) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                _app.Dispatcher.BeginInvoke(() =>
                    _app.Log.Error("Profile watcher failed while scanning processes.", ex));
            }
            finally
            {
                Interlocked.Exchange(ref _pollGate, 0);
                _app.Dispatcher.BeginInvoke(ApplyCadence);
            }
        });
    }

    private void ApplyCadence()
    {
        if (!_app.Settings.Current.AutoApplyProfiles)
        {
            IdleCadence.Set(_timer, IdleCadence.HeavyIdle);
            return;
        }

        IdleCadence.Set(_timer, _activeProcess is not null ? IdleCadence.Active : IdleCadence.HeavyIdle);
    }

    private void Poll()
    {
        if (!_app.Settings.Current.AutoApplyProfiles)
        {
            return;
        }

        if (_activeProcess is not null)
        {
            if (IsProcessRunning(_activeProcess))
            {
                _awaitingLaunchProcess = false;
                return;
            }

            if (_awaitingLaunchProcess
                && DateTime.UtcNow - _armedAwaitingProcessUtc < LaunchProcessGrace)
            {
                return;
            }

            _app.Dispatcher.Invoke(() => RestoreDefaults(_activeProcess!));
            return;
        }

        var profiles = _app.Profiles.All;
        if (profiles.Count == 0)
        {
            return;
        }

        // One process snapshot for all profile names — cheaper than GetProcessesByName × N.
        HashSet<string> running;
        try
        {
            running = Process.GetProcesses()
                .Select(process =>
                {
                    try
                    {
                        return process.ProcessName;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return;
        }

        foreach (var profile in profiles)
        {
            var processName = ProfileService.NormalizeProcessName(profile.ProcessName);
            if (processName.Length == 0 || !running.Contains(processName))
            {
                continue;
            }

            if (IsIgnored(processName))
            {
                continue;
            }

            _app.Dispatcher.Invoke(() => ApplyProfile(profile, processName));
            return;
        }
    }

    private bool IsIgnored(string processName)
    {
        var ignored = _app.Settings.Current.ProfileIgnoreProcesses;
        if (ignored is null || ignored.Count == 0)
        {
            return false;
        }

        var normalized = ProfileService.NormalizeProcessName(processName);
        return ignored.Any(entry =>
            string.Equals(ProfileService.NormalizeProcessName(entry), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private void ApplyProfile(GameProfile profile, string processName)
    {
        string summary;
        try
        {
            summary = _app.ApplyProfile(profile);
        }
        catch (Exception ex)
        {
            _app.Log.Error($"Detected {processName}.exe — profile '{profile.Name}' failed to apply.", ex);
            Notify("Profile apply failed", $"{profile.Name} could not be applied to {processName}.exe.");
            _app.HeadsetAnnouncer.AnnounceProfileApplyFailed(profile.Name);
            return;
        }

        TrySetPriority(processName, profile.CpuPriority);
        _activeProcess = processName;
        _activeProfileName = profile.Name;
        _app.Log.Info($"Detected {processName}.exe — applied profile '{profile.Name}'. {summary}");
        Notify(
            "Profile applied",
            $"{profile.Name} is now active for {processName}.exe.\nGlobal defaults will return when you close the game.");
        _app.HeadsetAnnouncer.AnnounceProfileDetected(profile.Name, summary);
        ApplyCadence();
    }

    private void RestoreDefaults(string processName)
    {
        var profileName = _activeProfileName ?? "profile";
        _activeProcess = null;
        _activeProfileName = null;
        _awaitingLaunchProcess = false;
        string summary;
        try
        {
            summary = _app.RestoreGlobalDefaults();
        }
        catch (Exception ex)
        {
            _app.Log.Error($"Could not restore global defaults after '{profileName}'.", ex);
            Notify("Global restore failed", $"Could not restore global defaults after '{profileName}'.");
            _app.HeadsetAnnouncer.AnnounceProfileRestoreFailed(profileName);
            ApplyCadence();
            return;
        }

        _app.Log.Info($"{processName}.exe exited — restored global defaults after '{profileName}'. {summary}");
        Notify(
            "Global defaults restored",
            $"{processName}.exe closed.\nRestored your global VR settings after '{profileName}'.");
        _app.HeadsetAnnouncer.AnnounceProfileRestored(profileName, summary);
        ApplyCadence();
    }

    private void Notify(string title, string message)
    {
        if (!_app.Settings.Current.ShowNotifications)
        {
            return;
        }

        _app.TrayNotify(title, message);
    }

    private void TrySetPriority(string processName, string priorityName)
    {
        if (string.Equals(priorityName, "Normal", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(priorityName))
        {
            return;
        }

        ProcessPriorityClass priority = priorityName switch
        {
            "High" => ProcessPriorityClass.High,
            "AboveNormal" => ProcessPriorityClass.AboveNormal,
            _ => ProcessPriorityClass.Normal
        };

        if (priority == ProcessPriorityClass.Normal)
        {
            return;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    process.PriorityClass = priority;
                }
            }

            _app.Log.Info($"Set {processName}.exe priority to {priorityName}.");
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Could not change CPU priority for {processName}.exe: {ex.Message}");
        }
    }
}
