using System.Windows.Threading;
using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

/// <summary>
/// Steam Link / SteamVR session helper: clearer guidance and optional OpenXR → SteamVR nudge.
/// Restores the user's preferred OpenXR runtime when the Steam Link session ends.
/// Does not edit steamvr.vrsettings or Steam Link bitrate (leave those to Valve's UI).
/// </summary>
public sealed class SteamLinkAssistService : IDisposable
{
    private readonly App _app;
    private readonly DispatcherTimer _timer;
    private bool _wasSteamSession;
    private bool _nudgeDoneThisSession;
    private bool _mismatchLoggedThisSession;
    private bool _switchedOpenXrForSession;
    private OpenXrRuntimeKind? _openXrBeforeSwitch;

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
                if (_wasSteamSession)
                {
                    RestorePreferredOpenXrAfterSteamLink();
                }

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
                _switchedOpenXrForSession = false;
                _openXrBeforeSwitch = null;
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
            _openXrBeforeSwitch = openXr;
            _switchedOpenXrForSession = true;
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

    private void RestorePreferredOpenXrAfterSteamLink()
    {
        if (!_switchedOpenXrForSession)
        {
            _switchedOpenXrForSession = false;
            _openXrBeforeSwitch = null;
            return;
        }

        _switchedOpenXrForSession = false;
        var preferred = _app.Settings.Current.OpenXr.PreferredRuntime;
        OpenXrRuntimeKind restoreTo;
        if (preferred is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            restoreTo = preferred;
        }
        else if (_openXrBeforeSwitch is OpenXrRuntimeKind.Meta or OpenXrRuntimeKind.SteamVr)
        {
            restoreTo = _openXrBeforeSwitch.Value;
        }
        else
        {
            _openXrBeforeSwitch = null;
            _app.Log.Info("Steam Link ended — OpenXR left on SteamVR (no preferred default to restore).");
            return;
        }

        _openXrBeforeSwitch = null;
        var current = _app.OpenXr.ReadActiveKind();
        if (current == restoreTo)
        {
            _app.Log.Info($"Steam Link ended — OpenXR already {OpenXrRuntimeService.Label(restoreTo)}.");
            return;
        }

        var result = _app.OpenXr.Set(restoreTo);
        _app.Log.Info("Steam Link ended — restored OpenXR default: " + result);
        if (_app.Settings.Current.ShowNotifications)
        {
            _app.TrayNotify(
                "Steam Link",
                $"Restored OpenXR to {OpenXrRuntimeService.Label(restoreTo)}.");
        }
    }

    private static bool IsSteamLinkSession(VrConnectionStatus status) =>
        status.SessionActive && status.Kind == VrConnectionKind.SteamLinkOrSteamVr;
}
