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
- [x] Ignore-list for noisy processes

### Phase 3 — Quest Link / Air Link

Writes Meta's own Link registry hive (`HKCU\Software\Oculus\RemoteHeadset`).

- [x] Bitrate, encode resolution width, HEVC preference, sliced-encoding off
- [x] Distortion curve, dynamic bitrate (DBR / DBR max / offset), Mobile ASW mode
- [x] Tray submenu + Link settings window + dashboard status
- [x] Store preferred Link settings separately from game profiles
- [x] Optional apply on app start
- [x] ODT registry reference ([docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md))
- [x] Detect wired Link vs Air Link when possible (Meta `DeviceCache.json` → `isUsingAirLink`; Steam Link / VD via processes)

## Next

### Phase 4 — Audio switching

- [x] List playback / recording devices
- [x] Switch to headset devices when Link audio becomes active
- [x] Restore fallback devices when Link audio disappears
- [x] Tray + audio settings window
- [x] Trigger modes: Link audio device (default) or Oculus service
- [x] Optional separate communications device pickers in the UI

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

- [x] Bundle Google platform-tools ADB (plus SDK / PATH / SideQuest fallback)
- [x] CPU/GPU, texture size, refresh rate, FFR, chromatic aberration
- [x] Capture size / FPS / bitrate / full-rate
- [x] Apply on connect + tray + Headset shell page
- [x] Paste text, proximity, guardian pause

### v1.0.0 — release polish

- [x] Product name **Meta Quest Tray Tool**, author **Eliminater74**, single version in Directory.Build.props
- [x] Custom CLI + ADB commands on global + personal profiles
- [x] Settings export / import
- [x] Trusted headset serial (block rogue ADB devices)
- [x] Info page + visible Donate (PayPal)
- [x] Inno Setup OTT-style Setup.exe (self-contained win-x64)

### Phase 6 — OTT-style settings shell (v0.6)

Modern sidebar UI matching classic Oculus Tray Tool tabs, wired to today’s Meta Link / OVRService stack.

- [x] Sidebar: Game Settings, Tray Tool, Power Options, Service & Startup, Log, Advanced, Quest Link
- [x] Tray opens the shell instead of the old dashboard
- [x] Service automation (start/stop with tool, wake restart, optional Home launch)
- [x] Expanded settings model for FOV H/V, Link sharpening, power triggers
- [x] Hotkeys configure UI + global RegisterHotKey bindings
- [x] Voice commands core (Windows speech, push-to-talk, phrase → HotKeyCommandService)
- [x] Voice polish (custom phrases, always-on confidence, mic picker)
- [ ] Oculus Homeless (largely covered by Dash → SteamVR / PreventDashLaunch)
- [x] Dash → SteamVR (kill Dash + launch SteamVR; PreventDashLaunch; CoreChannel LIVE/PublicTest/NO_UPDATES; hotkey/voice/auto)

### Post v1.0.0 (v1.0.1)

- [x] ODT registry map + expanded RemoteHeadset writes
- [x] Global hotkeys (HotKeyCommandService + configure UI)
- [x] Voice command preview (System.Speech + PTT)
- [x] Service & Startup Start/Stop button state
- [x] Profile restore on game exit
- [x] Release version sync + root LICENSE
- [x] In-app updates (GitHub latest release → Setup.exe → exit → install over)
- [x] Tag **v1.0.2** with updater included
- [x] README screenshots + demo video
- [x] Open Meta Horizon Link + audio auto-switch fix
- [x] Tag **v1.0.3**
- [x] Periodic update checks + PCVR session detect (Air/wired/Steam/VD) + Steam Link OpenXR assist
- [x] Tag **v1.0.4**
- [x] Quest Link presets + clearer Info Link/ADB + session connect/apply logging
- [x] Tag **v1.0.5**
- [x] Prefer SteamVR over Meta Wi‑Fi auto-connect DeviceCache
- [x] Tag **v1.0.6**
- [x] Dash → SteamVR + PreventDashLaunch + CoreChannel
- [x] Tag **v1.0.7**
- [x] Kill ADB before in-app update / Setup (platform-tools replace)
- [x] Hover tooltips across shell pages and settings windows
- [x] Open Oculus Debug Tool GUI (tray / Service & Startup / Game Settings / Advanced / hotkey / voice)
- [x] Tag **v1.0.8**
- [x] Cut idle CPU / snappy sidebar (process watcher, probe caches, deferred Refresh)
- [x] Tag **v1.0.9**
- [x] Wireless ADB connect / tcpip / auto-reconnect
- [x] Further idle-CPU cuts (audio cache, slower polls, background watchers)
- [x] Tag **v1.0.10**
- [x] Thread-safe Link/audio/ADB probe caches (tray menu race fix)
- [x] Tag **v1.0.11**
- [x] Steam-first PCVR polish: Ready checklist, battery/Wi‑Fi, session recover, library launch, profile ignore, last-good, comms audio, overlay close, Perf HUD cycle
- [x] Wireless ADB pairing-code UI
- [x] Voice polish — custom phrases, mic picker, always-on confidence
- [x] Version **1.0.12**

## Later

- Authenticode code signing for the installer / exe (SmartScreen; when budget allows)
- Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- Oculus Homeless (largely covered by Dash → SteamVR / PreventDashLaunch)

## Non-goals for now

- Replicating every OTT advanced/obscure tweak
- Shipping a full dash customizer (that belongs in Dash Manager)
- macOS / Linux
