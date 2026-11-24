using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MetaQuestTrayTool.Services;

public static class OverlayCommands
{
    public const int MaxLineLength = 1024;

    public static string Handle(string? line, Func<string>? snapshot = null)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLength || line.Contains('\n') || line.Contains('\r') || line.Contains('\0'))
        {
            return "ERR malformed command";
        }

        var command = line.Trim();
        if (command.Contains("..", StringComparison.Ordinal)
            || command.Contains(';', StringComparison.Ordinal)
            || command.Contains('|', StringComparison.Ordinal)
            || command.Contains('&', StringComparison.Ordinal)
            || command.Contains("adb", StringComparison.OrdinalIgnoreCase)
            || command.Contains("shell", StringComparison.OrdinalIgnoreCase)
            || command.Contains("cmd", StringComparison.OrdinalIgnoreCase)
            || command.Contains("powershell", StringComparison.OrdinalIgnoreCase))
        {
            return "ERR command rejected";
        }

        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            return "OK pong";
        }

        if (command.Equals("overlay-status", StringComparison.OrdinalIgnoreCase))
        {
            return "OK desktop-companion openvr-overlay=not-loaded reason=untested-native-binding";
        }

        if (command.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
        {
            var text = snapshot?.Invoke() ?? "no-sample";
            text = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            return "OK " + text;
        }

        if (command.Equals("mode compact", StringComparison.OrdinalIgnoreCase)
            || command.Equals("mode detail", StringComparison.OrdinalIgnoreCase)
            || command.Equals("mode hidden", StringComparison.OrdinalIgnoreCase))
        {
            return "OK " + command.ToLowerInvariant();
        }

        if (command.Equals("benchmark-stop", StringComparison.OrdinalIgnoreCase))
        {
            return "OK benchmark-stop acknowledged; no process was killed";
        }

        if (command.StartsWith("benchmark-start ", StringComparison.OrdinalIgnoreCase))
        {
            var duration = command["benchmark-start ".Length..].Trim();
            if (duration is "30" or "60" or "300")
            {
                return "OK benchmark-start " + duration + " acknowledged; SteamVR was not restarted";
            }

            return "ERR unsupported benchmark duration";
        }

        if (command.StartsWith("profile ", StringComparison.OrdinalIgnoreCase))
        {
            return "ERR profile changes are not available from the overlay";
        }

        return "ERR unsupported command";
    }
}

public sealed class OverlayIpcServer : IDisposable
{
    public const string PipeName = "MetaQuestTrayTool.PerformanceOverlay";
    private CancellationTokenSource? _cancel;
    private Func<string>? _snapshot;

    public bool IsRunning { get; private set; }

    public void Start(Func<string> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Stop();
        _snapshot = snapshot;
        _cancel = new CancellationTokenSource();
        IsRunning = true;
        var token = _cancel.Token;
        Task.Run(() => Listen(token));
    }

    public void Stop()
    {
        IsRunning = false;
        try
        {
            _cancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _cancel?.Dispose();
        _cancel = null;
    }

    public void Dispose() => Stop();

    private void Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = CreateServer();
                server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                var line = reader.ReadLine();
                writer.WriteLine(OverlayCommands.Handle(line, _snapshot));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                Thread.Sleep(200);
            }
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is not null)
        {
            security.AddAccessRule(new PipeAccessRule(identity.User, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
