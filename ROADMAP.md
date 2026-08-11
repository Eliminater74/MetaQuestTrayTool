# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

## Done

### v0.1 — tray host

The app lives in the notification area, opens a dashboard, tracks `OVRService`, and stores settings.

### Phase 1 — Game settings via Oculus Debug Tool CLI

Tray + dashboard push pixel density and ASW through `OculusDebugToolCLI.exe`.

- [x] Apply supersampling (`service set-pixels-per-display-pixel-override`)
- [x] Apply ASW (`server:asw.Off` / `Auto` / `Clock45`)
- [x] Tray submenu to change defaults and apply now
- [x] Persist last-used defaults in `settings.json`
- [x] Log CLI stdout/stderr and warn when Meta rejects `server:` commands
- [ ] FOV multiplier UI (CLI command is already sent when the stored value is not 1.0)

### Phase 2 — Profiles

- [x] Create / edit / delete profiles (name, process, SS, ASW, FOV, CPU priority, notes)
- [x] Profile list on the dashboard and tray
- [x] Watch running processes and apply the matching profile
- [x] Restore defaults when the game exits
- [x] CPU priority for the detected process
- [ ] Ignore-list for noisy processes

### Phase 3 — Quest Link / Air Link

Writes Meta's own Link registry hive (`HKCU\Software\Oculus\RemoteHeadset`).

- [x] Bitrate, encode resolution width, HEVC preference, sliced-encoding off
- [x] Tray submenu + Link settings window + dashboard status
- [x] Store preferred Link settings separately from game profiles
- [x] Optional apply on app start
- [ ] Detect wired Link vs Air Link when possible
- [ ] Dynamic bitrate max UI (Meta exposes related keys; needs more runtime verification)

## Next

### Phase 4 — Audio switching

- [x] List playback / recording devices
- [x] Switch to headset devices when Oculus service starts
- [x] Restore fallback devices when the service stops
- [x] Tray + audio settings window
- [ ] Optional separate communications devices (currently one toggle for communications role)

### Phase 5 — Power and USB

- [x] Switch Windows power plan while VR / Oculus service is active
- [x] USB selective suspend option
- [x] Restart `OVRService` after resume from sleep
- [x] Tray + power settings window

## Later

- Steam / Meta library scan and launch
- Hotkeys
- Optional voice commands (low priority)
- ADB helpers for Quest
- Elevated helper process so the tray app itself stays unelevated
- Profile ignore-list
- FOV multiplier UI
- Detect wired Link vs Air Link
- Dynamic bitrate max UI
- Separate communications audio devices

## Non-goals for now

- Replicating every OTT advanced/obscure tweak
- Shipping a full dash customizer (that belongs in Dash Manager)
- macOS / Linux
