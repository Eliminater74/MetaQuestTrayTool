# Shell and tray

Left-click the tray headset icon to open the sidebar shell. Close the window to keep the tray running (if **Close window to tray** is on). Use **Exit** on the tray menu to quit.

## Sidebar pages

| Page | Purpose |
| --- | --- |
| **Status** | Default home. Live chips: PCVR Ready, SteamVR install/running/Stable\|Beta, OpenXR, OVRService, elevation, session type, ADB, battery/Wi‑Fi, active profile, HotKeys/Voice, Dash→SteamVR, GPU, audio. |
| **Game Settings** | Global Super Sampling, ASW, FOV, HUD, OpenXR, overlay-close list. Opens Profiles / Global defaults. |
| **Tray Tool** | Start with Windows, elevation, audio switcher, HotKeys, voice, headset announcements, updates, theme, notifications. |
| **Power Options** | VR vs desktop power plan, USB selective suspend, restart OVRService after sleep. |
| **Service & Startup** | Start/Stop/Restart OVRService, Manual-at-boot, PreventDashLaunch, CoreChannel, SteamVR Home. |
| **Log Window** | Startup checks, profile applies, Link writes, audio/power. Refresh / open log folder. |
| **Advanced** | Reset settings, wipe profiles, library import, backup export/import, check updates, Debug Tool GUI. |
| **Quest Link** | RemoteHeadset bitrate, encode width, HEVC, slices, sharpening, DBR, Mobile ASW, GPU presets. |
| **Headset** | ADB props, Wireless Pair/Connect, trusted serial, apply-on-connect, **VR headsets only**, **Pause ADB**. |
| **VR Tools** | Curated third-party links (overlays, wireless PCVR, tracking, essentials). |
| **Info** | PCVR Ready checklist, Recover PCVR, session probe, system dump. |
| **Donate / About** | PayPal + version / credits. |

![Status dashboard](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/00-status.png)

![Tray Tool page](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/03-tray-tool.png)

![VR Tools links](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/16-vr-tools.png)

## Tray menu (right-click)

Typical actions:

- Open Settings, Meta Horizon Link, Oculus Debug Tool
- Start SteamVR over Link, Open SteamVR Home, Recover PCVR, Cycle Perf HUD, Save last-good
- Game Settings, Profiles, Quest Link, OpenXR, Audio, Power, Headset (ADB: VR headsets only / Pause / Resume)
- Start / stop / restart **OVRService**
- Check for updates, VR Tools, Donate, Exit

## Themes

**Tray Tool → Theme:** Pure Black (default), Dark, or Light.

## Single instance

Only one tray instance can own the app lock. Starting a second copy now shows a blocker with
the running process ID and explains how to close it. The second copy then signals the existing
tray to show its shell and exits. The normal-user `--session-helper` process is an internal
launch helper; it does not block the tray from starting and is shut down during tray exit, with
a safe fallback if it becomes stuck.

## Idle behavior

Watchers poll slowly when nothing is connected (~30–45s) and faster during PCVR. Disabled features stop their timers. Status/Info pause when the shell is hidden to the tray.

## Related

- [[Getting-Started]]
- [[Service-and-Startup]]
- [[HotKeys-Voice-and-Announcements]]
