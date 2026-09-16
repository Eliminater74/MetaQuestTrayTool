using System.Collections.Generic;
using System.IO;
using System.Text;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public enum SessionTraceKind
{
    State,
    Mutation
}

public sealed record SessionTraceEvent(
    DateTimeOffset Utc,
    DateTimeOffset Local,
    SessionTraceKind Kind,
    string Category,
    string Action,
    string Detail,
    string? Caller)
{
    public string ToLogLine()
    {
        var caller = string.IsNullOrWhiteSpace(Caller) ? string.Empty : $" caller={Caller}";
        return $"{Utc:yyyy-MM-ddTHH:mm:ss.fffZ} | {Local:yyyy-MM-dd HH:mm:ss.fff zzz} | {Kind.ToString().ToUpperInvariant()} | {Category} | {Action} | {Detail}{caller}";
    }
}

/// <summary>
/// Bounded Link-session timeline for freeze diagnosis. Logs state changes and mutating
/// actions only — never writes Link/ODT/ADB/OVRService state itself.
/// </summary>
public sealed class SessionFlightRecorder
{
    public const int Capacity = 256;
    public const string LogPrefix = "[TRACE]";

    public static SessionFlightRecorder? Current { get; set; }

    private readonly LogService? _log;
    private readonly string? _filePath;
    private readonly object _sync = new();
    private readonly Queue<SessionTraceEvent> _events = new();
    private string? _lastFingerprint;
    private string? _lastAdbSerial;

    public SessionFlightRecorder(LogService? log = null, string? filePath = null)
    {
        _log = log;
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(AppPaths.AppDataDirectory, "session-trace.log");

    public IReadOnlyList<SessionTraceEvent> Snapshot()
    {
        lock (_sync)
        {
            return _events.ToArray();
        }
    }

    public string FormatSnapshot()
    {
        var events = Snapshot();
        if (events.Count == 0)
        {
            return "No session-trace events yet.";
        }

        var text = new StringBuilder();
        foreach (var item in events)
        {
            text.AppendLine(item.ToLogLine());
        }

        return text.ToString().TrimEnd();
    }

    public static void ObserveLink(VrConnectionStatus status, string? caller = null) =>
        Current?.ObserveLinkCore(status, caller);

    public static void ObserveAdb(string? serial, bool wireless, string? caller = null) =>
        Current?.ObserveAdbCore(serial, wireless, caller);

    public static void Mutation(string category, string action, string detail, string? caller = null) =>
        Current?.Record(SessionTraceKind.Mutation, category, action, detail, caller);

    public static void State(string category, string action, string detail, string? caller = null) =>
        Current?.Record(SessionTraceKind.State, category, action, detail, caller);

    internal void ObserveLinkCore(VrConnectionStatus status, string? caller)
    {
        var fingerprint = LinkSessionWatchService.BuildFingerprint(status);
        string? previous;
        lock (_sync)
        {
            if (string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            previous = _lastFingerprint;
            _lastFingerprint = fingerprint;
        }

        var streaming = status.MetaLinkStreaming ? "streaming" : "not-streaming";
        var air = status.IsUsingAirLink switch
        {
            true => "air",
            false => "wired",
            _ => "unknown"
        };
        Record(
            SessionTraceKind.State,
            "link-fingerprint",
            previous is null ? "initial" : "change",
            $"from={previous ?? "(none)"} to={fingerprint} kind={status.Kind} "
            + $"sessionActive={status.SessionActive} metaStreaming={streaming} transport={air} "
            + $"deviceCache={status.DeviceCacheConnectionState ?? "—"}",
            caller);
    }

    internal void ObserveAdbCore(string? serial, bool wireless, string? caller)
    {
        var normalized = string.IsNullOrWhiteSpace(serial) ? null : serial;
        string? previous;
        lock (_sync)
        {
            if (string.Equals(normalized, _lastAdbSerial, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            previous = _lastAdbSerial;
            _lastAdbSerial = normalized;
        }

        var transport = normalized is null ? "none" : wireless ? "wireless" : "usb";
        Record(
            SessionTraceKind.State,
            "headset-adb",
            normalized is null ? "disconnect" : previous is null ? "connect" : "reconnect",
            $"from={DescribeAdbSerial(previous)} to={DescribeAdbSerial(normalized)} transport={transport}",
            caller);
    }

    internal void Record(SessionTraceKind kind, string category, string action, string detail, string? caller)
    {
        var now = DateTimeOffset.Now;
        var entry = new SessionTraceEvent(now.ToUniversalTime(), now, kind, category, action, detail, caller);
        lock (_sync)
        {
            _events.Enqueue(entry);
            while (_events.Count > Capacity)
            {
                _events.Dequeue();
            }
        }

        var line = entry.ToLogLine();
        _log?.Info($"{LogPrefix} {line}");
        AppendFile(line);
    }

    internal static string DescribeAdbSerial(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return "(none)";
        }

        return AdbService.LooksLikeWirelessSerial(serial) ? "wireless-endpoint" : "usb-serial";
    }

    private void AppendFile(string line)
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
        catch
        {
            // Tracing must never crash the tray app.
        }
    }
}
