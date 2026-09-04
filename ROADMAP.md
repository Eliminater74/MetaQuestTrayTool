# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

**Current public release:** [v1.1.28](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## Current product (v1.1.28)

Steam-first PCVR tray for Meta Quest Link + SteamVR OpenXR:

| Area | Shipped |
| --- | --- |
| Shell | Status (default), Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link, Headset, VR Tools, Info |
| Status / Ready | Live chips, SteamVR install/version/Stable\|Beta, PCVR Ready checklist, Recover PCVR, session probe (Air/wired/Steam/VD) |
| Game / profiles | ODT SS/ASW/FOV/HUD, Debug Tool GUI, auto profiles, library Launch, ignore list, last-good, overlays close |
| Link / Dash | RemoteHeadset Link settings + presets that remain editable while non-Meta streamers are active, Quest Link mirror screenshots, PreventDashLaunch → SteamVR over Link (registry only; no Meta process killing), CoreChannel, SteamVR Home (on demand), OVRService restart on SteamVR exit, OVRService Manual-at-boot toggle |
| OpenXR / audio / power | Meta vs SteamVR switch, Steam Link assist, comms audio pickers, power plan / USB / wake restart |
| Headset ADB | Wired + Wireless Pair/Connect/tcpip, CPU/GPU/refresh/FFR/capture, ADB Quest screenshots, battery/Wi‑Fi, trusted serial, **VR headsets only** toggle, **Pause ADB** (until resume / 2h) |
| Mid-session | HotKeys (Ctrl+Num 0–9, Ctrl+Shift+Num 0/8/9 plus bindable Exit), voice (PTT/mic/confidence/custom phrases + recover/audio/OpenXR/overlays/GPU/smart + Link + ADB screenshots + bindable Exit), expanded headset announcements with separate HotKey/voice/screenshot result toggles, experimental MSFS 2024 VR launch automation |
| Updates / polish | In-app GitHub updates, release checksum sidecars, shared/nonblocking status probes, helper repair diagnostics, [Wiki](https://github.com/Eliminater74/MetaQuestTrayTool/wiki), themes, tooltips, quiet idle cadence (stop disabled watchers; pause Status/Info when shell hidden), VR Tools links, Donate, durable settings/profiles (`.bak`/`.bak2` after power loss), neon icon/logo |

Checkbox history: [TODO.md](TODO.md). User-facing detail: [README.md](README.md).

---

## v1.1.28 — Quest Link preset editing and exit action

Released work:

- Quest Link preset and field editing remains available while Steam Link / SteamVR or Virtual Desktop is the current PCVR transport; only live Meta Link registry / ODT writes are skipped.
- Custom saved Quest Link settings display as custom instead of visually falling back to the first preset.
- Added bindable Exit Meta Quest Tray Tool action for HotKeys and custom voice-command phrases, with no default chord.

---

## v1.1.27 — headset announcement controls

Released work:

- Separate default-on headset announcement categories for HotKey action results, voice command results, and screenshot confirmations.
- Screenshot success/failure speech now routes through the screenshot category instead of the broader headset/ADB bucket.
- HotKey and voice command result speech now route through dedicated categories instead of the generic important-action bucket.

---

## v1.1.26 — Quest Link mirror screenshots

Released work:

- Quest Link / Air Link screenshots through Meta `OculusMirror.exe` while a live Meta Link stream is active.
- Smart screenshot action that prefers Quest Link mirror capture and falls back to trusted-headset ADB.
- Dedicated tray **Screenshots** and **HotKeys / Voice** submenus.
- Screenshot buttons on Headset, Quest Link, and Tray Tool pages, with headset **“Screenshot taken.”** feedback after successful saves.

---

## v1.1.25 — audit, screenshots, and release checksums

Released work:

- Shared runtime probe snapshots for Status, Info, PCVR Ready, and tray menu status.
- Nonblocking tray status refresh and background Headset trust-banner refresh.
- Serialized ADB command execution across watcher, status, custom command, headset tweak, and screenshot paths.
- Debounced high-churn Quest Link settings saves with an exit-time flush.
- Advanced **Repair stuck helper** / **Copy diagnostics** actions plus Info process diagnostics.
- Quest headset screenshots through tray, **Ctrl+Shift+Num 9**, and voice (“take screenshot”), with headset **“Screenshot taken.”** feedback when announcements can reach the Quest audio path.
- Release installer SHA-256 sidecar generation, workflow verification, and checksum upload.

---

## Done (by phase)

### v0.1 — tray host

Notification-area host, OVRService control, settings persistence, elevated logon option.

### Phase 1 — Game settings via Oculus Debug Tool CLI

Supersampling, ASW (Off/Auto/45/30/18), Adaptive GPU, mip flags, FOV stencil, Perf HUD, EnumHmd probe, FOV multipliers.

### Phase 2 — Profiles

Create/edit/delete, process watcher apply + restore on exit, CPU priority, Link overrides, ignore list, last-good, library Launch.

### Phase 3 — Quest Link / Air Link

`HKCU\Software\Oculus\RemoteHeadset` (bitrate, encode width, HEVC, sliced encoding, sharpening, distortion, DBR, Mobile ASW), presets, Air vs wired detect, ODT registry doc.

### Phase 4 — Audio switching

Playback/recording enums, Link-audio trigger, restore on drop, separate communications device pickers.

### Library polish

Steam + Meta library scan, cover art, global defaults editor vs personal profiles.

### Phase 5 — Power and USB

VR power plan, USB selective suspend, restart OVRService after sleep.

### Phase 6 — OTT-style settings shell

Sidebar MainShell, Service automation, HotKeys, Voice (core + polish), Dash → SteamVR / PreventDashLaunch / CoreChannel, remove obsolete Oculus Home, optional SteamVR Home.

### Phase 7 — OpenXR runtime switch

Meta vs SteamVR ActiveRuntime, global + per-profile, restore on exit, Steam Link assist.

### Phase 8 — elevated start

Hands-free elevated logon task, Restart as Administrator, no mid-session UAC. (SteamVR cannot click elevated tray — HotKeys/voice/auto.)

### Phase 9 — headset ADB

Bundled platform-tools, SideQuest-style props, apply on connect, paste/proximity/guardian, Wireless ADB + Pair UI, battery/Wi‑Fi.

### v1.0.0 — release polish

Product naming, custom CLI/ADB lines, settings backup, trusted headset, Info + Donate, Inno Setup self-contained Setup.exe, CI + Release on `v*` tags.

### Post v1.0.0 → v1.1.3

| Tag | Focus |
| --- | --- |
| v1.0.1–1.0.3 | Hotkeys/voice, updater, Open Meta Link, audio steal fix, screenshots |
| v1.0.4–1.0.6 | Session detect, Steam Link assist, Link presets, prefer SteamVR over Wi‑Fi DeviceCache |
| v1.0.7–1.0.8 | Dash → SteamVR, PreventDashLaunch, CoreChannel, ADB unlock for updates, Debug Tool GUI, tooltips |
| v1.0.9–1.0.11 | Idle CPU / snappy sidebar, Wireless ADB, thread-safe probe caches |
| v1.0.12 | Status page, SteamVR install detect, VR Tools, Steam-first PCVR polish, voice polish, Wireless Pair |
| v1.0.13 | Remove Oculus Home leftovers; SteamVR Home open action |
| v1.0.14 | Taller shell (Info visible); fix OpenXR checkbox persist on Game Settings Refresh |
| v1.0.15 | Audio switcher: leave boot audio alone; switch only on PCVR start/end |
| v1.0.16 | Quieter idle: adaptive watcher cadence; Sync* wiring; stop disabled timers; pause Status/Info when shell hidden |
| v1.0.17 | Restart OVRService on SteamVR exit; PreventDashLaunch-only path (removed Meta process killing); Steam Link vs Quest Link docs |
| v1.0.18 | Fix PreventDashLaunch auto SteamVR on DeviceCache auto-connect (headset on Wi‑Fi, Link not streaming) |
| v1.0.19 | Fix Check for updates crash from tray when settings window is closed |
| v1.1.0 | Restore desktop audio on PCVR exit; fix PreventDashLaunch auto SteamVR on tray start (EnumHmd ghost) |
| v1.1.1 | OVRService Manual-at-boot toggle — delay Meta at Windows sign-in; re-apply after Meta updates |
| v1.1.2 | Headset announcements (TTS status in Quest); expanded voice commands; fix false PreventDashLaunch SteamVR auto-start when not streaming |
| v1.1.3 | GitHub Wiki user guide; ignore false Link session-end toast after PC wake |

### Post v1.1.3 → v1.1.11

| Tag | Focus |
| --- | --- |
| v1.1.4 | Session/watcher gates; PreferDash; live audio/power; Steam Link vs Meta |
| v1.1.5 | Zombie SteamVR relaunch; tighter PreventDash connect detection |
| v1.1.6 | SteamVR exit → hard OVR drop for Quest Home; arm exit watch during Link |
| v1.1.7 | Changelog on GitHub releases, Setup info page, in-app update notes |
| v1.1.8 | SideQuest-on-headset ADB docs; Status idle while Quest off/charging |
| v1.1.9 | PreventDash idle latch; UI/ADB hangs; updater ADB unlock; library profile arm; settings load durability; exact DeviceCache connected |
| v1.1.10 | Start SteamVR from tray / Status / Ctrl+Shift+Num 0 / voice; headset speaks “Starting SteamVR.” |
| v1.1.11 | Session helper (unelevated SteamVR unless Steam is already admin); OVRService 10s hold on SteamVR exit; headset TTS voice picker |
| v1.1.12 | Fix 1.1.11 crash on start (session helper STARTUPINFO); helper no longer blocks the UI or opens a second copy |
| v1.1.13 | Headset wait cues: please-wait on Link connect; SteamVR closed + 10s OVR stop spoken before the service drops |
| v1.1.14 | Tray Pause ADB (until resume / 2h) + VR-headsets-only toggle; pause expire/tooltip/sweep races fixed |
| v1.1.15 | Headset TTS fixed when VR playback unset; stop audio auto-switch flap that silenced announcements |
| v1.1.16 | Durable settings/profile save + .bak/.bak2 restore after power-loss truncated JSON; saves work again after corrupt load |
| v1.1.17 | Neon VR icon/logo (tray + About + sidebar); exit/unhandled-exception logging for crash diagnosis |
| v1.1.18 | Setup-aware PCVR Ready; informed connect TTS (SteamVR vs Meta Horizon) with Link audio delay |
| v1.1.19 | Expanded headset voice coverage; safer TTS lifecycle and voice fallback; opt-in MSFS 2024 VR launch automation with target validation and safer profile/helper launches |
| v1.1.20 | Single-instance startup protection with visible process diagnostics; stale session-helper recovery |
| v1.1.21 | Clean-runner locked restore correction; workflow-run retention cleanup |
| v1.1.22 | Deterministic session-helper shutdown on tray exit |
| v1.1.23 | Full audit hardening: ADB/update/profile/URL/service/startup/power safety, process-handle cleanup, docs/build-flow alignment |
| v1.1.24 | Exit/helper hotfix: bounded helper IPC replies, helper owner tracking, parent-death shutdown, and stuck-helper cleanup |
| v1.1.25 | Audit/performance hardening: shared/nonblocking probes, serialized ADB, debounced Link-settings saves, stuck-helper repair diagnostics, Quest screenshots, and release checksum sidecars |
| v1.1.26 | Quest Link mirror screenshots, smart screenshot fallback, screenshot tray/page controls, and HotKeys / Voice menu polish |
| v1.1.27 | Explicit headset announcement toggles for HotKey results, voice command results, and screenshot confirmations |
| v1.1.28 | Quest Link presets stay editable under non-Meta streamers; custom Link settings keep their custom state; bindable Exit app action |

---

## Later

- Authenticode code signing for the installer / exe (SmartScreen; when budget allows)
- Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- More built-in presets as the community requests process names
- Hotkey profiles per game (global only today)

---

## Non-goals for now

- Reviving Meta Oculus Home / Homeless (removed by Meta; use Dash → SteamVR + optional SteamVR Home)
- Permanent AirLink (Meta-side)
- Replicating every OTT advanced/obscure tweak
- Shipping a full dash customizer / permanently replacing `OculusDash.exe` (that belongs in Dash Manager / OculusKiller-style installs)
- macOS / Linux
