# Changelog

All notable changes to **Meta Quest Tray Tool** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/). Versions use [SemVer](https://semver.org/).

The in-app updater and GitHub Releases show the notes for each version so you can decide whether to install.

## [Unreleased]

### Fixed
- Startup Link auto-apply now skips the automatic Link registry write if the preflight read fails, avoiding a possible downgrade of unknown high ODT fixed bitrate or DBRMax values.

### Docs
- Renamed the old issue #4 reproduction TODO to post-fix physical regression validation now that the tracked issue has shipped fixes.

## [1.1.34] - 2026-09-13

### Fixed
- Startup high-bitrate preservation now covers Dynamic Bitrate Max (`DBRMax`) as well as fixed Link bitrate, so a saved baseline from the old 500 Mbps preset range cannot silently downgrade an existing high DBR ceiling.

### Validation
- Local suite passes 168/168.

### Verification limits
- Physical Quest confirmation of the active Link stream bitrate and DBR ceiling still needs hardware validation; this release verifies startup merge behavior and saved-setting precedence with local regression coverage.

## [1.1.33] - 2026-09-12

### Fixed
- Performance samples start logcat at the headset's current timestamp without clearing its log buffer. VrRuntime and OVRPlugin metric lines no longer require FPS or VrApi text to be parsed.
- Stop & download waits for a new or changed recording to have matching size and timestamp across two reads, polling up to ten times at one-second intervals. If nothing stabilizes, it reports a retry instead of downloading an unchanged previous recording.
- Automatic Meta compatibility validation requires a successful Debug Tool read probe, readable component versions, and no registry/probe warnings. First detection remains pending, and explicit acknowledgement remains available.
- Quest Link bitrate controls now expose high presets through 960 Mbps, and startup preserves an existing fixed ODT bitrate above the old 500 Mbps preset ceiling instead of downgrading it to a stale saved cap.

### Validation
- Local suite passes 166/166.

### Verification limits
- Physical Quest recording finalization, current-firmware performance output, active Link stream bitrate adoption, and OVRService restart effects still need hardware validation.

## [1.1.32] - 2026-09-11

### Fixed
- **Read live registry** on the Quest Link page now stays read-only while it loads controls, so queued selection changes cannot save/apply settings or replace the read result with a non-Meta streamer skip message.
- Live operable Meta Link cache evidence now beats resident Virtual Desktop desktop processes, while stale/inoperable Meta cache records still leave Virtual Desktop / Steam Link guards in place.

## [1.1.31] - 2026-09-10

### Added
- Structured profile-apply results now report success, partial success, failed, skipped, and no-change outcomes instead of collapsing multi-system applies into one generic message.
- Headset ADB controls now include explicit **Start headset recording** / **Stop headset recording**, **Stop & download latest recording**, **Capture 10-second performance sample**, 30 Mbps and 40 Mbps capture bitrate presets, independent CPU and GPU levels 0-4, headset-aware refresh-rate choices, clearer Quest 2/Pro and Quest 3/3S texture labels, Dynamic vs Fixed foveation, and a collapsed **Experimental headset rendering** section for Quest Pro local dimming plus subsampled foveation.
- Added **Reset live ADB overrides** for documented texture, refresh, and capture defaults, with reboot guidance for CPU/GPU, foveation, chroma, local dimming, subsampled foveation, custom props, and other temporary values that do not have a proven safe clear.
- Added read-only Meta runtime compatibility checks that separate detected Meta runtime / Oculus Debug Tool versions from the validated baseline, keeping runtime-change warnings pending until the user runs the full check or acknowledges externally validated versions.
- Added **Export support ZIP** on the Info page with sanitized summary, settings counts, compatibility status, and recent logs. Successful export logs now avoid the full chosen output path.
- Added Dependabot, CodeQL, Cobertura coverage collection, and a conservative line-coverage floor for CI and release validation.

### Changed
- OpenXR switching now reads and writes the 64-bit and 32-bit HKLM registry views explicitly, clearing stale 32-bit ActiveRuntime values when the selected runtime has no matching 32-bit JSON and showing mixed-view diagnostics in reports.
- Headset UI labels now call out High Top FFR as legacy/VrApi behavior where OpenXR behaves as High, and Capture FPS as legacy/firmware-dependent until physical Quest validation confirms current support.
- Headset ADB command selection now prefers the trusted physical headset across USB and wireless transports, matching hardware identity before falling back to any ready recognized headset.
- ADB process launches now use `ProcessStartInfo.ArgumentList` for outer `adb.exe` tokens and async process waiting with timeout/cancellation cleanup.
- CI, Release, and installer build restores now use locked `win-x64` restore for normal validation instead of refreshing package lock evaluation.
- The Release workflow now installs a pinned Inno Setup 6.7.1 upstream installer and verifies its SHA-256 digest before running it.

### Security
- The in-app updater now stores downloads in a private random temp directory, revalidates installer size and SHA-256 immediately before launch, and holds the verified installer handle open while starting Setup so local replacement/tampering is blocked.
- Support ZIP export applies a second redaction pass for user-profile paths, IPv4/IPv6 endpoints, MAC/BSSID values, email-address-looking strings, labeled serial/fingerprint/SSID values, and long device-like tokens.

### Validation
- Local suite passes 145/145 after locked restore/build changes and the second-pass regressions.

### Verification limits
- Physical Quest headset behavior, active Link stream adoption, and OVRService restart effects remain unverified for this release; local source validation and hosted installer checksum verification are tracked separately from active-headset proof.

## [1.1.30] - 2026-09-10

### Fixed
- HotKeys configuration now resizes and scrolls, with a bounded binding list, so Add binding and the Action/Record editor remain reachable. Added guidance for assigning Exit without introducing a default quit shortcut.
- Meta device-cache selection prioritizes live Link evidence over newer idle or weak headset entries. This prevents those entries from incorrectly routing SteamVR-over-Link through the non-Meta registry-write guard.

### Changed
- Quest Link skips identify the detected streamer and log the session state and requested settings.
- Updated hotkey instructions and documented the reproduced detection defect and its verification limits.

### Validation
- Ten new regression cases since v1.1.29; full suite passes 106/106. Release build has zero warnings/errors.
- Original-layout checks failed both new layout cases; the original cache-selection algorithm failed three detection cases. All pass with the repairs.

### Verification limits
- The multi-headset detection defect is reproduced with synthetic cache records. Whether it explains the tester's ODT failure remains unconfirmed; physical headset verification is still open. Registry mappings and non-Meta session guards remain unchanged.

## [1.1.29] - 2026-09-08

### Fixed
- Quest Link sharpening now matches observed ODT DWORD values (Disabled=1, Normal=2, Quality=3); encode-width reads prefer ODT's current value over a stale legacy alias.
- Link applies verify every attempted registry write/deletion and report mismatches or access failures. Read live registry refreshes the controls without applying stale saved settings.
- Custom bitrate, width, supersampling and signed dynamic-bitrate offset values survive editor loading and unrelated edits.
- Separate horizontal/vertical FOV values survive JSON reload, including legacy migration. Profile/global editors expose both axes, reject non-finite input, and applying 1/1 explicitly restores the default multiplier.
- Performance HUD selections use explicit Meta CLI mappings instead of saved enum ordinals; App Render Timing, Compositor Timing and performance-headroom summary now select the intended modes.
- Wireless ADB connect/pair and Send text capture WPF input before background work, avoiding cross-thread control access failures.

### Changed
- Clarified **Start with Windows as Administrator (hands-free)** while preserving the separate current-session elevation command and assignable Exit action with no default shortcut.
- Marked unverified legacy capture/Mobile ASW behavior clearly; legacy Version HUD remains available with a display-verification qualification.

### Added
- Read-only Link registry snapshot script and detailed control audit covering 177 inputs / 260 declarative event bindings.
- 42 regression cases since v1.1.28; full suite passes 96/96.

### Verification limits
- Rebuilt service and installed ODT synchronized bitrate in both directions (500/450 Mbps). The tester-specific bitrate failure was not reproduced; OVRService restart and active-headset effects remain unverified. Registry read-back does not claim runtime application.

## [1.1.28] - 2026-09-04

### Added
- Added a bindable **Exit Meta Quest Tray Tool** action to HotKeys and custom voice-command phrases so users can quit the tray from VR without opening the tray menu.

### Fixed
- Quest Link presets and saved Link fields stay editable during Steam Link / SteamVR or Virtual Desktop sessions. Live Meta Link registry and ODT writes are still skipped under those streamers, but settings can now be saved for the next Meta Link / Air Link session.
- Custom Quest Link settings no longer visually fall back to the first preset when reopening the Quest Link page.

## [1.1.27] - 2026-09-03

### Fixed
- Headset announcements now show and honor explicit default-on categories for **HotKey action results**, **Voice command results**, and **Screenshot taken confirmations**. Screenshot success/failure speech no longer depends on the broader headset/ADB bucket, and HotKey/voice command result speech no longer depends on the generic important-action bucket.

## [1.1.26] - 2026-09-03

### Added
- Quest Link / Air Link mirror screenshots can now be captured through Meta's `OculusMirror.exe` from the tray **Screenshots** menu, the **Quest Link** page, voice phrases like “take link screenshot”, or a bindable hotkey action. This path requires an active Meta Link stream but does **not** require ADB.
- A new smart screenshot action prefers Quest Link mirror capture when Meta Link is actively streaming, then falls back to the trusted-headset ADB screenshot path. It is exposed from the tray **Screenshots** menu, the Headset / Quest Link / Tray Tool pages, voice phrases like “take screenshot”, and default hotkey **Ctrl+Shift+Num 8**.

### Changed
- Screenshot labels now distinguish **Quest Link mirror** from **Headset ADB** capture. ADB screenshots remain available from the Headset page/menu, voice phrases like “take headset screenshot”, and default hotkey **Ctrl+Shift+Num 9**.
- The tray menu now has dedicated **Screenshots** and **HotKeys / Voice** submenus, and the Tray Tool page surfaces voice/hotkey controls near headset announcements with screenshot phrases and a **Listen once** action.

## [1.1.25] - 2026-09-03

### Added
- Quest headset screenshots can be captured from tray → **Headset (ADB) → Take screenshot**, default hotkey **Ctrl+Shift+Num 9**, or voice phrases like “take screenshot” / “capture screenshot.” Captures use the trusted-headset ADB guard, save timestamped PNGs under `%AppData%\MetaQuestTrayTool\screenshots\`, show a tray result, and say **“Screenshot taken.”** in the headset when the announcement audio path is available.
- Advanced now includes **Repair stuck helper** and **Copy diagnostics** actions for hidden helper/process situations, and the Info dump includes main/helper PID details and settings/profile paths.
- Installer builds now emit a `MetaQuestTrayTool-Setup-<version>.exe.sha256.txt` sidecar, and the Release workflow verifies and uploads that checksum alongside the Setup.exe.

### Changed
- Status, Info, PCVR Ready, and tray menu refreshes now share a short-lived runtime snapshot instead of repeating the same SteamVR/OpenXR/OVRService/GPU probes.
- Tray menu status refresh and Headset trust-banner refresh now run expensive probes in the background so opening the menu or page stays responsive.
- ADB commands are serialized through one queue, including screenshot capture, so watcher/status/custom/headset actions cannot collide on `adb.exe`.
- High-churn Quest Link registry edits debounce settings writes and flush pending changes on exit.

### Fixed
- A stuck but verified same-exe session helper can now be repaired from the Advanced page without using Task Manager.
- Screenshot capture validates the PNG signature before reporting success and deletes corrupt output if ADB returned text/errors instead of image bytes.

## [1.1.24] - 2026-09-02

### Fixed
- Session-helper mode now skips the tray owner's shutdown cleanup, so quitting the helper cannot run normal tray exit work or save default settings over the user's configuration.
- Tray exit and startup cleanup now uses bounded helper pipe replies, a recorded helper owner, and same-exe child fallback; the helper also exits when its tray parent disappears, preventing an unresponsive helper from leaving the next launch stuck hidden.

## [1.1.23] - 2026-09-02

### Fixed
- Update cleanup now only terminates packaged `adb.exe` processes under this app's install folder, preserving external Android SDK / SideQuest / PATH ADB processes even when the bundled copy is unavailable.
- Developer command-line build instructions now use the locked `win-x64` restore + no-restore build/test flow that CI uses.
- Deleting all profiles now persists as an intentional empty profile list instead of restoring old profiles from backup on the next launch.
- Elevated URI fallback launches now use Explorer directly instead of `cmd /c start`, so Donate, VR Tools, and Steam URI fallbacks are not parsed as shell commands.
- Log redaction now covers `Wi-Fi` / `Wi‑Fi` SSID labels and quoted SSID values with spaces.
- VR Tools web links now require a valid absolute `http://` or `https://` URL instead of accepting any string that starts with `http`.
- Timer-driven SteamVR/session probes now dispose `Process` handles after `GetProcessesByName` checks, avoiding slow handle growth during long tray runs.
- `powercfg.exe` and `sc.exe` runners now read output asynchronously and honor timeouts instead of blocking indefinitely before the timeout check.
- Startup scheduled-task queries and updates now have bounded `schtasks.exe` waits and output reads.
- Failed library launches now keep the original launch error visible even if the armed-profile cleanup restore also fails.
- Wireless ADB host parsing now revalidates embedded ports and rejects host text that would split into extra ADB arguments.
- Restarting `OVRService` now preserves failed stop errors instead of reporting a misleading “already running” start result.
- Hotkey startup now only reports voice push-to-talk conflicts when voice push-to-talk is actually enabled and mapped.

## [1.1.22] - 2026-08-29

### Fixed
- Exiting the tray now requests the session helper to shut down, waits for it to exit, and safely terminates it if it remains stuck so a later launch is not blocked.

## [1.1.21] - 2026-08-29

### Added
- A manually runnable and weekly scheduled workflow to retain the five newest runs per workflow while skipping active runs.

### Fixed
- CI and release builds now restore the solution with the `win-x64` runtime before locked-mode build and test steps.

## [1.1.20] - 2026-08-29

### Fixed
- A stale session-helper process no longer blocks the tray after an unclean exit. A genuine second tray launch now displays the owning process ID and clear recovery instructions.
- Custom ADB commands now honor the trusted-headset requirement, and profile editing is transactional when cancelled.
- ODT / ADB failures now report timeouts and non-zero exit codes instead of false success.
- Audio and power watchers restore captured desktop state when disabled or stopped; PreventDashLaunch preserves the optional SteamVR-exit preference.
- Updates verify the exact GitHub installer asset, size, and published SHA-256 digest before launch.
- Installer cleanup no longer terminates unrelated ADB clients and removes this app's startup registrations on uninstall.

## [1.1.19] - 2026-08-28

### Added
- Expanded headset announcements with game/profile launch details, profile apply/restore outcomes, action/audio/headset/recovery results, and an opt-in experimental-result toggle.
- Experimental MSFS 2024 VR launch now prepares the selected PCVR path, supports configurable launch arguments and VR hotkeys, and verifies the target process window before sending input.

### Fixed
- Stale MSFS VR toggles are cancelled when another launch starts, and PCVR preparation failures no longer continue into game launch automation.
- Profile launches are blocked from replacing an already-active game profile, preventing a failed second launch from restoring the wrong global settings.
- Local profile executables must be relative `.exe` files inside the configured install directory; session-helper launch fields are encoded safely across the named pipe.
- Headset TTS reload and shutdown now retire synthesizers only after active playback releases them, and voice confirmations fall back when headset delivery is unavailable.
- OpenXR write failures are reported as failures in spoken action summaries.

## [1.1.18] - 2026-08-25

### Added
- Headset connect announcements wait for Link audio (~2.2s) and explain what happens next: **“Connected. Air Link. Now starting SteamVR.”** on PreventDash path, or **“Meta Horizon will load.”** when Dash is allowed.
- Duplicate “Please wait. Starting SteamVR.” is skipped when the connect line already covered auto-start.

### Fixed
- PCVR Ready / Status judge OpenXR against your **setup path** (PreventDash → SteamVR vs Meta Link / Dash), not a stale saved Meta preference — plus a **PCVR setup** chip on Status.

## [1.1.17] - 2026-08-25

### Added
- New neon VR headset icon and logo — larger, higher-contrast tray icon (16–256px) plus refreshed About dialog and settings sidebar branding. Master artwork lives in `assets/`; run `scripts/build-icons.ps1` to regenerate `App.ico`.

### Fixed
- Better crash diagnosis: log intentional exits (update, elevation handoff), exit codes, and fatal/unhandled background exceptions in `app.log`.

## [1.1.16] - 2026-08-25

### Fixed
- After a sudden power loss or forced restart, a half-written `settings.json` / `profiles.json` no longer silently resets everything — and re-checking options now actually saves again.
- Settings/profile writes flush to disk (write-through), keep `.bak` + `.bak2`, refuse to promote a truncated file over a healthy backup, auto-restore from backup when the primary looks corrupt/empty, and show a warning if a restore happened.

## [1.1.15] - 2026-08-25

### Fixed
- Headset TTS (“Speak status in headset”) works again when Audio has no VR playback device saved: announcements target Meta/Oculus Virtual Audio while Link/SteamVR is live, retry briefly after connect, and no longer log a false success when speech was skipped.
- Audio auto-switch no longer flaps desktop ↔ headset every ~10s when VR devices were unset (that loop also silenced announcements). Disconnect phrases speak before speakers are restored.

## [1.1.14] - 2026-08-23

### Added
- Tray **Headset (ADB)** menu: **VR headsets only** toggle (on = drop phone/TV wireless ADB; off = allow any device), plus **Pause ADB until I resume** / **Pause ADB for 2 hours** / **Resume** so other ADB gadgets work without quitting the tray.

### Fixed
- Timed ADB pause now restarts the watcher when it expires (reading pause status no longer clears the flag without SyncWatch).
- Tray menu sync no longer fires spurious save/balloon events when opening Headset checks.
- Tray tooltip refreshes when ADB pause ends; in-flight polls and manual wireless connect no longer sweep phones/TVs while ADB is paused.

## [1.1.13] - 2026-08-15

### Added
- Headset announcements cover the waits: **“Please wait. Starting SteamVR.”** when Link connects, and **“SteamVR closed. Stopping Meta service for 10 seconds.”** spoken in the Quest **before** OVRService stops (the old exit line ran after audio had already left the headset).

## [1.1.12] - 2026-08-15

### Fixed
- v1.1.11 could crash or hang as soon as it started (session helper `CreateProcessWithTokenW` used a bad STARTUPINFO layout). The settings window and tray menu open again. Starting the helper no longer blocks the UI or launches a second copy via Explorer.

## [1.1.11] - 2026-08-15

### Fixed
- Air Link no longer shows as Steam Link when leftover SteamVR processes are on the PC. A live DeviceCache Link stream wins, and PreventDash relaunches SteamVR so it attaches to this connect.
- Exiting SteamVR fully **stops** OVRService, holds it down **at least 10 seconds** so Link drops to Quest Home, then starts it again (a quick restart kept Link up and bounced SteamVR).
- SteamVR, Steam games, Horizon Link, Debug Tool, and other user launches go through a same-exe **session helper** (normal user). If Steam is already running as Administrator, SteamVR / steam:// skip the helper so they match that Steam instance.

### Added
- Headset announcements have a **spoken voice** picker. Auto prefers an English female Windows TTS voice; pick any installed voice. Voice-command confirmations use the same choice.

## [1.1.10] - 2026-08-15

### Added
- Start SteamVR from the tray, Status, Service, Quest Link, **Ctrl+Shift+Num 0**, and voice (“start steam v r” / “launch steam v r”) without PreventDashLaunch. The headset speaks “Starting SteamVR.”

## [1.1.9] - 2026-08-15

### Fixed
- PreventDashLaunch notices Air Link / Quest Link within seconds and starts SteamVR (the delayed launch no longer cancels a real connect). When SteamVR exits, OVRService is held down so Link drops and Quest Home can return.
- Auto SteamVR no longer bounces back during that OVR drop: keep the idle latch while the service is down, require several idle polls, and cancel the restart if the tray exits.
- EnumHmd, ADB, and OVRService waits no longer freeze the UI. Hung `adb.exe` is killed after a timeout.
- The in-app updater stops the headset ADB watcher, waits until `adb.exe` is gone, then starts Setup.
- Library Launch keeps the game profile armed until that process exits (Steam spawn delay included).
- A corrupt `settings.json` is left on disk (try `.bak`) instead of being overwritten with defaults; saves are serialized under a lock.
- DeviceCache must report exact `connected` (not a `connect` substring). A fresh `RemoteDesktopCompanion` process no longer auto-starts SteamVR while the Quest is charging.

### Added
- Headset-only wireless ADB (on by default): drops phones/tablets that appear over the network; uncheck on the Headset page if you want other wireless ADB devices to stay connected. Quest tweaks still never run on non-headsets.

### Docs
- Quest Link vs Steam Link: full tweaks need Link / Air Link; Steam Link app is ADB-only for headset props; SteamVR over Link uses PreventDashLaunch (wiki + README + in-app hints).

## [1.1.8] - 2026-08-14

### Docs
- Note that SideQuest running on the headset can open a wireless ADB port for Connect / auto-reconnect.

### Fixed
- Status no longer shows Air Link / PCVR session Active while the Quest is off or charging (ignore leftover RemoteDesktopCompanion; ignore sticky DeviceCache `alternate` / `isUsingAirLink` ghosts).

## [1.1.7] - 2026-08-14

### Added
- Changelog shown on GitHub Releases, in Setup before install, and in the in-app update prompt so you can review changes before updating.

## [1.1.6] - 2026-08-14

### Fixed
- Exiting SteamVR under PreventDashLaunch no longer leaves a black void: exit watch arms for manual SteamVR starts during Link, clears zombie `vrserver`, and holds OVRService stopped briefly so Air Link drops and Quest Home can return.

## [1.1.5] - 2026-08-14

### Fixed
- Zombie / invisible SteamVR (`vrserver` without compositor) is restarted instead of reporting “already running.”
- PreventDashLaunch auto-starts SteamVR on real Air Link connects (connection state, EnumHmd, `RemoteDesktopCompanion`) instead of waiting forever for headset audio.

## [1.1.4] - 2026-08-14

### Fixed
- Link audio alone no longer misclassifies Steam Link / Virtual Desktop as Meta Link.
- SteamVR-exit → OVR restart only arms from Dash→SteamVR (not unrelated SteamVR).
- Audio and power auto-switch gate on live PCVR streams.
- PreferPreventDashLaunch only latches after a successful registry write; settings import/reset reloads watchers; watchers dispose before OVR stop on exit.

### Docs
- Meta Quest setup URL corrected; headset announcements documented in voice/hotkeys guide.

## [1.1.3] - 2026-08-14

### Fixed
- False “Link session ended” toast after PC wake (resume quiet window + confirm polls).

### Docs
- User-guide wiki source and walkthrough video link.

## [1.1.2] - 2026-08-14

### Added
- Headset status announcements (optional TTS in the Quest).
- Expanded voice commands (recover PCVR, audio, OpenXR, overlays, GPU preset).

### Fixed
- Tighter Meta Link streaming detection before PreventDashLaunch auto-starts SteamVR.

## [1.1.1] - 2026-08-13

### Fixed
- OVRService Manual-at-boot handling improvements.

## [1.1.0] - 2026-08-13

### Added
- PreventDashLaunch → SteamVR over Link workflow (registry only; no Meta process killing).
- Optional CoreChannel (`LIVE` / `PublicTest` / `NO_UPDATES`).
- Restart OVRService when SteamVR exits (return toward Quest Home without Dash).

[Unreleased]: https://github.com/Eliminater74/MetaQuestTrayTool/compare/v1.1.34...HEAD
[1.1.34]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.34
[1.1.33]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.33
[1.1.32]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.32
[1.1.31]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.31
[1.1.30]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.30
[1.1.29]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.29
[1.1.28]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.28
[1.1.27]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.27
[1.1.26]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.26
[1.1.25]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.25
[1.1.24]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.24
[1.1.23]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.23
[1.1.22]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.22
[1.1.21]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.21
[1.1.20]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.20
[1.1.19]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.19
[1.1.18]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.18
[1.1.17]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.17
[1.1.16]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.16
[1.1.15]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.15
[1.1.14]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.14
[1.1.13]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.13
[1.1.12]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.12
[1.1.11]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.11
[1.1.10]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.10
[1.1.9]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.9
[1.1.8]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.8
[1.1.7]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.7
[1.1.6]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.6
[1.1.5]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.5
[1.1.4]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.4
[1.1.3]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.3
[1.1.2]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.2
[1.1.1]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.1
[1.1.0]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.0
