# Quest Link vs Steam Link (plain English)

Two different ways to play PC VR on a Quest. They are **not** the same pipe. This tray can do more on one of them than the other.

## The 30-second version

| You launched… | Picture | What this tray can push from the PC |
| --- | --- | --- |
| **Quest Link** or **Air Link** (Meta app) | Headset talks to **Meta on the PC** (`OVRService`) | **Almost everything:** Super Sampling, ASW, FOV (Game Settings), Link bitrate / encode / sharpening (Quest Link page), OpenXR, audio, power, optional Dash → SteamVR. Headset CPU/GPU still needs **ADB** if you want those. |
| **Steam Link** (Steam app on the Quest) | Headset talks to **Steam only** | **Not** SS / ASW / FOV / Link bitrate from this tray (Steam owns the stream). OpenXR assist, audio, power, HotKeys still work. **Headset tweaks = ADB only.** Stream quality = Steam Link in the headset + SteamVR Video. |

**Full tray tweaks (OTT-style) = Quest Link / Air Link.**  
**Steam Link alone cannot receive Meta Debug Tool or Link registry commands.** There is no “secret” hook.

---

## Why both exist

**Quest Link / Air Link**  
Meta’s PCVR runtime. The PC can talk to the headset through OVRService. That is how Oculus Tray Tool always worked: SS, ASW, bitrate, etc. You **need** [Meta Horizon Link](https://www.meta.com/quest/setup/) on the PC.

**Steam Link**  
Valve’s stream. No Meta PC app required. Simpler if you never want Horizon Link running. Quality knobs live in **Steam**, not in Meta’s registry.

You can **uninstall Meta** if you *only* use Steam Link. Then skip Quest Link, OVRService, and PreventDashLaunch — they will not do anything useful.

---

## What each tweak actually is

Think of **three buckets**:

1. **PC → Meta Link** (needs Link / Air Link running)  
   Game Settings: Super Sampling, ASW, FOV, HUD.  
   Quest Link page: bitrate, encode width, HEVC, sharpening, DBR.  
   Written to OVRService / `RemoteHeadset`. **Skipped** if Status shows Steam Link or Virtual Desktop.

2. **PC → Quest over ADB** (Developer Mode; USB or wireless ADB)  
   Headset page: CPU/GPU, refresh, FFR, texture, capture.  
   Works on **Link or Steam Link** as long as ADB is connected. Independent of Air Link.  
   Props **reset when the Quest reboots** — leave apply-on-connect on.

3. **Steam’s own UI** (Steam Link path)  
   In-headset Steam Link quality / bitrate. SteamVR → Video (resolution, motion smoothing).  
   This tray does **not** write those.

---

## If you want SteamVR games *and* full tweaks

Do **not** start the **Steam Link** app on the Quest.

Use this instead (what this tool is built for):

1. Install Meta Horizon Link. Start **Quest Link** or **Air Link**.
2. On **Service & Startup**, turn on **PreventDashLaunch** (blocks Meta Dash via registry — it does **not** kill Meta processes).
3. Connect Link. The tray can auto-start **SteamVR** over that Link session.
4. Now Game Settings + Quest Link pages apply, *and* you play SteamVR titles.

That is “Dash → SteamVR”: Link carries the video, SteamVR runs the games, Meta Dash stays out of the way. Details: [[Dash-to-SteamVR]].

SteamVR **cannot click** an elevated tray icon. Set [[HotKeys-Voice-and-Announcements]] before you put the headset on.

---

## Pick your setup

**“I want every slider in this app.”**  
Quest Link or Air Link + Meta on the PC. Add PreventDashLaunch if you play SteamVR. Add Headset ADB if you also want CPU/GPU/FFR.

**“I only use the Steam Link app. I hate Meta on the PC.”**  
Uninstall Meta. Use Steam Link + SteamVR. This tray still helps with OpenXR, audio, power, HotKeys. Headset CPU/GPU only if you set up ADB. Ignore SS / ASW / Quest Link pages while Steam Link is the streamer — the Status banner will say they are skipped.

**“I use Virtual Desktop.”**  
Same idea as Steam Link for Meta knobs: Link/ODT skipped. ADB / OpenXR / audio / power still work. Bitrate in VD’s own settings.

---

## Quick checks

- Status chip says **Air Link** or **wired Link** → Game Settings + Quest Link should apply (reconnect or restart OVRService after bitrate changes).
- Status chip says **Steam Link / SteamVR** or **Virtual Desktop** → those Meta writes are skipped on purpose. Check **Log**.
- Headset CPU/GPU did nothing → ADB is not connected (or not trusted). Air Link being “on” is not enough.

More: [[Quest-Link]] · [[Headset-ADB]] · [[Game-Settings-and-Profiles]] · [[FAQ]]
