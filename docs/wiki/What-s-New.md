# What's new

This page catches the wiki up from the older v1.1.18-era guide to the current public release, **v1.1.34**.

Download: [latest release](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)  
Full history: [CHANGELOG.md](https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/CHANGELOG.md)

## Current release: v1.1.34

v1.1.34 completes the high-bitrate startup protection on the Quest Link path. Startup auto-apply now preserves both:

- fixed Link bitrate values above the old 500 Mbps preset ceiling
- Dynamic Bitrate Max (`DBRMax`) values above that old ceiling

If your saved tray baseline is still an old 500 Mbps value but Oculus Debug Tool already has a higher value such as 960 Mbps, startup no longer silently lowers it. If you explicitly saved a high value in the tray, that saved value remains the source of truth.

Physical confirmation from issue #11 is still useful: the public build is shipped and locally regression-tested, but the reporter still needs to confirm the 960 Mbps startup path on their real Quest installation.

## Quest Link and streamer detection

Recent releases added and hardened:

- Quest Link bitrate presets through **960 Mbps**
- independent preservation for fixed bitrate and DBRMax during startup apply
- **Read live registry** on the Quest Link page without accidentally saving or applying the values it just read
- registry write verification with mismatch reporting
- custom Quest Link values that stay custom instead of snapping visually to a preset
- saved Quest Link presets that remain editable while Steam Link / SteamVR or Virtual Desktop is the active streamer
- better active Meta Link detection so live Link evidence wins over stale or idle `DeviceCache.json` entries
- clearer skipped-write logging when the current streamer owns its own bitrate path

Use [[Quest-Link]] for the controls and [[Quest-Link-vs-Steam-Link]] for the transport split.

## Screenshots, recording, and headset diagnostics

The app now has three screenshot paths:

- **Take screenshot**: tries Quest Link / Air Link mirror capture first, then falls back to ADB
- **Take Quest Link mirror screenshot**: uses Meta's `OculusMirror.exe`; no ADB required, but Link must be actively streaming
- **Take headset screenshot (ADB)**: uses the trusted Quest ADB path

Headset recording and diagnostic additions include:

- Start / Stop recording controls
- **Stop & download latest recording** into `%AppData%\MetaQuestTrayTool\captures\`
- stabilization checks before downloading a just-stopped recording
- capture bitrate presets up to 40 Mbps
- 10-second runtime performance samples from headset logcat
- performance logcat that starts at the headset's current timestamp instead of clearing history
- parsing for current VrRuntime / OVRPlugin metrics even when FPS text is absent

See [[Screenshots-and-Videos]] and [[Headset-ADB]].

## Headset ADB controls

Recent Headset page work added:

- trusted headset selection shared across USB and wireless ADB
- independent CPU and GPU levels
- model-aware refresh-rate options
- Dynamic FFR versus Fixed FFR controls
- guarded experimental rendering controls
- Reset live ADB overrides for documented defaults
- VR-headsets-only protection for wireless ADB
- Pause ADB until resume, or for two hours

ADB props are runtime headset overrides. Most reset when the Quest reboots; leave apply-on-connect enabled if you want the tray to reapply them.

## HotKeys, voice, and announcements

Recent control updates added:

- reachable, scrollable HotKey editing
- default smart screenshot and ADB screenshot hotkeys
- bindable Quest Link mirror screenshot, Open Debug Tool, Open SteamVR Home, Recover PCVR, audio, OpenXR, overlay, GPU preset, and Exit actions
- matching custom voice phrases for those actions
- separate headset announcement toggles for HotKey results, voice command results, screenshot confirmations, manual action results, headset/ADB results, and experimental launch results
- selectable Windows TTS voice for headset announcements and voice confirmations
- expanded headset speech for profile apply/restore, transport, OpenXR runtime, audio routing, SteamVR start/exit, and PCVR recovery

Configure these before going into VR. An elevated tray cannot be clicked from SteamVR.

## SteamVR, OpenXR, audio, and startup

Recent Steam-first PCVR updates include:

- Start SteamVR from the tray, Status page, Service & Startup, hotkey, or voice without enabling PreventDashLaunch
- on-demand SteamVR Home launch
- PreventDashLaunch path that blocks Meta Dash through registry only, without killing Meta processes
- optional 10-second OVRService hold after SteamVR exits so Link can drop cleanly back toward Quest Home
- Steam Link assist that switches OpenXR to SteamVR while Steam Link / SteamVR is active
- separate 64-bit and 32-bit OpenXR registry diagnostics
- audio switching that avoids stealing desktop speakers just because Meta virtual audio exists
- manual-at-boot OVRService control so Meta does not pop at Windows sign-in

See [[Dash-to-SteamVR]], [[Service-and-Startup]], and [[Audio-and-Power]].

## Updates, diagnostics, and release safety

The app now has stronger maintenance and support tooling:

- private, revalidated in-app updater downloads
- release `Setup.exe` SHA-256 sidecar files
- installer size/hash checks before launching an update
- update cleanup that only stops this app's bundled ADB copy
- CodeQL and Dependabot coverage in hosted GitHub checks
- locked restore and release packaging checks
- coverage gate raised to 8%
- sanitized support ZIP export
- persistent read-only Meta runtime compatibility checks
- nonblocking Status/Info/tray probes
- shared runtime snapshots to avoid repeated expensive probes
- bounded service/startup/power command runners
- safer session-helper ownership, shutdown, and repair diagnostics
- durable settings/profile saves with `.bak` and `.bak2` recovery after power loss

Use [[Troubleshooting]] when something does not apply, and include the sanitized diagnostics when opening an issue.

## Still needs physical confirmation

Local tests and CI are green for the public release, but some behavior depends on Meta runtime, firmware, headset model, and the active stream. The current open physical checks are:

- 960 Mbps fixed Link bitrate on a real Link / Air Link session
- DBR enabled with DBRMax 960
- active-stream bitrate adoption after reconnect or OVRService restart
- headset recording finalization on current firmware
- performance sample fields emitted by current firmware/apps
- Mobile ASW and legacy HUD/capture behavior
- wireless ADB, screenshots, headset announcements, and reset-defaults flows on physical hardware

These are validation limits, not hidden extra setup steps.
