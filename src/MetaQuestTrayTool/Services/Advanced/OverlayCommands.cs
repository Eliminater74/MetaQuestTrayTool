using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace MetaQuestTrayTool.Services;

public static class OverlayCommands
{
    public const int MaxLineLength = 1024;

    public static string Handle(string? line, Func<string>? snapshot = null, string? expectedToken = null)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLength || line.Contains('\n') || line.Contains('\r') || line.Contains('\0'))
        {
            return "ERR malformed command";
        }

        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return "ERR auth required";
        }

        if (!line.StartsWith("auth ", StringComparison.OrdinalIgnoreCase))
        {
            return "ERR auth required";
        }

        var supplied = line["auth ".Length..];
        var split = supplied.IndexOf(' ');
        var token = split < 0 ? supplied.Trim() : supplied[..split];
        var command = split < 0 ? string.Empty : supplied[(split + 1)..].Trim();
        if (!TokenEquals(token, expectedToken))
        {
            return "ERR auth rejected";
        }
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
            return "OK desktop-companion " + OpenVrOverlayStatus.Describe();
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

    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static bool TokenEquals(string supplied, string expected)
    {
        var left = Encoding.UTF8.GetBytes(supplied);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public static class OpenVrOverlayStatus
{
    public const string State = "separate-process";

    public static string Describe() =>
        "openvr-overlay=" + State
        + " binding=Valve openvr v2.5.1 IVROverlay_027"
        + " tray-loads-openvr=false"
        + " physical-acceptance=not-performed";
}

public sealed class OverlayIpcServer : IDisposable
{
    public const string PipeName = "MetaQuestTrayTool.PerformanceOverlay";
    private CancellationTokenSource? _cancel;
    private Func<string>? _snapshot;
    private string _token = string.Empty;

    public bool IsRunning { get; private set; }

    public string Token => _token;

    private Action<string>? _command;

    public void Start(Func<string> snapshot, Action<string>? command = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Stop();
        _snapshot = snapshot;
        _command = command;
        _token = OverlayCommands.CreateToken();
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
                server.ReadTimeout = IoTimeoutMilliseconds;
                server.WriteTimeout = IoTimeoutMilliseconds;
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                var line = ReadBounded(server, OverlayCommands.MaxLineLength);
                var response = OverlayCommands.Handle(line, _snapshot, _token);
                if (response.StartsWith("OK benchmark-stop", StringComparison.OrdinalIgnoreCase))
                {
                    _command?.Invoke("benchmark-stop");
                }

                writer.WriteLine(response);
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

    private static string? ReadBounded(Stream stream, int maxLength)
    {
        var buffer = new byte[maxLength + 1];
        var count = 0;
        while (count <= maxLength)
        {
            var next = stream.ReadByte();
            if (next < 0 || next == '\n')
            {
                break;
            }

            if (next == '\r')
            {
                continue;
            }

            buffer[count++] = (byte)next;
            if (count > maxLength)
            {
                return null;
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, count);
    }

    public const int IoTimeoutMilliseconds = 2000;

    public const string MediumIntegrityLabel = "S:(ML;;NW;;;ME)";

    public static PipeSecurity CreatePipeSecurity()
    {
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("The current user has no SID. The overlay pipe was not created.");
        var security = new PipeSecurity();
        security.SetSecurityDescriptorSddlForm($"D:(A;;GA;;;{user.Value})");
        return security;
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = CreatePipeSecurity();
        var server = NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
        AllowMediumIntegrity(server);
        return server;
    }

    private static void AllowMediumIntegrity(NamedPipeServerStream server)
    {
        var descriptor = new RawSecurityDescriptor(MediumIntegrityLabel);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        if (!SetKernelObjectSecurity(server.SafePipeHandle.DangerousGetHandle(), LabelSecurityInformation, bytes))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "The overlay pipe could not be labeled for a medium-integrity companion.");
        }
    }

    private const int LabelSecurityInformation = 0x00000010;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetKernelObjectSecurity(IntPtr handle, int securityInformation, byte[] securityDescriptor);
}
