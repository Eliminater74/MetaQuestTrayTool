# Meta Quest Tray Tool wiki

Windows tray app for **Quest Link / Air Link + SteamVR OpenXR**. Free, modern OTT-inspired settings — a **new C# app**, not a decompile of ApollyonVR’s Oculus Tray Tool.

**Current public release:** [v1.1.18](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

[Download Setup.exe](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest) · [GitHub repo](https://github.com/Eliminater74/MetaQuestTrayTool) · [Donate](https://www.paypal.com/donate/?business=X76ZW4RHA6T9C&no_recurring=0&item_name=Eliminater74+builds+Meta+Quest+Tray+Tool+%E2%80%94+free+Quest+Link+%26+SteamVR+tray+settings.+Your+gift+keeps+it+going.&currency_code=USD)

---

## Watch

| Video | What it shows |
| --- | --- |
| [Walkthrough (25 Aug 2025, v1.1.18)](https://github.com/Eliminater74/MetaQuestTrayTool/raw/main/docs/media/demo.mp4) | Full shell walkthrough — Status, pages, profiles, VR Tools |
| [Earlier walkthrough (13 Aug 2026)](https://github.com/Eliminater74/MetaQuestTrayTool/releases/download/v1.1.2/walkthrough-2026-08-13.mp4) | Previous recording (pre–Status / VR Tools stills) |

**Screenshot gallery:** [[Screenshots-and-Videos]] · [docs/media](https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/docs/media/README.md)

<p align="center">
  <a href="https://github.com/Eliminater74/MetaQuestTrayTool/raw/main/docs/media/demo.mp4">
    <img src="https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/00-status.png" alt="Status dashboard" width="720"/>
  </a>
</p>

---

## Start here

1. **Pick your pipe** — [[Quest-Link-vs-Steam-Link]] (Quest Link vs the Steam Link app — they are not the same)
2. [[Getting-Started|Install and first launch]]
3. Set [[Game-Settings-and-Profiles|global defaults + per-game profiles]]
4. Tune [[Quest-Link]] bitrate / encode, then [[Dash-to-SteamVR]] if you want SteamVR games **over Link** (full tweaks). Skip this if you only use the Steam Link app.
5. Configure [[HotKeys-Voice-and-Announcements]] **before** you put the headset on

**SteamVR cannot click an elevated tray menu.** Mid-session control is HotKeys, voice, and automation — not the tray icon.

---

## What the app does

| Area | Highlights |
| --- | --- |
| Status | Live PCVR Ready chips, session type (Air / wired / Steam Link / VD), SteamVR install |
| Game settings | Super Sampling, ASW, FOV, HUD via Oculus Debug Tool CLI + GUI |
| Profiles | Auto-apply on game launch, restore globals on exit, library Launch |
| Quest Link | Bitrate, encode width, HEVC, sharpening, DBR, Mobile ASW |
| Dash → SteamVR | PreventDashLaunch registry (no Meta process killing); auto SteamVR on real Link stream |
| OVRService | Start / stop / restart; **Manual-at-boot** so Meta does not pop at Windows sign-in |
| OpenXR | Switch Meta vs SteamVR (global + per-profile); Steam Link assist |
| Audio / power | Switch to headset on PCVR start, restore desktop on exit; power plan; USB suspend |
| Headset ADB | CPU/GPU/refresh/FFR; Wireless Pair; VR headsets only; Pause ADB |
| Voice / HotKeys | Ctrl+Numpad, push-to-talk, headset spoken status |

---

## Requirements

- Windows 10 / 11 (64-bit)
- [Meta Quest PC app](https://www.meta.com/quest/setup/) for Quest Link / Air Link features
- [SteamVR](https://store.steampowered.com/app/250820/SteamVR/) for Steam PCVR
- Headset ADB: Quest **Developer Mode** (USB or Wireless debugging). ADB is bundled.

Installer is **self-contained** (.NET 8 included). Settings live in `%AppData%\MetaQuestTrayTool\` and survive uninstall.

---

## Wiki map

- [[Getting-Started]]
- [[Quest-Link-vs-Steam-Link]]
- [[Shell-and-Tray]]
- [[Game-Settings-and-Profiles]]
- [[Quest-Link]]
- [[Dash-to-SteamVR]]
- [[Service-and-Startup]]
- [[Headset-ADB]]
- [[Audio-and-Power]]
- [[HotKeys-Voice-and-Announcements]]
- [[Troubleshooting]]
- [[FAQ]]
- [[Screenshots-and-Videos]]
