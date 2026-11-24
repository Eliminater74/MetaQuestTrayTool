using System.Text.RegularExpressions;

namespace MetaQuestTrayTool.Services;

public enum NoticeSeverity
{
    Information,
    Success,
    Warning,
    Error
}

public enum NoticeCategory
{
    General,
    Startup,
    Headset,
    Profile,
    Performance,
    Benchmark,
    Update,
    Recovery,
    SteamVr
}

public enum NoticeAction
{
    None,
    OpenSettings,
    OpenPerformance,
    OpenHeadset,
    OpenProfiles,
    ViewLogs,
    OpenRecovery
}

public sealed record NoticePreferences
{
    public bool ModernFlyouts { get; init; } = true;
    public bool Transparency { get; init; } = true;
    public bool Animations { get; init; } = true;
    public int DurationSeconds { get; init; } = 6;
    public bool PlaySound { get; init; }
    public bool KeepHistory { get; init; } = true;
    public bool Startup { get; init; } = true;
    public bool Headset { get; init; } = true;
    public bool Profile { get; init; } = true;
    public bool Performance { get; init; } = true;
    public bool Benchmark { get; init; } = true;
    public bool Update { get; init; } = true;
    public bool General { get; init; } = true;
    public bool LeftClickOpensQuickControls { get; init; }
}

public sealed record NoticeItem
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required NoticeSeverity Severity { get; init; }
    public required NoticeCategory Category { get; init; }
    public required NoticeAction Action { get; init; }
    public IReadOnlyList<NoticeAction> Actions { get; init; } = [];
    public required DateTimeOffset CreatedUtc { get; init; }
    public required TimeSpan Duration { get; init; }
    public bool Read { get; set; }
}

public sealed class DesktopNotificationCenter
{
    public const int MaxVisible = 3;
    public const int MaxHistory = 40;
    private static readonly TimeSpan Throttle = TimeSpan.FromSeconds(8);
    private static readonly Regex Sensitive = new(
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b|[A-Fa-f0-9]{16,}",
        RegexOptions.Compiled);

    private readonly List<NoticeItem> _history = [];
    private readonly List<NoticeItem> _visible = [];
    private readonly Dictionary<string, DateTimeOffset> _recent = new(StringComparer.Ordinal);

    public IReadOnlyList<NoticeItem> History => _history;
    public IReadOnlyList<NoticeItem> Visible => _visible;
    public int UnreadRecovery => _history.Count(item => !item.Read && item.Category == NoticeCategory.Recovery && item.Severity >= NoticeSeverity.Warning);

    public NoticeItem? PresentStartup(NoticePreferences preferences, bool desktopEnabled, DateTimeOffset utc)
    {
        if (!preferences.Startup)
        {
            return null;
        }

        return Admit(new NoticeItem
        {
            Title = "Ready for Virtual Reality",
            Message = "Your PCVR controls and performance tools are ready.",
            Severity = NoticeSeverity.Information,
            Category = NoticeCategory.Startup,
            Action = NoticeAction.OpenSettings,
            Actions = [NoticeAction.OpenSettings, NoticeAction.OpenPerformance],
            CreatedUtc = utc,
            Duration = DurationFor(NoticeSeverity.Information, preferences)
        }, preferences, desktopEnabled);
    }

    public NoticeItem? Present(string? title, string? message, NoticePreferences preferences, bool desktopEnabled, DateTimeOffset utc)
    {
        var classified = Classify(title, message);
        if (!CategoryEnabled(classified.Category, preferences) && classified.Category != NoticeCategory.Recovery)
        {
            return null;
        }

        var actions = classified.Action == NoticeAction.None ? Array.Empty<NoticeAction>() : new[] { classified.Action };
        var item = new NoticeItem
        {
            Title = classified.Title,
            Message = Redact(classified.Message),
            Severity = classified.Severity,
            Category = classified.Category,
            Action = classified.Action,
            Actions = actions,
            CreatedUtc = utc,
            Duration = DurationFor(classified.Severity, preferences)
        };

        return Admit(item, preferences, desktopEnabled);
    }

    private NoticeItem? Admit(NoticeItem item, NoticePreferences preferences, bool desktopEnabled)
    {
        var utc = item.CreatedUtc;
        var key = item.Title + "\n" + item.Message;
        if (_recent.TryGetValue(key, out var previous) && utc - previous < Throttle)
        {
            return null;
        }

        _recent[key] = utc;
        if (preferences.KeepHistory || item.Category == NoticeCategory.Recovery)
        {
            _history.Insert(0, item);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);
            }
        }

        var show = desktopEnabled;
        if (!desktopEnabled)
        {
            show = item.Category == NoticeCategory.Recovery && item.Severity >= NoticeSeverity.Warning;
        }

        if (!show)
        {
            return null;
        }

        _visible.Add(item);
        while (_visible.Count > MaxVisible)
        {
            _visible.RemoveAt(0);
        }

        return item;
    }

    public void Dismiss(NoticeItem item)
    {
        _visible.Remove(item);
        item.Read = true;
    }

    public void ClearHistory()
    {
        _history.Clear();
        _visible.Clear();
    }

    public static TimeSpan DurationFor(NoticeSeverity severity, NoticePreferences preferences)
    {
        var seconds = Math.Clamp(preferences.DurationSeconds, 3, 20);
        var scaled = severity switch
        {
            NoticeSeverity.Warning => seconds * 2,
            NoticeSeverity.Error => seconds * 3,
            _ => seconds
        };
        return TimeSpan.FromSeconds(Math.Clamp(scaled, 3, 30));
    }

    public static bool CategoryEnabled(NoticeCategory category, NoticePreferences preferences) => category switch
    {
        NoticeCategory.Startup => preferences.Startup,
        NoticeCategory.Headset => preferences.Headset,
        NoticeCategory.Profile => preferences.Profile,
        NoticeCategory.Performance => preferences.Performance,
        NoticeCategory.Benchmark => preferences.Benchmark,
        NoticeCategory.Update => preferences.Update,
        NoticeCategory.General or NoticeCategory.SteamVr => preferences.General,
        NoticeCategory.Recovery => true,
        _ => preferences.General
    };

    public static (string Title, string Message, NoticeSeverity Severity, NoticeCategory Category, NoticeAction Action) Classify(string? title, string? message)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Meta Quest Tray Tool" : title.Trim();
        var safeMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        if (safeMessage.Length > 400)
        {
            safeMessage = safeMessage[..400];
        }

        var text = safeTitle + " " + safeMessage;
        var severity = NoticeSeverity.Information;
        if (Contains(text, "fail", "error", "could not", "unable", "denied"))
        {
            severity = NoticeSeverity.Error;
        }
        else if (Contains(text, "warn", "unsupported", "reconnect", "pending", "thermal", "bottleneck", "unavailable"))
        {
            severity = NoticeSeverity.Warning;
        }
        else if (Contains(text, "applied", "restored", "ready", "finished", "completed", "success", "connected"))
        {
            severity = NoticeSeverity.Success;
        }

        var category = NoticeCategory.General;
        var action = NoticeAction.OpenSettings;
        if (Contains(safeTitle, "update"))
        {
            category = NoticeCategory.Update;
            action = NoticeAction.OpenSettings;
        }
        else if (Contains(safeTitle, "recover"))
        {
            category = NoticeCategory.Recovery;
            action = NoticeAction.OpenRecovery;
        }
        else if (Contains(safeTitle, "benchmark"))
        {
            category = NoticeCategory.Benchmark;
            action = NoticeAction.OpenPerformance;
        }
        else if (Contains(safeTitle, "performance", "bottleneck"))
        {
            category = NoticeCategory.Performance;
            action = NoticeAction.OpenPerformance;
        }
        else if (Contains(safeTitle, "profile", "game settings", "launch"))
        {
            category = NoticeCategory.Profile;
            action = NoticeAction.OpenProfiles;
        }
        else if (Contains(safeTitle, "headset", "adb"))
        {
            category = NoticeCategory.Headset;
            action = NoticeAction.OpenHeadset;
        }
        else if (Contains(safeTitle, "steam"))
        {
            category = NoticeCategory.SteamVr;
            action = NoticeAction.None;
        }
        else if (Contains(text, "running in the notification area"))
        {
            category = NoticeCategory.Startup;
            action = NoticeAction.OpenSettings;
        }

        if (severity == NoticeSeverity.Error && action == NoticeAction.OpenSettings)
        {
            action = NoticeAction.ViewLogs;
        }

        return (safeTitle, safeMessage, severity, category, action);
    }

    public static string Redact(string message) => Sensitive.Replace(message, "[redacted]");

    private static bool Contains(string text, params string[] words)
    {
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public readonly record struct NoticeAnchor(double Left, double Top);

public static class NoticePlacement
{
    public static NoticeAnchor Place(double workLeft, double workTop, double workRight, double workBottom, double width, double height, int stackIndex, bool taskbarOnTop, bool taskbarOnLeft)
    {
        var margin = 12d;
        var stack = Math.Max(0, stackIndex) * (height + 8);
        double left;
        double top;
        if (taskbarOnLeft)
        {
            left = workLeft + margin;
            top = workBottom - height - margin - stack;
        }
        else if (taskbarOnTop)
        {
            left = workRight - width - margin;
            top = workTop + margin + stack;
        }
        else
        {
            left = workRight - width - margin;
            top = workBottom - height - margin - stack;
        }

        left = Math.Clamp(left, workLeft, Math.Max(workLeft, workRight - width));
        top = Math.Clamp(top, workTop, Math.Max(workTop, workBottom - height));
        return new NoticeAnchor(left, top);
    }
}
