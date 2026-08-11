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
- [x] Activity log on the dashboard and on disk
- [x] Project README, roadmap, and this TODO

## Phase 1 — game settings (Debug Tool CLI)

- [x] `OculusDebugToolService` that writes a command file and runs `OculusDebugToolCLI.exe -f`
- [ ] Super Sampling options on the tray menu
- [ ] ASW mode options on the tray menu
- [ ] Apply current defaults from the dashboard
- [ ] Persist default SS / ASW / FOV
- [ ] Show Debug Tool path and last apply result on the dashboard
- [ ] Warn in the log when Meta rejects `server:` commands

## Phase 2 — profiles

- [ ] Profile model + JSON store
- [ ] Profile editor window
- [ ] Tray submenu of saved profiles
- [ ] Process watcher that applies a profile on game launch
- [ ] Restore defaults when the watched process exits
- [ ] CPU priority for the detected process

## Phase 3 — Quest Link / Air Link

- [ ] Link settings model (bitrate, encode resolution, dynamic bitrate)
- [ ] Apply / read Link-related settings
- [ ] Tray + dashboard UI

## Phase 4 — audio

- [ ] Enumerate playback and recording devices
- [ ] Switch defaults when Oculus / Link becomes active
- [ ] Restore previous devices when VR stops

## Phase 5 — power / USB

- [ ] Power plan switch while VR is running
- [ ] USB selective suspend option
- [ ] Restart `OVRService` after sleep

## Housekeeping

- [ ] Keep README "what works now" in sync after each phase
- [ ] Bump version in the csproj when a phase ships
- [ ] Decide on a license before a public release
