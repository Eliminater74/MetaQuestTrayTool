# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

**Current public release:** [v1.0.18](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## Current product (v1.0.18)

Steam-first PCVR tray for Meta Quest Link + SteamVR OpenXR:

| Area | Shipped |
| --- | --- |
| Shell | Status (default), Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link, Headset, VR Tools, Info |
| Status / Ready | Live chips, SteamVR install/version/Stable\|Beta, PCVR Ready checklist, Recover PCVR, session probe (Air/wired/Steam/VD) |
| Game / profiles | ODT SS/ASW/FOV/HUD, Debug Tool GUI, auto profiles, library Launch, ignore list, last-good, overlays close |
| Link / Dash | RemoteHeadset Link settings + presets, PreventDashLaunch → SteamVR over Link (registry only; no Meta process killing), CoreChannel, SteamVR Home (on demand), OVRService restart on SteamVR exit |
| OpenXR / audio / power | Meta vs SteamVR switch, Steam Link assist, comms audio pickers, power plan / USB / wake restart |
| Headset ADB | Wired + Wireless Pair/Connect/tcpip, CPU/GPU/refresh/FFR/capture, battery/Wi‑Fi, trusted serial |
| Mid-session | HotKeys (Ctrl+Num 0–9), voice (PTT/mic/confidence/custom phrases), automation |
| Updates / polish | In-app GitHub updates, themes, tooltips, quiet idle cadence (stop disabled watchers; pause Status/Info when shell hidden), VR Tools links, Donate |

Checkbox history: [TODO.md](TODO.md). User-facing detail: [README.md](README.md).

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

### Post v1.0.0 → v1.0.18

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
