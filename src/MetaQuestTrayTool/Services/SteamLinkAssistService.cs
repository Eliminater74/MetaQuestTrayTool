using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Steam Link / SteamVR session helper: clearer guidance and optional OpenXR → SteamVR nudge.
/// Does not edit steamvr.vrsettings or Steam Link bitrate (leave those to Valve's UI).
/// </summary>
public sealed class SteamLinkAssistService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool _wasSteamSession;
    private bool _nudgeDoneThisSession;
    private bool _mismatchLoggedThisSession;

    public SteamLinkAssistService(App app)
    {
        _app = app;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        if (_timer.IsEnabled)
        {
            return;
        }

        _timer.Start();
        Poll();
    }

    public void Dispose() => _timer.Stop();

    /// <summary>OpenXR mismatch tip for Info / banners (null if fine).</summary>
    public string? DescribeOpenXrMismatch(VrConnectionStatus? status = null)
    {
        status ??= _app.LinkConnection.Probe();
        if (!IsSteamLinkSession(status))
        {
            return null;
        }

        var openXr = _app.OpenXr.ReadActiveKind();
        if (openXr == OpenXrRuntimeKind.SteamVr)
        {
            return null;
        }

        return "Steam Link tip: set OpenXR to SteamVR (Game Settings or tray OpenXR menu). "
               + $"Currently active: {OpenXrRuntimeService.Label(openXr)}. "
               + "Wrong runtime is a common cause of “headset not detected” in OpenXR games.";
    }

    private void Poll()
    {
        try
        {
            var status = _app.LinkConnection.Probe(includeEnumHmd: false);
            var steam = IsSteamLinkSession(status);
            if (!steam)
            {
                _wasSteamSession = false;
                _nudgeDoneThisSession = false;
                _mismatchLoggedThisSession = false;
                return;
            }

            if (!_wasSteamSession)
            {
                _wasSteamSession = true;
                _nudgeDoneThisSession = false;
                _mismatchLoggedThisSession = false;
                _app.Log.Info(
                    "Steam Link / SteamVR session detected — Meta Link registry and ODT are gated. "
                    + "Use Steam Link / SteamVR Video settings for bitrate and resolution. ADB + OpenXR still apply.");
            }

            var mismatch = DescribeOpenXrMismatch(status);
            if (mismatch is not null && !_mismatchLoggedThisSession)
            {
                _mismatchLoggedThisSession = true;
                _app.Log.Warn(mismatch);
                if (_app.Settings.Current.ShowNotifications)
                {
                    _app.TrayNotify("Steam Link", "OpenXR is not SteamVR — OpenXR games may fail. See Info / Game Settings.");
                }
            }

            if (_nudgeDoneThisSession
                || !_app.Settings.Current.OpenXr.PreferSteamVrDuringSteamLink)
            {
                return;
            }

            var openXr = _app.OpenXr.ReadActiveKind();
            if (openXr == OpenXrRuntimeKind.SteamVr)
            {
                _nudgeDoneThisSession = true;
                return;
            }

            _nudgeDoneThisSession = true;
            var result = _app.OpenXr.Set(OpenXrRuntimeKind.SteamVr);
            _app.Log.Info("Steam Link assist: " + result);
            if (_app.Settings.Current.ShowNotifications)
            {
                _app.TrayNotify("Steam Link", "Switched OpenXR to SteamVR for this session.");
            }
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Steam Link assist failed: {ex.Message}");
        }
    }

    private static bool IsSteamLinkSession(VrConnectionStatus status) =>
        status.SessionActive && status.Kind == VrConnectionKind.SteamLinkOrSteamVr;
}
