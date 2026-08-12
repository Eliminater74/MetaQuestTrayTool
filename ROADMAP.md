# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

## Done

### v0.1 — tray host

The app lives in the notification area, opens a dashboard, tracks `OVRService`, and stores settings.

### Phase 1 — Game settings via Oculus Debug Tool CLI

Tray + dashboard push pixel density and ASW through `OculusDebugToolCLI.exe`.

- [x] Apply supersampling (`service set-pixels-per-display-pixel-override`)
- [x] Apply ASW (`server:asw.Off` / `Auto` / `Clock45` / `Clock30` / `Clock18`)
- [x] Adaptive GPU scale, force/offset mip on layers, FOV stencil, Perf HUD
- [x] `server:EnumHmd` headset serial probe at startup
- [x] Tray submenu to change defaults and apply now
- [x] Persist last-used defaults in `settings.json`
- [x] Log CLI stdout/stderr and warn when Meta rejects `server:` commands
- [x] FOV multiplier UI (CLI command is already sent when the stored value is not 1.0)

### Phase 2 — Profiles

- [x] Create / edit / delete profiles (name, process, SS, ASW, FOV, CPU priority, notes)
- [x] Profile list on the dashboard and tray
- [x] Watch running processes and apply the matching profile
- [x] Restore defaults when the game exits
- [x] CPU priority for the detected process
- [x] Optional Link overrides on personal profiles (sharpening, bitrate, encode width; inherit = keep global)
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
- [x] Switch to headset devices when Link audio becomes active
- [x] Restore fallback devices when Link audio disappears
- [x] Tray + audio settings window
- [x] Trigger modes: Link audio device (default) or Oculus service
- [ ] Optional separate communications device pickers in the UI

### Phase 2 / library polish

- [x] Steam + Meta library scan
- [x] Add personal profiles from library
- [x] Global defaults editor vs personal per-app profiles

### Phase 5 — Power and USB

- [x] Switch Windows power plan while VR / Oculus service is active
- [x] USB selective suspend option
- [x] Restart `OVRService` after resume from sleep
- [x] Tray + power settings window

### Phase 7 — OpenXR runtime switch (v0.7)

- [x] Detect Meta and SteamVR OpenXR JSON manifests
- [x] Switch `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` (elevate if needed)
- [x] Global preferred runtime + apply on start
- [x] Per-game profile override with restore on exit

### Phase 8 — elevated start (v0.7.1 / v0.7.2)

Classic OTT ran elevated so VR-time work needed no UAC. A Windows Service cannot host a tray icon (Session 0).

- [x] Hands-free by default: one UAC, then elevated at every logon
- [x] Logon scheduled task with highest privileges
- [x] No mid-session UAC for OpenXR / OVRService (unreachable in headset)
- [x] Opt out from Tray Tool / Service & Startup if you want a normal user tray

### Phase 9 — headset ADB (v0.8)

SideQuest-style `debug.oculus.*` props. They reset on reboot, so the tray re-applies when the headset connects.

- [x] Find adb.exe (platform-tools, PATH, SideQuest)
- [x] CPU/GPU, texture size, refresh rate, FFR, chromatic aberration
- [x] Capture size / FPS / bitrate / full-rate
- [x] Apply on connect + tray + Headset shell page
- [x] Paste text, proximity, guardian pause

### Phase 6 — OTT-style settings shell (v0.6)

Modern sidebar UI matching classic Oculus Tray Tool tabs, wired to today’s Meta Link / OVRService stack.

- [x] Sidebar: Game Settings, Tray Tool, Power Options, Service & Startup, Log, Advanced, Quest Link
- [x] Tray opens the shell instead of the old dashboard
- [x] Service automation (start/stop with tool, wake restart, optional Home launch)
- [x] Expanded settings model for FOV H/V, Link sharpening, power triggers
- [ ] Hotkeys configure UI
- [ ] Voice / Homeless / Visual HUD (legacy OTT features — evaluate if still useful)

## Later

- Hotkeys
- Optional voice commands (low priority)
- Wireless ADB pairing UI (USB ADB headset page is in v0.8)
- Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- Profile ignore-list
- Detect wired Link vs Air Link
- Dynamic bitrate max registry write (UI exists; needs verification)
- Separate communications audio device pickers in the UI
- Launch games directly from the library picker
- In-app update checks

## Non-goals for now

- Replicating every OTT advanced/obscure tweak
- Shipping a full dash customizer (that belongs in Dash Manager)
- macOS / Linux
