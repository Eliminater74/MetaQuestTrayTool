using System.Windows.Threading;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Shared wake-up cadence for tray background watchers.
/// Philosophy: stay quiet on the desktop; react quickly when PCVR / a feature needs attention.
/// </summary>
public static class IdleCadence
{
    /// <summary>Tray sitting unused — minimize wakeups (PCVR frametime headroom).</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(30);

    /// <summary>Feature armed or session may start soon.</summary>
    public static readonly TimeSpan Watching = TimeSpan.FromSeconds(12);

    /// <summary>Active PCVR / profile / audio latch — need timely exit/edge detection.</summary>
    public static readonly TimeSpan Active = TimeSpan.FromSeconds(5);

    /// <summary>Heavy work (full process snapshot / ADB) while nothing is connected.</summary>
    public static readonly TimeSpan HeavyIdle = TimeSpan.FromSeconds(45);

    public static void Set(DispatcherTimer timer, TimeSpan interval)
    {
        if (timer.Interval != interval)
        {
            timer.Interval = interval;
        }
    }
}
