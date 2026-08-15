# TODO

Check items off as they land. Keep this in sync with [ROADMAP.md](ROADMAP.md).

**Current public release:** [v1.1.12](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## Remaining / later

- [ ] Authenticode code signing for Setup.exe (+ published exe) in the Release workflow — reduces SmartScreen friction; wait until budget allows (OV cert + timestamp; prefer cloud signing / Actions secrets, not a key in the repo)
- [ ] Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- [ ] Hotkey profiles per game (global only today)
- [ ] More built-in presets as people request process names

**Not doing (non-goals):** Permanent AirLink; Dash Manager–style dash replace; revive Oculus Home / Homeless; macOS / Linux. See [ROADMAP.md](ROADMAP.md).

**Docs sync:** [README.md](README.md) · [REDDIT.md](REDDIT.md) · [docs/VOICE-AND-HOTKEYS.md](docs/VOICE-AND-HOTKEYS.md)

---

## Shipped checklist (history)

### v0.1 — tray host

- [x] WPF .NET 8 solution that opens in Visual Studio Community
- [x] Tray icon and right-click menu
- [x] Dashboard window (left-click)
- [x] Detect Oculus install path and `OVRService`
- [x] Start / stop / restart `OVRService`
- [x] Persist settings to `%AppData%\MetaQuestTrayTool\settings.json`
- [x] Start with Windows (HKCU Run)
- [x] Optional Start with Windows as Administrator (elevated logon task)
- [x] Activity log on the dashboard and on disk
- [x] Project README, roadmap, and this TODO

### Phase 1 — game settings (Debug Tool CLI)

- [x] `OculusDebugToolService` that writes a command file and runs `OculusDebugToolCLI.exe -f`
- [x] Super Sampling options on the tray menu
- [x] ASW mode options on the tray menu
- [x] Apply current defaults from the dashboard
- [x] Persist default SS / ASW / FOV
- [x] Show Debug Tool path and last apply result on the dashboard
- [x] Warn in the log when Meta rejects `server:` commands
- [x] Wire Adaptive GPU, mip layer flags, FOV stencil, Visual HUD, ASW 30/18
- [x] Probe headset serials via `server:EnumHmd`

### Phase 2 — profiles

- [x] Profile model + JSON store
- [x] Profile editor window
- [x] Tray submenu of saved profiles
- [x] Process watcher that applies a profile on game launch
- [x] Restore defaults when the watched process exits
- [x] CPU priority for the detected process
- [x] Optional Link overrides on personal profiles (sharpening, bitrate, encode width)

### Phase 3 — Quest Link / Air Link

- [x] Link settings model (bitrate, encode resolution, dynamic bitrate)
- [x] Apply / read Link-related settings
- [x] Tray + dashboard UI
- [x] Distortion curve, DBR / DBR max / DBR offset, Mobile ASW mode (ODT RemoteHeadset hive)
- [x] Document ODT registry vs CLI ([docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md))
- [x] Detect wired Link vs Air Link when possible (DeviceCache `isUsingAirLink` + Steam/VD heuristics)

### Phase 4 — audio

- [x] Enumerate playback and recording devices
- [x] Switch defaults when Oculus / Link becomes active
- [x] Restore previous devices when VR stops
- [x] Tray / dashboard UI for VR and fallback devices
- [x] Link-audio trigger (restore when headset endpoint disappears, not only when OVRService stops)

### Library / profiles polish

- [x] Scan Steam installed games
- [x] Scan Meta / Oculus installed apps
- [x] Library picker for personal profiles
- [x] Global defaults editor separate from personal profiles
- [x] Steam + Meta cover art in the library picker and profiles list

### Phase 5 — power / USB

- [x] Power plan switch while VR is running
- [x] USB selective suspend option
- [x] Restart `OVRService` after sleep

### Phase 7 — OpenXR switch (v0.7)

- [x] Meta vs SteamVR ActiveRuntime registry switch
- [x] Global + personal profile OpenXR choice
- [x] Restore previous / global runtime when the game exits

### Phase 8 — elevated start (v0.7.1 / v0.7.2)

- [x] Optional logon scheduled task with highest privileges
- [x] One-shot Restart as Administrator
- [x] Hands-free by default: auto-relaunch elevated once, then silent at logon
- [x] No mid-session UAC (OpenXR / service) — headset blocks those prompts

### Phase 6 — OTT-style shell (v0.6)

- [x] Sidebar MainShell with OTT tab layout
- [x] Game / Tray / Power / Service / Log / Advanced / Quest Link pages
- [x] Wire existing services into the shell
- [x] Tray left-click opens shell
- [x] Hotkeys (global shortcuts + configure UI; default Ctrl+Numpad 0–9)
- [x] Voice commands core (Windows speech, push-to-talk, phrase → HotKeyCommandService)
- [x] Service & Startup: Start/Stop accent follows live OVRService state
- [x] Removed obsolete Oculus Home / Homeless UI (Meta removed Home years ago)
- [x] Optional SteamVR Home (steamtours) — Service & Startup / tray / hotkey / voice
- [x] Voice polish (custom phrases, mic picker, always-on confidence)

### Phase 9 — headset ADB (v0.8)

- [x] Detect adb.exe (bundled platform-tools first, then SDK / PATH / SideQuest)
- [x] Ship Google platform-tools ADB with the app
- [x] CPU/GPU, texture size, refresh, FFR, chroma, capture
- [x] Auto-apply when the Quest appears on ADB
- [x] Paste text to headset; proximity / guardian helpers
- [x] Wireless ADB connect UI (host/port, tcpip helper, auto-reconnect)

### v1.1 — profiles, presets, global baseline

- [x] Tray notifications when a profile applies and when global restores after exit
- [x] Global baseline on tool start, VR headset connect, and after profile exit
- [x] Dedicated profiles.json store (no SQL — simpler backup)
- [x] Built-in global presets (Balanced, Performance, Quality, Sim, Competitive)
- [x] Built-in PCVR game presets (MSFS 2024, Beat Saber, HL:Alyx, DCS, etc.)
- [x] OTT-style Windows Setup.exe (Inno Setup + self-contained publish)
- [x] GitHub Actions CI + Release (tag v* → Setup.exe on Releases)

### v1.0.0

- [x] Single version + product name in Directory.Build.props
- [x] Author credit: Eliminater74
- [x] Custom CLI / ADB commands on global defaults and personal profiles
- [x] Settings export / import backup
- [x] Trusted headset serial + rogue-device block
- [x] Info page (OpenXR live, OVRService, headset identity)
- [x] Visible Donate (sidebar, About, tray)
- [x] Paste live donate URL into AppInfo.DonateUrl

### Post v1.0.0 releases

- [x] ODT registry reference doc + full RemoteHeadset writes (distortion, DBR, Mobile ASW)
- [x] Global hotkeys + HotKeysWindow configure UI
- [x] Voice command core + VoiceCommandsWindow
- [x] Service page Start/Stop button highlighting fix
- [x] Profile restore on game exit
- [x] Release version sync (exe matches installer)
- [x] Hotkey/voice reliability (conflicts, import reload, phrase matching)
- [x] Full Link fields in tray Link window
- [x] Root LICENSE.txt
- [x] Tag and publish **v1.0.1**
- [x] In-app update check (GitHub latest `v*` → download Setup → exit → install over)
- [x] Tag and publish **v1.0.2** (with in-app updater)
- [x] README screenshots + demo video
- [x] Open Meta Horizon Link (tray / hotkey / voice)
- [x] Audio switch: do not steal speakers when Oculus virtual audio is merely installed
- [x] Tag and publish **v1.0.3**
- [x] Configurable periodic update checks
- [x] Detect Meta Air Link vs wired + Steam Link / Virtual Desktop sessions
- [x] Gate Meta Link/ODT under VD / Steam Link; Steam Link OpenXR assist + restore
- [x] Tag and publish **v1.0.4**
- [x] Quest Link presets + clearer Info Link/ADB status
- [x] Log Link/ADB connect/disconnect and applies
- [x] Tag and publish **v1.0.5**
- [x] Prefer SteamVR over Meta Wi‑Fi auto-connect DeviceCache
- [x] Tag and publish **v1.0.6**
- [x] Dash → SteamVR (kill Dash + launch SteamVR; hotkey/voice/auto)
- [x] PreventDashLaunch registry + auto-start SteamVR when enabled
- [x] CoreChannel flip (LIVE / PublicTest / NO_UPDATES) + optional NO_UPDATES with PreventDash
- [x] Steam Link gating when EnumHmd sees Meta auto-connect
- [x] Tag and publish **v1.0.7**
- [x] Kill ADB before in-app update so Setup can replace platform-tools
- [x] Hover tooltips across remaining pages and windows
- [x] Open Oculus Debug Tool GUI (classic OTT shortcut)
- [x] Tag and publish **v1.0.8**
- [x] Cut idle CPU and snappy sidebar navigation (watcher / cache / defer Refresh)
- [x] Tag and publish **v1.0.9**
- [x] Wireless ADB connect, tcpip helper, and auto-reconnect
- [x] Further idle-CPU cuts (audio/USB caches, slower polls, off-UI watchers)
- [x] Tag and publish **v1.0.10**
- [x] Thread-safe Link/audio/ADB caches (fix concurrent Dictionary crash)
- [x] Tag and publish **v1.0.11**
- [x] PCVR Ready checklist (Steam-biased)
- [x] Headset battery / charge / Wi‑Fi via ADB
- [x] Recover PCVR after Link drop
- [x] Library / profile Launch (steam://run)
- [x] Profile ignore list
- [x] Tray now-playing + Switch OpenXR → SteamVR + SteamVR Video hints
- [x] Save last-good to active profile
- [x] Separate communications audio device pickers
- [x] Auto-close overlays on Link connect
- [x] Perf HUD tray cycle
- [x] Wireless ADB pairing-code UI
- [x] Voice polish (custom phrases, mic picker, always-on confidence)
- [x] Status page as default opening view (live chips)
- [x] SteamVR install / version / Stable vs Beta detect
- [x] VR Tools page + tray menu (curated third-party links)
- [x] Bump version to **1.0.12**
- [x] Tag and publish **v1.0.12**
- [x] Remove Oculus Home / Homeless leftovers; add SteamVR Home open action
- [x] Bump version to **1.0.13**
- [x] Tag and publish **v1.0.13**
- [x] Refresh README / REDDIT / ROADMAP / TODO for v1.0.13 accuracy
- [x] Enlarge main shell + scrollable nav (Info not clipped)
- [x] Fix Game Settings OpenXR checkboxes wiped on Refresh
- [x] Bump version to **1.0.14**
- [x] Tag and publish **v1.0.14**
- [x] Audio switcher: leave boot audio alone; switch only on PCVR start/end
- [x] Bump version to **1.0.15**
- [x] Tag and publish **v1.0.15**
- [x] Adaptive IdleCadence — quiet tray watchers until PCVR / armed features
- [x] Audit: SyncTimer / SyncSessionWatch wiring; stop disabled watchers; pause Status/Info when shell hidden
- [x] Bump version to **1.0.16**
- [x] Tag and publish **v1.0.16**
- [x] Restart OVRService when SteamVR exits (PreventDashLaunch / stuck-in-Link fix)
- [x] PreventDashLaunch-only — remove Meta process killing (Dash reaper, close client)
- [x] Docs: Steam Link vs Quest Link; Meta Horizon Link startup investigation
- [x] Fix PreventDashLaunch auto SteamVR on DeviceCache auto-connect ghost
- [x] Fix Check for updates crash (null MessageBox owner from tray)
- [x] Restore desktop audio when PCVR / SteamVR session ends
- [x] Fix PreventDashLaunch auto SteamVR on tray start (EnumHmd-only ghost)
- [x] Bump version to **1.1.0**
- [x] Tag and publish **v1.1.0**
- [x] OVRService Manual-at-boot toggle (Service & Startup) + re-apply after Meta reset
- [x] Bump version to **1.1.1**
- [x] Tag and publish **v1.1.1**
- [x] Headset announcements (TTS in Quest on connect/profile/launch/Dash→SteamVR)
- [x] Fix false PreventDashLaunch SteamVR auto-start when Link not streaming
- [x] Expand voice commands (recover PCVR, audio, OpenXR, overlays, GPU preset)
- [x] Bump version to **1.1.2**
- [x] Tag and publish **v1.1.2**
- [x] GitHub Wiki user guide + walkthrough video
- [x] Fix false Link session-end toast after PC wake
- [x] Bump version to **1.1.3**
- [x] Tag and publish **v1.1.3**

### Housekeeping (ongoing)

- [x] Keep README "what works now" in sync after each phase
- [x] Bump version in Directory.Build.props when a phase ships
- [x] Decide on a license before a public release
