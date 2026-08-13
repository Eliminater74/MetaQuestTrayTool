# HotKeys and voice commands

Both routes call the same **`HotKeyCommandService`** actions (ASW, SS, Link, OVRService, Dash → SteamVR, SteamVR Home, Debug Tool, …).

Enable on **Tray Tool** in the sidebar shell. Settings persist in `settings.json`.

## Why this matters in SteamVR

When the tray runs **elevated** (recommended so OpenXR / OVRService / profiles never hit UAC), **SteamVR cannot interact with that tray menu** — including via Air Link → SteamVR desktop overlays. Windows isolates elevated UI from the VR compositor for security.

So mid-session control is intentionally:

1. **HotKeys** (global shortcuts)
2. **Voice** (push-to-talk or always-on)
3. **Automation** (profiles on game launch, apply-on-connect, Dash → SteamVR auto, overlay close, audio switch, etc.)

Configure shortcuts and phrases **before** you put the headset on.

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
| Ctrl+Num 8 | Cycle Performance HUD |
| Ctrl+Num 9 | Open Meta Horizon Link |
| Ctrl+Num 0 | Kill Dash → start SteamVR |

Assign **Open Oculus Debug Tool** or **Open SteamVR Home** in Configure if you want a shortcut (no default numpad binding — Num 0–9 are taken).

You can add bindings, change shortcuts (Record…), and restore defaults in the configure window.

HotKeys require the tray app to be running. They work globally, including in VR.

---

## Voice commands

**Tray Tool → Enable voice commands → Configure**

Uses Windows **`System.Speech`** recognition (Windows 10/11 only).

### Push-to-talk (recommended)

Default shortcut: **Ctrl+Shift+V** → speak one phrase → release.

Push-to-talk avoids game audio triggering commands. Change the shortcut in Configure.

### Always-on (optional)

Uncheck **Push-to-talk only** in Configure. Raise **Minimum confidence** to reduce false triggers.

### Microphone

Optionally pick a preferred capture device. The tray temporarily switches Windows’ default mic while listening, then restores it (System.Speech uses the default device).

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
| toggle H U D / performance H U D | Cycle Performance HUD |
| open meta link / show meta link / open oculus client | Open Meta Horizon Link |
| open debug tool / open oculus debug tool / launch debug tool | Open Oculus Debug Tool GUI |
| kill dash / dash to steam v r / start steam v r | Kill Dash → start SteamVR |
| open steam v r home / steam v r home / launch steam v r home | Open SteamVR Home (steamtours) |

Custom phrases can be added in Configure (phrase → tray action).

Spell out **A S W**, **H U D**, and **steam v r** — Windows recognition handles that better than “ASW” / “SteamVR” as one word.

Use **Test listen once** in Configure to try without leaving the window.

### Troubleshooting

- Check **Log** for “Voice command not recognized” or engine errors.
- Ensure the Windows speech language matches how you speak.
- Prefer a clear PC mic; raise min confidence if always-on misfires.

---

## Not implemented yet

- Hotkey profiles per game (global only today)

**Dash → SteamVR** (kill OculusDash + launch SteamVR over Air Link) is available on Service & Startup / Quest Link / tray / hotkey / voice — inspired by [OculusKiller](https://github.com/DevOculus-Meta-Quest/OculusKiller). Optional **PreventDashLaunch** and **CoreChannel** (`LIVE` / `PublicTest` / `NO_UPDATES`) live on Service & Startup. It does not permanently replace `OculusDash.exe` (use OculusKiller / Dash Manager for that install style).

**SteamVR Home** is on-demand (Service & Startup / tray / hotkey / voice) — Meta’s old Oculus Home is gone.

See [ROADMAP.md](../ROADMAP.md).
