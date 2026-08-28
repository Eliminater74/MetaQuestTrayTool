using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class LogService
{
    private const int MaxEntries = 400;
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private readonly object _sync = new();
    private static readonly Regex SensitiveValue = new(
        @"(?<label>\b(?:serial|fingerprint|ssid|wifi(?:\s+ssid)?)(?:\s*[:=]\s*|\s+))(?<value>[^\s,;)\]]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public event EventHandler<LogEntry>? EntryAdded;

    public void Info(string message) => Add("INFO", message);
    public void Warn(string message) => Add("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Add("ERROR", detail);
    }

    /// <summary>Clears the in-memory log and truncates the on-disk log file.</summary>
    public void Clear()
    {
        void Wipe()
        {
            lock (_sync)
            {
                Entries.Clear();
            }

            try
            {
                AppPaths.EnsureAppDataDirectory();
                File.WriteAllText(AppPaths.LogFile, string.Empty);
            }
            catch
            {
                // Logging must never crash the tray app.
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Wipe();
        }
        else
        {
            dispatcher.Invoke(Wipe);
        }
    }

    private void Add(string level, string message)
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = RedactSensitiveData(message)
        };

        void Append()
        {
            lock (_sync)
            {
                Entries.Add(entry);
                while (Entries.Count > MaxEntries)
                {
                    Entries.RemoveAt(0);
                }
            }

            try
            {
                AppPaths.EnsureAppDataDirectory();
                RotateLogIfNeeded();
                File.AppendAllText(AppPaths.LogFile, entry + Environment.NewLine);
            }
            catch
            {
                // Logging must never crash the tray app.
            }

            EntryAdded?.Invoke(this, entry);
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Append();
        }
        else
        {
            dispatcher.Invoke(Append);
        }
    }

    private static string RedactSensitiveData(string message) =>
        SensitiveValue.Replace(message, match => match.Groups["label"].Value + "<redacted>");

    private static void RotateLogIfNeeded()
    {
        if (!File.Exists(AppPaths.LogFile)
            || new FileInfo(AppPaths.LogFile).Length < MaxFileBytes)
        {
            return;
        }

        var backup = AppPaths.LogFile + ".1";
        var older = AppPaths.LogFile + ".2";
        File.Delete(older);
        if (File.Exists(backup))
        {
            File.Move(backup, older);
        }

        File.Move(AppPaths.LogFile, backup);
    }
}
