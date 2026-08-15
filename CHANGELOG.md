# Changelog

All notable changes to **Meta Quest Tray Tool** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/). Versions use [SemVer](https://semver.org/).

The in-app updater and GitHub Releases show the notes for each version so you can decide whether to install.

## [Unreleased]

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

[Unreleased]: https://github.com/Eliminater74/MetaQuestTrayTool/compare/v1.1.12...HEAD
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
