# Issue #12: Meta Link freeze after minutes

Date: 2026-09-16. Related reports: [GitHub issue #12](https://github.com/Eliminater74/MetaQuestTrayTool/issues/12) (open), [issue #11](https://github.com/Eliminater74/MetaQuestTrayTool/issues/11) (960 Mbps), [issue #4](https://github.com/Eliminater74/MetaQuestTrayTool/issues/4) (Quest Link apply).

Reviewed HEAD: `e9fe5f39d5eac38e800eda7bb4acba2abbea0ecb` (`main` at investigation start). Diagnostics later shipped in **v1.1.35**. This investigation did **not** assume 960 Mbps is the defect and did **not** change Link/ODT apply/restore/recovery behavior.

## Reporter facts that can be used

- Freeze happens while using Meta Quest Link **with** Meta Quest Tray Tool.
- Freeze does **not** happen without the tray tool.
- After freeze, the last image remains on the Quest 3; recovery requires leaving Link and closing Oculus services.
- Same reporter previously needed >500 Mbps (issue #11). That is an environmental hypothesis, not a proven app bug.
- No extra reporter information was required. No physical-device commands were run.

## Outcome

**No app-side freeze root cause is proven.** The tray does **not** periodically rewrite Link/ODT settings on a stable live session. Several **edge-triggered mutation paths** can still fire after Link has already started. Those paths are documented below with call chains. They are suspects, not a demonstrated compositor freeze.

Because the freeze is unproven, this pass adds **read-only diagnostics only** (session flight recorder). It does not change apply/restore/recovery behavior.

## Recurring services during a live Meta Link session

Shared cadence (`IdleCadence`): Quiet 30s, Watching 12s, Active 5s, HeavyIdle 45s.

| Service | Cadence while Link is up | Periodic mutation on a stable session? |
| --- | --- | --- |
| `LinkSessionWatchService` | Watching (12s) | No. Probe + fingerprint compare. Mutations only on **edges** (session start/end). Two-poll drop confirm is 12s+12s plus 5s probe cache, not minutes. |
| `LinkConnectionProbeService` | On demand, 5s cache | Read-only (DeviceCache, USB VID, processes, optional audio). |
| `HeadsetWatchService` | Watching (12s) if ADB present | **Can mutate** on serial change / reconnect. Same serial with `_appliedForSerial` is a no-op besides cadence. `WirelessAutoReconnect` default **off**; retry 45s only when no ready Quest. |
| `ProcessWatcherService` | Active 5s if a profile is latched; else HeavyIdle 45s | Mutates only on process appear/exit (`ApplyProfile` / `RestoreGlobalDefaults`). 90s launch grace is only for library-armed launches. |
| `AudioSwitchWatcher` | Active 5s while VR audio latched | Mutates audio endpoints on session start/end. Does not write Link/ODT. Fallback capture every 2 min is ID snapshot only. |
| `PowerWatchService` | Watching 12s if auto-switch is on | Mutates power plan / USB selective suspend on session edges. Sleep resume can restart OVRService if that setting is on. |
| `SteamLinkAssistService` | Quiet 30s unless Steam Link kind | OpenXR write only for Steam Link sessions. Idle on pure Meta Link. |
| `DashToSteamVrService` | Timer **stopped** unless PreventDash auto-start is on (default **off**) | Can stop OVRService 10s when SteamVR exits, if that feature is armed. |
| `UpdateWatchService` | 1 hour | GitHub check only. |
| `StatusDashboardService` / `RuntimeSnapshotService` | UI-only, 3s snapshot TTL; Status page 12s when visible | Read-only. |
| `SessionRecoverService` | No timer | `NotifySessionEnded` records a drop and toasts. **`Recover()` is manual only** (tray / Info / hotkey). |
| `OverlayCloseService` | No timer | Kills configured overlay processes on session-start edges if the setting is on (default **off**). |
| `HeadsetAnnouncerService` | Event-driven | TTS only. |
| `SettingsService` debounce timer | 750ms after settings edits | Writes `settings.json` only. |

**Stable-session answer:** once Link is up and ADB serial / profile process / audio latch / power latch are unchanged, the watchers observe. They do not re-apply bitrate, DBR, encode width, or ODT SS/ASW every poll.

## Mutation paths that can still run after Link has started

Defaults that keep these armed: `ApplyWhenHeadsetConnects=true`, `ApplyGlobalWhenHeadsetConnects=true`, `ApplyLinkSettingsOnStart=true`, `ApplyGameSettingsOnStart=true`, `AutoApplyProfiles=true`.

### 1. ADB serial change / reconnect → `ApplyGlobalBaseline` (strongest app-side mutation)

```
HeadsetWatchService.Poll (12s when headset present)
  Adb.FindQuest() → AdbDevice.Serial  (USB serial OR host:port)
  if serial is null: _lastSerial=null, _appliedForSerial=false
  connected = !string.Equals(_lastSerial, serial, OrdinalIgnoreCase)
  if !connected && _appliedForSerial: return          // stable ADB: no write
  Headset.Apply(settings)                             // at least setprop fullRateCapture
  if !IsGameProfileActive && ApplyGlobalWhenHeadsetConnects:
    App.ApplyGlobalBaseline(notify: connected)
      GetCapabilities()  // live Meta Link => AllowsMetaLinkRegistry + AllowsOculusDebugTool
      DebugTool.Apply(DefaultGameSettings)            // OculusDebugToolCLI SS/ASW/FOV
      Link.PreflightStartupLinkApply + Link.Apply     // HKCU RemoteHeadset WRITE
```

`FindQuest` prefers any ready headset and uses the **ADB transport serial**, not `ro.serialno`. USB (`1WMHH…`) and wireless (`192.168.x.x:5555`) are different strings, so a transport flip is a new connection. ADB disappearing during Link (common on USB video) clears `_appliedForSerial`; when ADB returns, globals are applied again **while Meta Link is already streaming**.

This is a proven code path. It is **not** proven that the reporter’s Quest drops/reappears ADB after minutes, and it is not proven that a mid-stream `Link.Apply` / ODT CLI apply freezes the compositor.

### 2. False Link fingerprint edge (session start/end side effects, not Link rewrite)

Fingerprint while live:

`active:{Kind}:{HeadsetSerial}:{DeviceCacheConnectionState}:{IsUsingAirLink}`

Any DeviceCache `connectionState` flicker changes the fingerprint. `Poll` then treats **live + fingerprint change** as session **start**: `SessionRecover.NotifySessionStarted()` (clears drop state only), overlay close if enabled, announcer. It does **not** call `Link.Apply` or `DebugTool.Apply`.

Session **end** requires the previous fingerprint to start with `active:` and **two** consecutive non-live polls (Watching cadence ≈ 12s each). `NotifySessionEnded` does not restart OVRService. It can restore desktop audio.

A false end→start pair can flap audio and overlays. It does not, by itself, rewrite bitrate.

### 3. Profile restore while a VR child is still running

```
ProcessWatcherService.Poll
  if latched process not running:
    if awaiting launch && < 90s: wait
    else Dispatcher.Invoke RestoreDefaults
      App.RestoreGlobalDefaults
        DebugTool.Apply(globals) + Link.Apply(globals) + OpenXr.RestoreAfterProfile
```

Auto-detect latches the **profile’s process name**, not children. If a launcher process exits while the actual OpenXR/Oculus game continues, globals (including Link registry + ODT) are restored during the live stream. Library launches use a 90s grace; that is shorter than “a few minutes” unless the process appears then later exits.

### 4. SessionRecover cannot affect a live session unless the user asks

`NotifySessionStarted` / `NotifySessionEnded` do not call `Oculus.Restart` or `Link.Apply`. `Recover()` does restart OVRService and re-apply Link/ODT/audio. That is explicit user action.

### 5. 960 Mbps write cadence

`LinkSettings.BitratePresets` includes 960. Writes happen only through `LinkSettingsService.Apply`:

- Startup `ApplyGlobalBaseline` if `ApplyLinkSettingsOnStart` (once; preflight may preserve external high bitrate/DBRMax).
- ADB-connect `ApplyGlobalBaseline` (suspect #1).
- Profile apply/restore.
- Manual Quest Link page / tray / hotkey / Recover PCVR.

There is **no timer that rewrites 960**. If the encoder is unstable at 960, that is a Meta runtime/environment hypothesis, not a periodic tray rewrite.

### 6. Dash→SteamVR OVRService drop

Default `PreferPreventDashLaunch=false`, so the 5s session timer is off. If the user enabled PreventDash and `RestartOvrServiceWhenSteamVrExits` (default true when that feature is used), SteamVR exit stops OVRService for 10s. That would tear down Link, which matches “must close Oculus services,” but only after SteamVR actually exits — not while a Meta Link game keeps running without SteamVR.

## Ranked hypotheses (evidence, not guesses)

1. **ADB reconnect / USB↔wireless serial change → `ApplyGlobalBaseline` during live Link** — proven call chain; freeze effect unproven. Files: `HeadsetWatchService.Poll`, `App.ApplyGlobalBaseline`, `LinkSettingsService.Apply`, `OculusDebugToolService.Apply`.
2. **Profile restore because a launcher process exited** — proven if auto-apply is on and the watched process dies; freeze effect unproven. Files: `ProcessWatcherService.RestoreDefaults`, `App.RestoreGlobalDefaults`.
3. **False session-end audio restore / overlay kill** — proven edge behavior; weak freeze mechanism (does not restart OVRService or rewrite Link).
4. **Extreme bitrate/DBR (960) Meta encoder stall** — possible environment; tray writes it at most on apply edges, not every poll. Issue #11 context only.
5. **Unrelated Meta/GPU hang that happens to coincide with MQT** — cannot be excluded from source; reporter says it does not happen without the tray, which keeps app-side edges in play.
6. **Automatic OVRService restart during play** — not found except sleep-resume (setting) and Dash→SteamVR SteamVR-exit (opt-in).

## Diagnostics added (no Link mutation)

`SessionFlightRecorder` logs **state changes and mutating actions only** to `%AppData%\MetaQuestTrayTool\session-trace.log` and as `[TRACE]` lines in `app.log`. Support ZIP includes the ring buffer. It never calls `Link.Apply`, ODT, ADB, or OVRService.
