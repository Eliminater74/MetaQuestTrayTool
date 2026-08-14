# Changelog

All notable changes to **Meta Quest Tray Tool** are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/). Versions use [SemVer](https://semver.org/).

The in-app updater and GitHub Releases show the notes for each version so you can decide whether to install.

## [Unreleased]

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

[Unreleased]: https://github.com/Eliminater74/MetaQuestTrayTool/compare/v1.1.7...HEAD
[1.1.7]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.7
[1.1.6]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.6
[1.1.5]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.5
[1.1.4]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.4
[1.1.3]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.3
[1.1.2]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.2
[1.1.1]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.1
[1.1.0]: https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.1.0
