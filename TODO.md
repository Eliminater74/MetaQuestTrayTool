# TODO

Check items off as they land. Keep this in sync with [ROADMAP.md](ROADMAP.md).

**Current public release:** [v1.1.34](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## Remaining / later

- [ ] Post-fix physical regression validation from issue #4 and the open issue #11 bitrate report: verify 960 fixed bitrate, DBR enabled + DBRMax 960, OVRService restart and stream effects, Mobile ASW, legacy HUD/capture support, headset recording/download, performance sampling, reset live ADB defaults, experimental rendering props, and wireless/text actions on physical hardware.

- [ ] Physical Quest validation for v1.1.34 high Link bitrate and DBRMax startup preservation, recording/download finalization, performance-sample parsing, active stream bitrate adoption, OVRService restart effects, Read Live, active Meta Link detection with resident Virtual Desktop processes, Link, screenshot, headset-announcement, reset-defaults, experimental-rendering, USB ADB, wireless ADB, voice command, default hotkey, and tray/page command flows.
- [ ] Authenticode code signing for Setup.exe (+ published exe) in the Release workflow — reduces SmartScreen friction; wait until budget allows (OV cert + timestamp; prefer cloud signing / Actions secrets, not a key in the repo)
- [ ] Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- [ ] Hotkey profiles per game (global only today)
- [ ] More built-in presets as people request process names
- [ ] Later experimental VrRuntime Lab: research accepted values before exposing swap interval, dynamic-resolution scaling, colorspace, layer filters, force SpaceWarp, GFR, and higher conditional CPU/GPU levels. Keep normal CPU/GPU dropdowns at safer 0–4 until prerequisites and runtime evidence are verified.

**Not doing (non-goals):** Permanent AirLink; Dash Manager–style dash replace; revive Oculus Home / Homeless; macOS / Linux. See [ROADMAP.md](ROADMAP.md).

**Docs sync:** [README.md](README.md) · [REDDIT.md](REDDIT.md) · [docs/VOICE-AND-HOTKEYS.md](docs/VOICE-AND-HOTKEYS.md)

---

## Shipped checklist (history)

### v1.1.34

- [x] Preserve Dynamic Bitrate Max (`DBRMax`) values above the old 500 Mbps preset ceiling during startup auto-apply when the saved baseline is still old-capped.
- [x] Keep explicitly saved high fixed bitrate and DBRMax values authoritative over external ODT values.
- [x] Add regression coverage for fixed-only, DBRMax-only, both-high, saved-high-wins, and legacy-ceiling no-op cases.
- [x] Full local suite 168/168.
- [x] Update version, installer notes, current release documentation, release workflow default, and announcement draft to 1.1.34.
- [x] Tag and publish **v1.1.34**.

### v1.1.33

- [x] Add Quest Link bitrate presets through 960 Mbps.
- [x] Preserve an existing ODT bitrate above the old 500 Mbps preset ceiling during startup auto-apply when the saved baseline is still old-capped.
- [x] Start performance logcat at the headset's current timestamp without clearing history and parse VrRuntime / OVRPlugin metric lines without FPS text.
- [x] Wait for a new or changed headset recording to stabilize before Stop & download pulls it.
- [x] Require successful Debug Tool probing, readable versions, and no prerequisite warnings before automatic Meta compatibility baseline promotion.
- [x] Full local suite 166/166.
- [x] Update version, installer notes, current release documentation, release workflow default, and announcement draft to 1.1.33.

### v1.1.32

- [x] Keep Quest Link **Read live registry** read-only while controls load, preventing queued UI events from saving/applying settings.
- [x] Let live operable Meta Link cache evidence beat resident Virtual Desktop desktop processes while preserving stale/inoperable non-Meta guards.
- [x] Update version, installer notes, current release documentation, release workflow default, and announcement draft to 1.1.32.

### v1.1.31

- [x] Report profile applies as structured success, partial success, failed, skipped, and skipped-only no-change results.
- [x] Prefer the trusted Quest headset across USB and wireless ADB transports.
- [x] Add Start/Stop headset recording and 30/40 Mbps capture bitrate presets.
- [x] Split CPU and GPU headset levels into independent App default / 0-4 controls.
- [x] Filter refresh-rate options by headset model and clarify Quest 2/Pro and Quest 3/3S texture defaults.
- [x] Add Dynamic vs Fixed FFR and preserve App default as no new override.
- [x] Add collapsed experimental local dimming and subsampled foveation controls with Quest Pro gating for local dimming.
- [x] Add Reset live ADB overrides for documented defaults, with reboot fallback for unsafe clears.
- [x] Harden updater installer launch with private temp storage, final hash/size validation, and file-handle locking.
- [x] Move outer ADB process invocation to ArgumentList and async process waiting/cancellation cleanup.
- [x] Add pinned Inno Setup release install, CodeQL, Dependabot, coverage reporting, and locked restore to CI/release paths.
- [x] Add read-only Meta runtime compatibility checks with separate detected and validated Meta/ODT version tracking.
- [x] Add sanitized support ZIP export from Info.

- [x] Keep skipped-only profile applies from aborting launches under Steam Link / Virtual Desktop.
- [x] Keep Meta runtime drift warnings pending until the user runs the compatibility check or acknowledges externally validated versions.
- [x] Prevent older Info refresh tasks from overwriting newer compatibility/export results.
- [x] Handle OpenXR ActiveRuntime through explicit 64-bit and 32-bit registry views.
- [x] Expand support ZIP sanitization for IPv6, MAC/BSSID, email-looking strings, and filename-only export logging.
- [x] Label High Top FFR as legacy/VrApi and Capture FPS as legacy/firmware-dependent.
- [x] Add a 10-second headset runtime logcat performance sampler.
- [x] Add Stop & download latest headset recording into the local captures folder.
- [x] Update version, installer notes, current release documentation, release workflow default, and announcement draft to 1.1.31.

### v1.1.30

- [x] Repair clipped HotKeys controls and document Exit assignment.
- [x] Reproduce and repair device-cache selection hiding live Meta Link evidence.
- [x] Log skipped Quest Link writes with detected session and requested settings.
- [x] Add ten regression cases; full suite 106/106.
- [x] Update version, installer notes and current release documentation to 1.1.30.

### v1.1.29

- [x] Audit control/value ownership and commit each repair stage.
- [x] Correct Link/HUD mappings, FOV migration/reset, custom value preservation and headset input threading.
- [x] Add detailed audit evidence and isolated regressions; full suite 96/96.
- [x] Update version, installer notes and current release documentation to 1.1.29.

### v1.1.28

- [x] Keep Quest Link presets and saved Link fields editable while Steam Link / SteamVR or Virtual Desktop is active
- [x] Keep live Meta Link registry and ODT writes gated under non-Meta streamers
- [x] Show unmatched saved Quest Link settings as custom instead of visually falling back to Meta defaults
- [x] Add bindable Exit Meta Quest Tray Tool action for HotKeys and custom voice phrases
- [x] Bump version to **1.1.28**
- [x] Tag and publish **v1.1.28**

### v1.1.27

- [x] Add visible headset announcement checkboxes for HotKey action results, voice command results, and screenshot confirmations
- [x] Route screenshot success/failure speech through the screenshot announcement category
- [x] Route HotKey and voice command result speech through dedicated announcement categories
- [x] Bump version to **1.1.27**
- [x] Tag and publish **v1.1.27**

### v1.1.26

- [x] Add Quest Link / Air Link mirror screenshot capture through Meta `OculusMirror.exe`
- [x] Add smart screenshot action that prefers Quest Link mirror capture and falls back to ADB
- [x] Add default **Ctrl+Shift+Num 8** for smart screenshots and keep **Ctrl+Shift+Num 9** for ADB headset screenshots
- [x] Add dedicated tray **Screenshots** and **HotKeys / Voice** menus
- [x] Add screenshot and voice-command controls to the Headset, Quest Link, and Tray Tool pages

### v1.1.25

- [x] Share runtime probe snapshots across Status, Info, PCVR Ready, and tray menu refreshes
- [x] Make tray status refresh and Headset trust-banner refresh nonblocking
- [x] Serialize ADB command execution through one queue
- [x] Debounce high-churn Quest Link settings saves and flush pending saves on exit
- [x] Add Advanced repair/copy diagnostics for stuck session helpers
- [x] Add Quest screenshot capture from tray, **Ctrl+Shift+Num 9**, and voice
- [x] Speak **“Screenshot taken.”** in the headset when the announcement audio route is available
- [x] Emit, verify, upload, and document release installer SHA-256 checksum sidecars

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
- [x] Headset announcements (TTS in Quest on connect / SteamVR wait / 10s OVR stop / profile / launch)
- [x] Fix false PreventDashLaunch SteamVR auto-start when Link not streaming
- [x] Expand voice commands (recover PCVR, audio, OpenXR, overlays, GPU preset)
- [x] Bump version to **1.1.2**
- [x] Tag and publish **v1.1.2**
- [x] GitHub Wiki user guide + walkthrough video
- [x] Fix false Link session-end toast after PC wake
- [x] Bump version to **1.1.3**
- [x] Tag and publish **v1.1.3**
- [x] Durable settings/profiles (write-through, `.bak`/`.bak2`, auto-restore after power-loss truncate; saves work after corrupt load)
- [x] Bump version to **1.1.16**
- [x] Tag and publish **v1.1.16**
- [x] Neon VR icon/logo (tray, About, sidebar) + `scripts/build-icons.ps1`
- [x] Exit/unhandled-exception logging for crash diagnosis
- [x] Bump version to **1.1.17**
- [x] Tag and publish **v1.1.17**
- [x] Setup-aware PCVR Ready / Status OpenXR checks
- [x] Informed connect TTS (SteamVR vs Meta Horizon) + Link audio delay
- [x] Bump version to **1.1.18**
- [x] Tag and publish **v1.1.18**
- [x] Expand headset voice coverage with game/profile, action, audio, headset, recovery, and experimental results
- [x] Add opt-in experimental MSFS 2024 VR launch automation
- [x] Audit and harden MSFS targeting, profile launch safety, helper IPC, TTS lifecycle, and voice fallback
- [x] Bump version to **1.1.19**
- [x] Tag and publish **v1.1.19**
- [x] Full v1.1.23 audit hardening: ADB/update/profile/URL/service/startup/power safety, process-handle cleanup, log redaction, docs/build-flow alignment
- [x] Bump version to **1.1.23**
- [x] Tag and publish **v1.1.23**
- [x] Fix tray Exit/session-helper shutdown so a stuck helper cannot leave the next launch hidden
- [x] Bump version to **1.1.24**
- [x] Tag and publish **v1.1.24**
- [x] Bump version to **1.1.25**
- [x] Tag and publish **v1.1.25**
- [x] Bump version to **1.1.26**
- [x] Tag and publish **v1.1.26**
- [x] Bump version to **1.1.27**
- [x] Tag and publish **v1.1.27**
- [x] Bump version to **1.1.28**
- [x] Tag and publish **v1.1.28**

### Housekeeping (ongoing)

- [x] Keep README "what works now" in sync after each phase
- [x] Bump version in Directory.Build.props when a phase ships
- [x] Decide on a license before a public release
