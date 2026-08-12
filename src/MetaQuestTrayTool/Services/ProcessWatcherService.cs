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

    public ProcessWatcherService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += (_, _) => Poll();
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

    private void Poll()
    {
        if (!_app.Settings.Current.AutoApplyProfiles)
        {
            return;
        }

        try
        {
            var running = Process.GetProcesses()
                .Select(process => process.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_activeProcess is not null)
            {
                if (running.Contains(_activeProcess))
                {
                    return;
                }

                RestoreDefaults(_activeProcess);
                return;
            }

            if (_app.Profiles.All.Count == 0)
            {
                return;
            }

            foreach (var profile in _app.Profiles.All)
            {
                var processName = ProfileService.NormalizeProcessName(profile.ProcessName);
                if (processName.Length == 0 || !running.Contains(processName))
                {
                    continue;
                }

                ApplyProfile(profile, processName);
                return;
            }
        }
        catch (Exception ex)
        {
            _app.Log.Error("Profile watcher failed while scanning processes.", ex);
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
