# Changelog

All notable changes to **Meta Quest Tray Tool** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/). Versions use [SemVer](https://semver.org/).

The in-app updater and GitHub Releases show the notes for each version so you can decide whether to install.

## [Unreleased]

### Fixed
- Update cleanup now only terminates packaged `adb.exe` processes under this app's install folder, preserving external Android SDK / SideQuest / PATH ADB processes even when the bundled copy is unavailable.
- Developer command-line build instructions now use the locked `win-x64` restore + no-restore build/test flow that CI uses.
- Deleting all profiles now persists as an intentional empty profile list instead of restoring old profiles from backup on the next launch.
- Elevated URI fallback launches now use Explorer directly instead of `cmd /c start`, so Donate, VR Tools, and Steam URI fallbacks are not parsed as shell commands.
- Log redaction now covers `Wi-Fi` / `Wi‑Fi` SSID labels and quoted SSID values with spaces.
- VR Tools web links now require a valid absolute `http://` or `https://` URL instead of accepting any string that starts with `http`.

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

[Unreleased]: https://github.com/Eliminater74/MetaQuestTrayTool/compare/v1.1.22...HEAD
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
