# TODO

Check items off as they land. Keep this in sync with [ROADMAP.md](ROADMAP.md).

## v0.1 — tray host

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

## Phase 1 — game settings (Debug Tool CLI)

- [x] `OculusDebugToolService` that writes a command file and runs `OculusDebugToolCLI.exe -f`
- [x] Super Sampling options on the tray menu
- [x] ASW mode options on the tray menu
- [x] Apply current defaults from the dashboard
- [x] Persist default SS / ASW / FOV
- [x] Show Debug Tool path and last apply result on the dashboard
- [x] Warn in the log when Meta rejects `server:` commands
- [x] Wire Adaptive GPU, mip layer flags, FOV stencil, Visual HUD, ASW 30/18
- [x] Probe headset serials via `server:EnumHmd`

## Phase 2 — profiles

- [x] Profile model + JSON store
- [x] Profile editor window
- [x] Tray submenu of saved profiles
- [x] Process watcher that applies a profile on game launch
- [x] Restore defaults when the watched process exits
- [x] CPU priority for the detected process
- [x] Optional Link overrides on personal profiles (sharpening, bitrate, encode width)

## Phase 3 — Quest Link / Air Link

- [x] Link settings model (bitrate, encode resolution, dynamic bitrate)
- [x] Apply / read Link-related settings
- [x] Tray + dashboard UI
- [x] Distortion curve, DBR / DBR max / DBR offset, Mobile ASW mode (ODT RemoteHeadset hive)
- [x] Document ODT registry vs CLI ([docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md))
- [ ] Detect wired Link vs Air Link when possible

## Phase 4 — audio

- [x] Enumerate playback and recording devices
- [x] Switch defaults when Oculus / Link becomes active
- [x] Restore previous devices when VR stops
- [x] Tray / dashboard UI for VR and fallback devices
- [x] Link-audio trigger (restore when headset endpoint disappears, not only when OVRService stops)

## Library / profiles polish

- [x] Scan Steam installed games
- [x] Scan Meta / Oculus installed apps
- [x] Library picker for personal profiles
- [x] Global defaults editor separate from personal profiles
- [x] Steam + Meta cover art in the library picker and profiles list

## Phase 5 — power / USB

- [x] Power plan switch while VR is running
- [x] USB selective suspend option
- [x] Restart `OVRService` after sleep

## Phase 7 — OpenXR switch (v0.7)

- [x] Meta vs SteamVR ActiveRuntime registry switch
- [x] Global + personal profile OpenXR choice
- [x] Restore previous / global runtime when the game exits

## Phase 8 — elevated start (v0.7.1 / v0.7.2)

- [x] Optional logon scheduled task with highest privileges
- [x] One-shot Restart as Administrator
- [x] Hands-free by default: auto-relaunch elevated once, then silent at logon
- [x] No mid-session UAC (OpenXR / service) — headset blocks those prompts

## Phase 6 — OTT-style shell (v0.6)

- [x] Sidebar MainShell with OTT tab layout
- [x] Game / Tray / Power / Service / Log / Advanced / Quest Link pages
- [x] Wire existing services into the shell
- [x] Tray left-click opens shell
- [x] Hotkeys (global shortcuts + configure UI; default Ctrl+Numpad 1–8)
- [x] Voice commands core (Windows speech, push-to-talk, phrase → HotKeyCommandService)
- [x] Service & Startup: Start/Stop accent follows live OVRService state
- [ ] Legacy OTT extras (Homeless)
- [ ] Voice polish (custom phrases, mic picker, always-on tuning)

## Phase 9 — headset ADB (v0.8)

- [x] Detect adb.exe (bundled platform-tools first, then SDK / PATH / SideQuest)
- [x] Ship Google platform-tools ADB with the app
- [x] CPU/GPU, texture size, refresh, FFR, chroma, capture
- [x] Auto-apply when the Quest appears on ADB
- [x] Paste text to headset; proximity / guardian helpers
- [ ] Wireless ADB pairing UI

## v1.1 — profiles, presets, global baseline

- [x] Tray notifications when a profile applies and when global restores after exit
- [x] Global baseline on tool start, VR headset connect, and after profile exit
- [x] Dedicated profiles.json store (no SQL — simpler backup)
- [x] Built-in global presets (Balanced, Performance, Quality, Sim, Competitive)
- [x] Built-in PCVR game presets (MSFS 2024, Beat Saber, HL:Alyx, DCS, etc.)
- [x] OTT-style Windows Setup.exe (Inno Setup + self-contained publish)
- [x] GitHub Actions CI + Release (tag v* → Setup.exe on Releases)

## v1.0.0

- [x] Single version + product name in Directory.Build.props
- [x] Author credit: Eliminater74
- [x] Custom CLI / ADB commands on global defaults and personal profiles
- [x] Settings export / import backup
- [x] Trusted headset serial + rogue-device block
- [x] Info page (OpenXR live, OVRService, headset identity)
- [x] Visible Donate (sidebar, About, tray)
- [x] Paste live donate URL into AppInfo.DonateUrl

## Post v1.0.0 (main — pending v1.0.1 tag)

- [x] ODT registry reference doc + full RemoteHeadset writes (distortion, DBR, Mobile ASW)
- [x] Global hotkeys + HotKeysWindow configure UI
- [x] Voice command core + VoiceCommandsWindow (preview)
- [x] Service page Start/Stop button highlighting fix
- [x] Profile restore on game exit
- [x] Release version sync (exe matches installer)
- [x] Hotkey/voice reliability (conflicts, import reload, phrase matching)
- [x] Full Link fields in tray Link window
- [x] Root LICENSE.txt
- [ ] Tag and publish **v1.0.1** release on GitHub
- [ ] README screenshots

## Housekeeping

- [x] Keep README "what works now" in sync after each phase
- [x] Bump version in Directory.Build.props when a phase ships
- [x] Decide on a license before a public release
