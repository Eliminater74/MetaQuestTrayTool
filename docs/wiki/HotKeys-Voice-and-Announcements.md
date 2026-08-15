# HotKeys, voice, and headset announcements

An **elevated** tray cannot be clicked from SteamVR (including Air Link → SteamVR). Configure these **before** you put the headset on.

## HotKeys

**Tray Tool → Enable HotKeys → Configure**

Defaults use **Ctrl + Numpad**:

| Shortcut | Action |
| --- | --- |
| Ctrl+Num 1 | ASW Off |
| Ctrl+Num 2 | ASW Auto |
| Ctrl+Num 3 | ASW 45 FPS |
| Ctrl+Num 4 | Cycle ASW (Off → Auto → 45 → 30 → 18) |
| Ctrl+Num 5 | Cycle super sampling |
| Ctrl+Num 6 | Apply global defaults |
| Ctrl+Num 7 | Restart OVRService |
| Ctrl+Num 8 | Cycle Performance HUD |
| Ctrl+Num 9 | Open Meta Horizon Link |
| Ctrl+Num 0 | Start SteamVR over Link (PreventDashLaunch) |
| Ctrl+Shift+Num 0 | Start SteamVR |

Also bindable (no default numpad): Open Debug Tool, Open SteamVR Home, Recover PCVR, desktop/VR audio, OpenXR Meta/SteamVR, close overlays, apply GPU presets.

Record a new chord, or restore defaults, in Configure.

## Voice commands

**Tray Tool → Enable voice commands → Configure**

Windows **System.Speech** (Windows 10/11).

- **Push-to-talk** (recommended): default **Ctrl+Shift+V**, then speak one phrase.
- **Always-on:** uncheck PTT; raise minimum confidence.
- Optional preferred microphone (temporarily becomes Windows default while listening).
- Optional **spoken confirmation** of the action name.

Spell out **A S W**, **H U D**, **steam v r**, **open x r**, **GPU**.

| Say (examples) | Action |
| --- | --- |
| apply global / apply defaults | Apply global defaults |
| restart service / restart o v r service | Restart OVRService |
| A S W off / auto / forty five | ASW modes |
| cycle A S W | Cycle ASW |
| cycle super sampling | Cycle SS |
| toggle H U D / performance H U D | Cycle Perf HUD |
| open meta link / open oculus client | Open Horizon Link |
| open debug tool / launch debug tool | Oculus Debug Tool GUI |
| dash to steam v r / start steam v r over link | PreventDashLaunch → SteamVR |
| start steam v r / launch steam v r | Start SteamVR |
| steam v r home | Open SteamVR Home |
| recover PCVR / recover link | Recover PCVR |
| restore desktop audio / restore audio | Fallback speakers |
| switch to VR audio / headset audio | VR devices |
| switch open x r meta / steam open x r | OpenXR runtime |
| close overlays / kill overlays | Configured overlay processes |
| apply GPU preset | GPU-tier Link + globals |

**Custom phrases:** map your wording to any tray action in Configure. **Test listen once** is on that window.

## Headset announcements

**Tray Tool → Speak status in headset** (off by default)

Windows TTS on the Link / VR audio path so you **hear** what the tray is doing when you cannot see desktop balloons.

Examples: “Connected. Air Link.” · “Applying profile. Beat Saber.” · “Launching. Half-Life Alyx.” · “Starting SteamVR.”

Sub-toggles: connect, disconnect, profiles, launch, SteamVR start/exit, Steam Link assist. **Quiet while a game profile is active** = connect/disconnect only mid-game.

**Spoken voice:** Auto prefers an English **female** Windows TTS voice (Microsoft Zira when installed). Choose any installed voice from the list. Voice-command confirmations use the same voice.

Starting SteamVR (button / **Ctrl+Shift+Num 0** / **start steam v r**) always tries to speak **“Starting SteamVR.”** in the headset when a Quest playback device is found.

**Test in headset** speaks a sample phrase. Enable **Use Audio Switcher** so TTS actually routes to the Quest.

Waits ~900 ms for Link audio; uses WASAPI if Windows default is not the headset.

## Related

Repo copy: [VOICE-AND-HOTKEYS.md](https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/docs/VOICE-AND-HOTKEYS.md)
