# HotKeys and voice commands

Both routes call the same **`HotKeyCommandService`** actions (Debug Tool, Link, OVRService).

Enable on **Tray Tool** in the sidebar shell. Settings persist in `settings.json`.

---

## HotKeys

**Tray Tool → Enable HotKeys → Configure**

Default bindings use **Ctrl + Numpad** so they rarely clash with games.

| Shortcut | Action |
| --- | --- |
| Ctrl+Num 1 | ASW Off |
| Ctrl+Num 2 | ASW Auto |
| Ctrl+Num 3 | ASW 45 FPS |
| Ctrl+Num 4 | Cycle ASW (Off → Auto → 45 → 30 → 18) |
| Ctrl+Num 5 | Cycle super sampling |
| Ctrl+Num 6 | Apply global defaults |
| Ctrl+Num 7 | Restart OVRService |
| Ctrl+Num 8 | Toggle Performance HUD |
| Ctrl+Num 9 | Open Meta Horizon Link |

You can add bindings, change shortcuts (Record…), and restore defaults in the configure window.

HotKeys require the tray app to be running. They work globally, including in VR.

---

## Voice commands (preview)

**Tray Tool → Enable voice commands → Configure**

Uses Windows **`System.Speech`** recognition (Windows 10/11 only).

### Push-to-talk (recommended)

Default shortcut: **Ctrl+Shift+V** → speak one phrase → release.

Push-to-talk avoids game audio triggering commands. Change the shortcut in Configure.

### Always-on (optional)

Uncheck **Push-to-talk only** in Configure. The mic stays open; use only in a quiet room.

### Spoken confirmation

When enabled, Windows TTS says the action name after a successful match (e.g. “ASW Off”).

### Supported phrases

| Say (examples) | Action |
| --- | --- |
| apply global / apply defaults | Apply global defaults |
| restart service / restart o v r service | Restart OVRService |
| A S W off | ASW Off |
| A S W auto | ASW Auto |
| A S W forty five | ASW 45 FPS |
| cycle A S W | Cycle ASW |
| cycle supersampling / cycle super sampling | Cycle super sampling |
| toggle H U D / performance H U D | Toggle Performance HUD |
| open meta link / show meta link / open oculus client | Open Meta Horizon Link |

Spell out **A S W** and **H U D** — Windows recognition handles that better than “ASW” as one word.

Use **Test listen once** in Configure to try without leaving the window.

### Troubleshooting

- Check **Log** for “Voice command not recognized” or engine errors.
- Ensure the Windows speech language matches how you speak.
- Desktop mic usually works better than Link mic for recognition.
- Voice is **preview** quality: custom phrases and mic picker are planned.

---

## Not implemented yet

- Custom voice phrases / editable grammar
- Microphone device picker
- Oculus Homeless
- Hotkey profiles per game (global only today)

See [ROADMAP.md](../ROADMAP.md).
