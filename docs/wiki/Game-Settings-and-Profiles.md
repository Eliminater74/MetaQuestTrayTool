# Game settings and profiles

Global defaults apply when no personal profile is active: tool start (optional), headset connect (optional), and after a game exits.

![Game Settings](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/01-game-settings.png)

## OpenXR

On **Game Settings**:

- Preferred runtime: **Meta / Oculus** or **SteamVR**
- **Switch now** writes `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` (needs Administrator)
- **Apply preferred OpenXR when the tool starts**

Per-profile OpenXR can override the global choice. **Steam Link assist** can force SteamVR OpenXR while Steam Link / SteamVR is the session, then restore your preferred runtime. See [[FAQ]].

## Debug Tool settings (ODT CLI)

Applied through `OculusDebugToolCLI.exe` (and optionally the GUI from tray / Service & Startup).

| Setting | Typical use |
| --- | --- |
| Super Sampling | Pixel density (often needs a **new VR session**) |
| ASW | Off / Auto / 45 / 30 / 18 FPS clock |
| Adaptive GPU | On/Off |
| FoV multiplier H/V | Narrower FOV for GPU headroom |
| Visual HUD | Performance / compositor / ASW overlays |
| Mip flags | Force / offset mip on layers |
| OVR server priority | CPU priority for OVRServer |

**Cycle Perf HUD** from the tray or HotKeys. Newer Meta runtimes sometimes **reject** `server:` ASW CLI — check **Log**.

While **Steam Link / Virtual Desktop** is the active streamer, Meta ODT + Link registry writes are **skipped** (those streamers own bitrate/SS). ADB, OpenXR, power, and audio still apply.

## Profiles

1. **Profiles → New** or import from Steam / Meta library (cover art).
2. Set the **process name** (e.g. `BeatSaber.exe`) and overrides (SS, ASW, Link, OpenXR, custom CLI/ADB).
3. Optional **Launch** via `steam://run/{appId}`.
4. When that process starts, the profile auto-applies (tray notification + optional headset announcement).
5. When it exits, **global defaults restore**.

**Ignore list** (Tray / Game Settings) stops Discord, browsers, `vrserver`, etc. from stealing the profile.

**Save last-good** (tray) writes the current SS/ASW/HUD into the *active* personal profile mid-session.

Built-in presets exist for several PCVR titles (MSFS 2024, Beat Saber, HL:Alyx, DCS, iRacing, …).

Profiles file: `%AppData%\MetaQuestTrayTool\profiles.json`. Export/import everything from **Advanced**.

## Close overlays on Link connect

Optional list of process names (no `.exe`) such as RTSS, Afterburner, NZXT CAM. Closed when a PCVR session connects. Voice **close overlays** can force the same list even if auto-close is off.

Never kills SteamVR, Oculus, Discord, or Explorer.

## Custom CLI / ADB lines

![Custom commands](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/02-game-settings-custom-commands.png)

Extra OculusDebugToolCLI lines and `adb shell` lines on global defaults and on each profile.

## Related

- [[Quest-Link]]
- [[HotKeys-Voice-and-Announcements]]
