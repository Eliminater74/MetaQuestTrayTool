# Roadmap

Living plan for Meta Quest Tray Tool. Update this when a phase lands or the order changes.

Inspired by [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/) (ApollyonVR), but this is a new C# app — not a decompiled port.

**Current public release:** [v1.1.35](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## Current source checkout (public v1.1.35)

Steam-first PCVR tray for Meta Quest Link + SteamVR OpenXR:

| Area | Shipped |
| --- | --- |
| Shell | Status (default), Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link, Headset, VR Tools, Info |
| Status / Ready | Live chips, SteamVR install/version/Stable\|Beta, PCVR Ready checklist, Recover PCVR, session probe (Air/wired/Steam/VD) |
| Game / profiles | ODT SS/ASW/FOV/HUD, Debug Tool GUI, auto profiles, library Launch, ignore list, last-good, overlays close |
| Link / Dash | RemoteHeadset Link settings + high bitrate presets through 960 Mbps, startup protection for externally set high fixed ODT bitrate and DBRMax values, presets that remain editable while non-Meta streamers are active, Quest Link mirror screenshots, PreventDashLaunch → SteamVR over Link (registry only; no Meta process killing), CoreChannel, SteamVR Home (on demand), OVRService restart on SteamVR exit, OVRService Manual-at-boot toggle |
| OpenXR / audio / power | Meta vs SteamVR switch, Steam Link assist, comms audio pickers, power plan / USB / wake restart |
| Headset ADB | Wired + Wireless Pair/Connect/tcpip, independent CPU/GPU, model-aware refresh, Dynamic/Fixed FFR, capture + recording start/stop/download, 10-second logcat performance sampler, guarded experimental rendering, reset live documented defaults, ADB Quest screenshots, battery/Wi‑Fi, trusted serial, **VR headsets only** toggle, **Pause ADB** (until resume / 2h) |
| Mid-session | HotKeys (Ctrl+Num 0–9, Ctrl+Shift+Num 0/8/9 plus bindable Exit), voice (PTT/mic/confidence/custom phrases + recover/audio/OpenXR/overlays/GPU/smart + Link + ADB screenshots + bindable Exit), expanded headset announcements with separate HotKey/voice/screenshot result toggles, experimental MSFS 2024 VR launch automation |
| Updates / polish | In-app GitHub updates, private revalidated installer launch, release checksum sidecars, CodeQL, Dependabot, coverage gate, expanded sanitized support ZIP including session-trace.log, persistent read-only Meta compatibility check, 64-bit/32-bit OpenXR diagnostics, shared/nonblocking status probes, helper repair diagnostics, [Wiki](https://github.com/Eliminater74/MetaQuestTrayTool/wiki), themes, tooltips, quiet idle cadence (stop disabled watchers; pause Status/Info when shell hidden), VR Tools links, Donate, durable settings/profiles (`.bak`/`.bak2` after power loss), neon icon/logo |

Checkbox history: [TODO.md](TODO.md). User-facing detail: [README.md](README.md).

---

## v1.1.35 — freeze diagnostics and safer startup Link preflight

Adds a bounded session flight recorder so Link fingerprint, ADB reconnect, and mutating Link/ODT/OVRService/profile/audio/power edges can be timeline-diagnosed for issue #12 without rewriting Link state. Startup Link auto-apply now skips the registry write if the high-bitrate preflight read fails.

- Local validation: 183 passing tests.
- Physical Quest 3 freeze reproduction is still required; this release does not claim the freeze is fixed.

## v1.1.34 — DBRMax high-bitrate preservation

Startup high-bitrate preservation now covers Dynamic Bitrate Max (`DBRMax`) as well as fixed Link bitrate. When ODT already has either value above the old 500 Mbps preset ceiling and the saved tray baseline is still old-capped, startup auto-apply keeps the higher ODT value instead of downgrading it.

- Local validation: 168 passing tests.
- Physical active-stream bitrate and DBR ceiling behavior still need hardware confirmation; local tests verify startup merge behavior and saved-setting precedence.

---

## v1.1.33 — high Link bitrate and headset diagnostics

Adds Quest Link bitrate presets through 960 Mbps and preserves existing fixed ODT bitrate values above the old 500 Mbps preset ceiling during startup auto-apply. Performance sampling starts at the headset's current log time without clearing history, Stop & download waits for a new or changed recording to stabilize, and Meta compatibility auto-baselines now require a successful Debug Tool read probe, readable versions, and no prerequisite warnings.

- Local validation: 166 passing tests.
- Physical Quest recording finalization, current-firmware performance output, active Link stream bitrate adoption, and OVRService restart effects still need hardware confirmation.

---

## v1.1.32 — Read Live and active Meta Link detection

**Read live registry** now stays read-only while controls load, so queued UI selection changes cannot save/apply Link settings or replace the read result with a non-Meta streamer skip message. Live operable Meta Link cache evidence now beats resident Virtual Desktop desktop processes, while stale/inoperable Meta cache records still keep Virtual Desktop / Steam Link guards in place.

- Local validation: 146 passing tests.
- Physical Quest Link / Virtual Desktop background-process behavior still needs hardware confirmation.

---

## v1.1.31 — headset controls, release hardening, and diagnostics

Adds profile partial-success/no-change reporting, trusted USB/wireless headset selection, headset recording start/stop/download, a 10-second logcat performance sampler, 30/40 Mbps capture bitrates, independent CPU/GPU levels, model-aware refresh choices, Dynamic FFR, guarded experimental headset rendering, reset of documented live ADB defaults, private revalidated updater installs, persistent Meta runtime compatibility checks, 64-bit/32-bit OpenXR registry handling, expanded support ZIP sanitization, CodeQL, Dependabot, coverage reporting, and locked release restores.

- Local source validation: 145 passing tests before release publication.
- Physical Quest headset behavior, active Link stream adoption, and OVRService restart effects remain open for hardware validation.

---

## v1.1.30 — reachable hotkey editing and Meta Link detection

Fixes clipped HotKeys editing controls and prevents newer idle or weak cached headsets from hiding live Meta Link evidence. Adds explicit logging for skipped Quest Link writes. The tester-specific ODT report remains unconfirmed; existing non-Meta guards are preserved.

- Ten new regression cases; 106 total passing tests.
- Physical confirmation of the tester-specific failure remains open.

---

## v1.1.29 — control correctness and interoperability

Corrects Quest Link and HUD mappings, verifies Link registry writes, preserves custom values and independent FOV axes, and repairs headset button input handling.

- Verified Link persistence and ODT interoperability, with exact mismatch reporting.
- Preserved existing non-Meta session guards and saved settings compatibility.
- Added a complete control inventory and 42 regression cases (96 total passing tests).
- Physical headset/restart verification and the tester-specific bitrate report remain open.

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
| v1.1.29 | Link/HUD mapping, registry verification, custom values, independent FOV persistence/reset, headset input-thread fixes |
| v1.1.30 | Scrollable hotkey editor, active-headset cache selection, skipped-write diagnostics |
| v1.1.31 | Headset control expansion, updater/release hardening, Meta runtime/OpenXR diagnostics, support ZIP redaction |
| v1.1.32 | Read Live remains read-only; live Meta Link cache beats resident Virtual Desktop desktop processes |
| v1.1.33 | 960 Mbps Link bitrate presets, high-ODT startup preservation, recording/performance diagnostics, stricter Meta validation baselines |
| v1.1.34 | DBRMax high-bitrate startup preservation |
| v1.1.35 | Session freeze diagnostics and safer startup Link preflight |

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
