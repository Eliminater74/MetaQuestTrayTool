using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public enum AdbActivityMode
{
    Idle,
    SessionActive,
    ManualCheck
}

/// <summary>
/// Decides when this tray may touch ADB. Link detection stays outside this type.
/// Automatic headset ADB requires a live PCVR session; <see cref="VrSessionCapabilities.AllowsHeadsetAdb"/>
/// defaults to true on an idle capability object and must not start ADB by itself.
/// </summary>
public sealed class AdbSessionGate
{
    public const string IdleLogMessage =
        "ADB idle — no active PCVR session. Headset ADB is on demand.";

    public const string HeadsetPageIdleText =
        "ADB idle — starts on PCVR connection or when you request an ADB action.";

    /// <summary>
    /// Session teardown stops our timers and commands. It does not run adb kill-server.
    /// The shared ADB server may keep running for Android Studio, SideQuest, and other clients.
    /// </summary>
    public const bool SessionEndKillsAdbServer = false;

    /// <summary>Normal startup locates adb.exe and does not execute it.</summary>
    public const bool StartupExecutesAdbCommands = false;

    /// <summary>Phones, TVs, emulators, and other wireless devices are not disconnected.</summary>
    public const bool AutomaticSweepDisconnectsOtherDevices = false;

    public bool SessionActive { get; private set; }
    public bool AllowsHeadsetAdb { get; private set; }
    public VrConnectionKind? Kind { get; private set; }
    public int Generation { get; private set; }
    public AdbActivityMode Mode { get; private set; } = AdbActivityMode.Idle;
    public bool ManualInProgress { get; private set; }

    /// <summary>
    /// True only for a live PCVR session whose capabilities allow headset ADB.
    /// Meta Air Link, Meta wired Link, Steam Link / SteamVR, and Virtual Desktop all allow it
    /// once <see cref="LinkSessionWatchService.IsLivePcvrSession"/> is true. That matches the
    /// existing product behavior: those sessions still use headset ADB, while Meta registry/ODT
    /// apply is what non-Meta transports skip.
    /// </summary>
    public bool AllowsAutomaticAdb => SessionActive && AllowsHeadsetAdb;

    public bool IsCurrent(int generation) => generation == Generation;

    /// <summary>Returns true when the live/allowed state changed.</summary>
    public bool NotifySession(VrConnectionStatus status)
    {
        if (!LinkSessionWatchService.IsLivePcvrSession(status))
        {
            return NotifyEnded();
        }

        var caps = VrSessionCapabilities.From(status);
        var allows = status.SessionActive && caps.SessionActive && caps.AllowsHeadsetAdb;
        var changed = !SessionActive || Kind != status.Kind || AllowsHeadsetAdb != allows;
        SessionActive = true;
        Kind = status.Kind;
        AllowsHeadsetAdb = allows;
        if (!ManualInProgress)
        {
            Mode = allows ? AdbActivityMode.SessionActive : AdbActivityMode.Idle;
        }

        return changed;
    }

    /// <summary>Returns true when a session was cleared. Always bumps the generation when a session was active.</summary>
    public bool NotifyEnded()
    {
        var wasActive = SessionActive || Mode == AdbActivityMode.SessionActive;
        if (!wasActive)
        {
            if (!ManualInProgress)
            {
                Mode = AdbActivityMode.Idle;
            }

            return false;
        }

        SessionActive = false;
        AllowsHeadsetAdb = false;
        Kind = null;
        Generation++;
        if (!ManualInProgress)
        {
            Mode = AdbActivityMode.Idle;
        }

        return true;
    }

    public void BeginManual()
    {
        ManualInProgress = true;
        Mode = AdbActivityMode.ManualCheck;
    }

    public void EndManual()
    {
        ManualInProgress = false;
        Mode = AllowsAutomaticAdb ? AdbActivityMode.SessionActive : AdbActivityMode.Idle;
    }

    /// <summary>
    /// Headset-only wireless sweep is intentionally not an input. A saved
    /// <c>HeadsetOnlyWirelessAdb=true</c> must not keep the watcher alive.
    /// </summary>
    public static bool ShouldArmAutomaticWatcher(bool automaticSessionAllowed, HeadsetSettings settings)
    {
        if (!automaticSessionAllowed || settings.AdbWatcherPaused)
        {
            return false;
        }

        return settings.ApplyWhenHeadsetConnects || settings.WirelessAutoReconnect;
    }

    public static bool ShouldAttemptWirelessReconnect(bool automaticSessionAllowed, HeadsetSettings settings) =>
        ShouldArmAutomaticWatcher(automaticSessionAllowed, settings)
        && settings.WirelessAutoReconnect
        && settings.WirelessEndpoint is not null;

    public static string PassiveStatusLabel(AdbActivityMode mode) => mode switch
    {
        AdbActivityMode.SessionActive => "ADB: Active for PCVR session",
        AdbActivityMode.ManualCheck => "ADB: Manual check",
        _ => "ADB: Idle (on demand)"
    };
}
