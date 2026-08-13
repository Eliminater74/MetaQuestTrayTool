using System.Diagnostics;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Polls running processes and applies the matching game profile.
/// When the game exits, global defaults are restored and the user is notified.
/// </summary>
public sealed class ProcessWatcherService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private string? _activeProcess;
    private string? _activeProfileName;
    private int _pollGate;

    public ProcessWatcherService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += (_, _) => BeginPoll();
    }

    public string? ActiveProfileName => _activeProfileName;
    public string? ActiveProcessName => _activeProcess;
    public bool IsProfileActive => _activeProcess is not null;

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _app.Log.Info("Profile watcher started.");
        }
    }

    public void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            _app.Log.Info("Profile watcher stopped.");
        }
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
            }
        });
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

            _app.Dispatcher.Invoke(() => ApplyProfile(profile, processName));
            return;
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyProfile(GameProfile profile, string processName)
    {
        var summary = _app.ApplyProfile(profile);
        TrySetPriority(processName, profile.CpuPriority);
        _activeProcess = processName;
        _activeProfileName = profile.Name;
        _app.Log.Info($"Detected {processName}.exe — applied profile '{profile.Name}'. {summary}");
        Notify(
            "Profile applied",
            $"{profile.Name} is now active for {processName}.exe.\nGlobal defaults will return when you close the game.");
    }

    private void RestoreDefaults(string processName)
    {
        var profileName = _activeProfileName ?? "profile";
        _activeProcess = null;
        _activeProfileName = null;
        var summary = _app.RestoreGlobalDefaults();
        _app.Log.Info($"{processName}.exe exited — restored global defaults after '{profileName}'. {summary}");
        Notify(
            "Global defaults restored",
            $"{processName}.exe closed.\nRestored your global VR settings after '{profileName}'.");
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
