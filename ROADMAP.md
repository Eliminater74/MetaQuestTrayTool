# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

## Now — v0.1 tray host

Done. The app lives in the notification area, opens a dashboard, tracks `OVRService`, and stores settings.

## Next

### Phase 1 — Game settings via Oculus Debug Tool CLI

OTT's main job: push pixel density and ASW into the runtime without opening the Debug Tool GUI.

- Apply supersampling (`service set-pixels-per-display-pixel-override`)
- Apply ASW (`server:asw.Off` / `Auto` / `Clock45`)
- Optional FOV multiplier (`service set-client-fov-tan-angle-multiplier`)
- Tray submenu to change defaults and apply now
- Persist last-used defaults in `settings.json`
- Log CLI stdout/stderr. Newer Meta builds may reject `server:` commands — surface that clearly

### Phase 2 — Profiles

Per-game overrides, the other half of OTT.

- Create / edit / delete profiles (name, process, SS, ASW, FOV, CPU priority, notes)
- Profile list on the dashboard and tray
- Watch running processes and apply the matching profile
- Restore defaults when the game exits
- Ignore-list for noisy processes

### Phase 3 — Quest Link / Air Link

- Bitrate, encode resolution, dynamic bitrate
- Detect wired Link vs Air Link when possible
- Store Link defaults separately from game profiles

### Phase 4 — Audio switching

- List playback / recording devices
- Switch to headset devices when VR starts
- Restore previous defaults when VR stops
- Optional separate communications devices

### Phase 5 — Power and USB

- Switch Windows power plan while VR is active
- USB selective suspend
- Restart `OVRService` after resume from sleep

### Later

- Steam / Meta library scan and launch
- Hotkeys
- Optional voice commands (low priority)
- ADB helpers for Quest
- Elevated helper process so the tray app itself stays unelevated

## Non-goals for now

- Replicating every OTT advanced/obscure tweak
- Shipping a full dash customizer (that belongs in Dash Manager)
- macOS / Linux
