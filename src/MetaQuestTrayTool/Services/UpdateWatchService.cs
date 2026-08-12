using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Periodically checks GitHub for updates based on the user's chosen interval.
/// </summary>
public sealed class UpdateWatchService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private int _checking;

    public UpdateWatchService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer
        {
            // Coarse poll — actual cadence is gated by LastUpdateCheckUtc + interval.
            Interval = TimeSpan.FromHours(1)
        };
        _timer.Tick += async (_, _) => await PollAsync().ConfigureAwait(true);
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        // Hourly tick only — first eligibility after start is handled by Check for updates on start
        // and/or the next timer tick (avoids a duplicate GitHub hit in the first few seconds).
        _timer.Start();
    }

    public void Dispose() => _timer.Stop();

    private async Task PollAsync()
    {
        if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var tray = _app.Settings.Current.Tray;
            if (!UpdateCheckIntervalHelper.IsDue(tray.AutoUpdateCheckInterval, tray.LastUpdateCheckUtc, DateTimeOffset.UtcNow))
            {
                return;
            }

            if (_app.Updates.IsBusy)
            {
                return;
            }

            _app.Log.Info($"Scheduled update check ({UpdateCheckIntervalHelper.Describe(tray.AutoUpdateCheckInterval)})…");
            await _app.Updates.CheckInteractivelyAsync(owner: null, quietIfUpToDate: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Scheduled update check failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }
}
