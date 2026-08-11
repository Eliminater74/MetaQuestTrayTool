using System.Diagnostics;
using System.Windows.Threading;
using MetaQuestTrayTool.Models;
using MetaQuestTrayTool;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Polls running processes and applies the matching game profile.
/// Same idea as OTT's timer-based app watcher.
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

    public void Dispose()
    {
        _timer.Stop();
    }

    private void Poll()
    {
        if (!_app.Settings.Current.AutoApplyProfiles || _app.Profiles.All.Count == 0)
        {
            return;
        }

        try
        {
            var running = Process.GetProcesses()
                .Select(process => process.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_activeProcess is not null && running.Contains(_activeProcess))
            {
                return;
            }

            if (_activeProcess is not null)
            {
                RestoreDefaults(_activeProcess);
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
    }

    private void RestoreDefaults(string processName)
    {
        var summary = _app.RestoreGlobalDefaults();
        _app.Log.Info($"{processName}.exe exited — restored global defaults. {summary}");
        _activeProcess = null;
        _activeProfileName = null;
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
