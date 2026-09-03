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
| Ctrl+Shift+Num 8 | Take screenshot (Quest Link preferred, ADB fallback) |
| Ctrl+Shift+Num 9 | Take headset screenshot (ADB) |

Also bindable: Quest Link mirror screenshot, Open Debug Tool, Open SteamVR Home, Recover PCVR, desktop/VR audio, OpenXR Meta/SteamVR, close overlays, apply GPU presets.

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
| take screenshot / capture screenshot / save screenshot | Screenshot (Quest Link preferred, ADB fallback) |
| take link screenshot / quest link screenshot / mirror screenshot | Quest Link mirror screenshot |
| take headset screenshot / quest screenshot / ADB screenshot | Headset screenshot (ADB) |

**Custom phrases:** map your wording to any tray action in Configure. **Test listen once** is on that window.

## Headset announcements

**Tray Tool → Speak status in headset** (off by default)

Windows TTS on the Link / VR audio path so you **hear** what the tray is doing when you cannot see desktop balloons.

Examples: “Connected. Air Link. SteamVR OpenXR runtime will be used. Now starting SteamVR.” · “Launching Beat Saber. Steam game. Profile Beat Saber is armed.” · “Beat Saber detected. Profile applied. OpenXR is set to SteamVR.” · “Beat Saber closed. Restored global settings after profile.” · “SteamVR closed. Stopping Meta service for 10 seconds.”

Sub-toggles: connect, disconnect, profiles, launch, SteamVR start/exit, Steam Link assist, important manual action results, HotKey action results, voice command results, screenshot confirmations, manual audio routing, headset/ADB results, PCVR recovery, and experimental launch results. **Quiet while a game profile is active** still permits connect/disconnect, profile apply/restore, SteamVR-exit 10s wait, HotKey/voice/screenshot confirmations, and experimental outcomes, while suppressing lower-priority chatter.

**Spoken voice:** Auto prefers an English **female** Windows TTS voice (Microsoft Zira when installed). Choose any installed voice from the list. Voice-command confirmations use the same voice.

Starting SteamVR (button / **Ctrl+Shift+Num 0** / **start steam v r**) always tries to speak **“Starting SteamVR.”** in the headset when a Quest playback device is found. Auto PreventDashLaunch also speaks **“Please wait. Starting SteamVR.”** as soon as Link is confirmed. Exit under PreventDashLaunch speaks **“SteamVR closed. Stopping Meta service for 10 seconds.”** *before* OVRService stops, while headset audio is still there.

Taking a screenshot from the tray **Screenshots** menu, **Ctrl+Shift+Num 8**, or voice **take screenshot** prefers Quest Link / Air Link mirror capture while Meta Link is actively streaming, then falls back to trusted-headset ADB. **Take link screenshot** forces Oculus Mirror capture. **Ctrl+Shift+Num 9** or **take headset screenshot** forces ADB. Saved PNGs are written to `%AppData%\MetaQuestTrayTool\screenshots\`, and the app queues **“Screenshot taken.”** after the file has been saved.

**Test in headset** speaks a sample phrase. Enable **Use Audio Switcher** so TTS actually routes to the Quest.

For the opt-in **Experimental MSFS 2024 VR launch** profile option, headset speech also reports when the delayed VR toggle was sent, or when the MSFS window/focus/toggle step failed. The automation cannot click verification dialogs or know when a flight is ready; see [[Game-Settings-and-Profiles]] and verify the first run manually.

The connect announcement also reports the active Windows OpenXR runtime (Meta or SteamVR), whether automatic VR audio switching is enabled, and the transport. When Dash → SteamVR is enabled, it reports that SteamVR OpenXR will be used because the runtime is switched as SteamVR starts. Action, HotKey, voice command, and screenshot result categories use short success/skip/failure summaries; full details remain in Log. Waits for Link audio (minimum connect delay is about 2.2 seconds); uses WASAPI if Windows default is not the headset.

## Related

Repo copy: [VOICE-AND-HOTKEYS.md](https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/docs/VOICE-AND-HOTKEYS.md)
