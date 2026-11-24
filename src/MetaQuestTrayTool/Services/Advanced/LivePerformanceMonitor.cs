namespace MetaQuestTrayTool.Services;

public enum MonitorState
{
    Stopped,
    Running,
    Paused
}

public enum EvidenceKind
{
    LiveSample,
    PastedLog,
    RequestedSetting,
    PropertyReadback,
    RuntimeEvidence,
    Unavailable
}

public sealed class MonitorPoint
{
    public DateTimeOffset Utc { get; init; }
    public EvidenceKind Evidence { get; init; }
    public IReadOnlyList<TelemetryMetric> Metrics { get; init; } = [];
}

/// <summary>
/// Session-scoped performance history. It records only metrics the caller supplies.
/// Missing values stay unavailable. Periodic samples are not frame-time statistics.
/// </summary>
public sealed class LivePerformanceMonitor
{
    public const int MaxPoints = 180;
    public static readonly TimeSpan MinimumHeadsetInterval = TimeSpan.FromSeconds(15);

    private readonly List<MonitorPoint> _points = [];
    private DateTimeOffset _lastHeadsetUtc = DateTimeOffset.MinValue;

    public MonitorState State { get; private set; } = MonitorState.Stopped;

    public int HeadsetSamplesRequested { get; private set; }

    public IReadOnlyList<MonitorPoint> History => _points;

    public void Start() => State = MonitorState.Running;

    public void Pause()
    {
        if (State == MonitorState.Running)
        {
            State = MonitorState.Paused;
        }
    }

    public void Stop()
    {
        State = MonitorState.Stopped;
        _points.Clear();
        _lastHeadsetUtc = DateTimeOffset.MinValue;
        HeadsetSamplesRequested = 0;
    }

    public bool TryRecord(IReadOnlyList<TelemetryMetric> metrics, EvidenceKind evidence, DateTimeOffset utc)
    {
        if (State != MonitorState.Running || metrics is null)
        {
            return false;
        }

        _points.Add(new MonitorPoint
        {
            Utc = utc,
            Evidence = evidence,
            Metrics = Label(metrics, evidence, utc)
        });
        while (_points.Count > MaxPoints)
        {
            _points.RemoveAt(0);
        }

        return true;
    }

    public bool HeadsetSampleDue(bool sessionActive, bool userRequestedHeadset, DateTimeOffset utc)
    {
        if (State != MonitorState.Running)
        {
            return false;
        }

        if (!TelemetryCollectionPolicy.AllowHeadsetAdb(sessionActive, userRequestedHeadset))
        {
            return false;
        }

        if (!sessionActive && !userRequestedHeadset)
        {
            return false;
        }

        return utc - _lastHeadsetUtc >= MinimumHeadsetInterval;
    }

    public IReadOnlyList<TelemetryMetric> Tick(
        bool sessionActive,
        Func<IReadOnlyList<TelemetryMetric>>? headsetSampler,
        IReadOnlyList<TelemetryMetric>? pcMetrics,
        DateTimeOffset utc)
    {
        if (State != MonitorState.Running)
        {
            return [];
        }

        var collected = new List<TelemetryMetric>();
        if (pcMetrics is not null)
        {
            collected.AddRange(Label(pcMetrics, EvidenceKind.LiveSample, utc));
        }

        if (HeadsetSampleDue(sessionActive, userRequestedHeadset: false, utc) && headsetSampler is not null)
        {
            HeadsetSamplesRequested++;
            _lastHeadsetUtc = utc;
            collected.AddRange(Label(headsetSampler(), EvidenceKind.RuntimeEvidence, utc));
        }
        else if (!sessionActive)
        {
            collected.Add(TelemetryMetric.Unavailable(
                "RuntimeFps",
                "FPS",
                "not-sampled",
                null,
                utc,
                "No live headset sample. ADB is not polled while no PCVR session is active."));
        }

        _points.Add(new MonitorPoint
        {
            Utc = utc,
            Evidence = sessionActive ? EvidenceKind.RuntimeEvidence : EvidenceKind.LiveSample,
            Metrics = collected
        });
        while (_points.Count > MaxPoints)
        {
            _points.RemoveAt(0);
        }

        return collected;
    }

    public static IReadOnlyList<TelemetryMetric> Label(IReadOnlyList<TelemetryMetric> metrics, EvidenceKind evidence, DateTimeOffset utc)
    {
        var prefix = evidence switch
        {
            EvidenceKind.LiveSample => "Live sample. ",
            EvidenceKind.PastedLog => "Pasted log. Not a live sample. ",
            EvidenceKind.RequestedSetting => "Requested setting. Not a measured runtime value. ",
            EvidenceKind.PropertyReadback => "Property readback. Not proof of the panel rate or GPU clock. ",
            EvidenceKind.RuntimeEvidence => "Runtime evidence. ",
            _ => "Unavailable. "
        };
        return metrics.Select(metric => metric with
        {
            TimestampUtc = utc,
            Note = prefix + (metric.Note ?? string.Empty)
        }).ToList();
    }
}
