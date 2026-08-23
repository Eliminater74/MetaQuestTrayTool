# Meta Quest Tray Tool

[![Latest release](https://img.shields.io/github/v/release/Eliminater74/MetaQuestTrayTool?label=Release)](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Eliminater74/MetaQuestTrayTool/total?label=Downloads)](https://github.com/Eliminater74/MetaQuestTrayTool/releases)
[![Stars](https://img.shields.io/github/stars/Eliminater74/MetaQuestTrayTool?label=Stars)](https://github.com/Eliminater74/MetaQuestTrayTool/stargazers)

Windows tray utility for Meta Quest / Oculus Link and SteamVR OpenXR, by **Eliminater74**. This is a **new C# project**, not a continuation of the unfinished conversion of [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/).

**User guide:** [Wiki](https://github.com/Eliminater74/MetaQuestTrayTool/wiki) — start with [Quest Link vs Steam Link](https://github.com/Eliminater74/MetaQuestTrayTool/wiki/Quest-Link-vs-Steam-Link) if you are unsure which app to launch on the headset.

### Preview

[Watch a short walkthrough (MP4)](docs/media/demo.mp4)

<p align="center">
  <img src="docs/media/01-game-settings.png" alt="Game Settings" width="720"/>
</p>

<details>
<summary>More screenshots (shell pages)</summary>

| Page | Preview |
| --- | --- |
| Game Settings | ![Game Settings](docs/media/01-game-settings.png) |
| Game Settings (custom CLI / ADB) | ![Game Settings custom commands](docs/media/02-game-settings-custom-commands.png) |
| Tray Tool | ![Tray Tool](docs/media/03-tray-tool.png) |
| Power Options | ![Power Options](docs/media/04-power-options.png) |
| Service & Startup | ![Service and Startup](docs/media/05-service-startup.png) |
| Log Window | ![Log Window](docs/media/06-log-window.png) |
| Advanced | ![Advanced](docs/media/07-advanced.png) |
| Quest Link | ![Quest Link](docs/media/08-quest-link.png) |
| Headset (capture / trust) | ![Headset capture](docs/media/09-headset-capture.png) |
| Headset (performance) | ![Headset performance](docs/media/10-headset-performance.png) |
| Info | ![Info](docs/media/11-info.png) |
| About | ![About](docs/media/12-about.png) |

_Status_ and _VR Tools_ pages shipped in v1.0.12+ (screenshots for those pages not captured yet).

</details>

---

## Download

**Latest:** [v1.1.14 — MetaQuestTrayTool-Setup-1.1.14.exe](https://github.com/Eliminater74/MetaQuestTrayTool/releases/download/v1.1.14/MetaQuestTrayTool-Setup-1.1.14.exe)

Or open the [latest release](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest) and click the `.exe` asset (the badge at the top of this README always tracks the newest tag).

**Past releases:** [all versions on GitHub](https://github.com/Eliminater74/MetaQuestTrayTool/releases) · **What's new:** [CHANGELOG.md](CHANGELOG.md)

The installer is **self-contained** (includes .NET 8 — no separate runtime install). Settings are stored in `%AppData%\MetaQuestTrayTool\` and are kept if you uninstall.

The **Downloads** badge uses GitHub’s public asset `download_count`. It only counts clicks on the release **`.exe` asset** (not “Source code” zips). Downloads by the repo owner often stay at **0**; other users’ downloads usually show up after a short delay (shields.io also caches).

---

## How to use

### 1. Install and first launch

1. Download and run `MetaQuestTrayTool-Setup-*.exe` from [Releases](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest).
2. Launch **Meta Quest Tray Tool** from the Start Menu (or let it start with Windows if you enabled that during setup).
3. The app lives in the **notification area** (system tray). If you do not see the headset icon, click the **^** overflow chevron near the clock.
4. On first run, approve **Administrator mode** when prompted if you want OpenXR switching, OVRService control, and profile apply to work in-headset without UAC. You can also use **Restart as Administrator** from the tray menu later.

### 2. Open settings

- **Left-click** the tray headset icon, or
- **Right-click** the icon → **Open Settings**

The sidebar shell opens on **Status** by default, then: **Game Settings**, **Tray Tool**, **Power Options**, **Service & Startup**, **Log Window**, **Advanced**, **Quest Link**, **Headset**, **VR Tools**, and **Info**. **Profiles**, HotKeys, Voice, Audio, and Global defaults open as separate windows from those pages or the tray.

### 3. Set your global defaults

1. Open **Profiles** (or **Advanced → Global defaults**) and configure your everyday Link / OpenXR / audio / power preferences.
2. On **Quest Link**, set bitrate, encode width, sharpening, HEVC, and related Link options.
3. On **Game Settings** (tray menu or shell), adjust super sampling, ASW, FOV, and other PCVR tweaks.
4. Changes save automatically to `%AppData%\MetaQuestTrayTool\`.

Global defaults stay applied until a game with a personal profile launches.

### 4. Per-game profiles (optional)

1. Open **Profiles** → **New profile** (or pick a built-in preset such as MSFS 2024 or Beat Saber).
2. Set the **executable name** (e.g. `FlightSimulator.exe`) and any overrides (Link, OpenXR runtime, game settings).
3. Optionally **Launch** from the library / profile (`steam://run/{appId}`), or use the **ignore list** so noisy helper processes do not steal the profile.
4. When that game starts, the profile auto-applies and you get a tray notification.
5. When the game exits, **global defaults are restored** automatically. Mid-session you can **save last-good** SS/ASW/HUD into the active profile.

Export/import profiles and full settings from **Advanced**.

### 5. Quest Link / headset tweaks

- **Quest Link** registry settings usually need a Link reconnect or **OVRService** restart — use **Service & Startup** or the tray menu.
- **Headset** (ADB) tweaks (CPU/GPU, refresh rate, texture size, etc.) apply when a trusted Quest connects. Enable **Developer Mode** on the headset and approve USB debugging once, **or** use **Wireless ADB** (Enable tcpip once, or Wireless debugging **Pair** + Connect). These settings do not survive a headset reboot; the tray re-applies them on connect.
- Only **real VR headsets** are trusted. Phones, tablets, and Android emulators are ignored.
- **Status** / **Info** show PCVR Ready, battery / Wi‑Fi (via ADB), and session type (Air Link vs wired, Steam Link / SteamVR, Virtual Desktop).

### 6. Tray menu quick actions

**Right-click** the tray icon for:

- Open Settings, Meta Horizon Link, Oculus Debug Tool
- Start SteamVR over Link, Open SteamVR Home, Recover PCVR, Cycle Perf HUD, Save last-good
- Game Settings (SS, ASW, FOV, …), Profiles, Quest Link, OpenXR, Audio, Power, Headset
- Start / stop / restart **OVRService**, Check for updates, VR Tools, Donate, Exit

### 7. HotKeys and voice (optional)

On **Tray Tool**:

- **HotKeys** — Enable → Configure. Defaults **Ctrl+Numpad 0–9** (ASW, SS cycle, apply global, restart OVRService, Perf HUD, open Meta Link, Dash → SteamVR). Assign **Open Debug Tool** / **Open SteamVR Home** if you want them. **Required for mid-session control in SteamVR** — an elevated tray cannot be clicked from SteamVR.
- **Voice commands** — Enable → Configure. Default **push-to-talk Ctrl+Shift+V**, then say e.g. “A S W off”, “dash to steam v r”, or “open steam v r home”. Optional mic preference, min confidence, and custom phrases. Same actions as hotkeys when you cannot reach the tray.

Full shortcut and phrase list: [docs/VOICE-AND-HOTKEYS.md](docs/VOICE-AND-HOTKEYS.md).

### 8. Tips

- **Themes:** Pure Black (default), Dark, or Light — change on the **Tray Tool** page.
- **Updates:** Tray Tool → Check for updates on start (on by default), **Also check while running** (default weekly: daily / 3 days / week / 2 weeks / month / off), or **Check now** / tray menu **Check for updates…**. Confirms, downloads the latest Setup.exe from GitHub, exits the app, then installs over the current location (ADB is stopped first so bundled platform-tools can be replaced).
- **Start minimized** and **minimize-on-close** keep the app in the tray instead of the taskbar.
- **Service & Startup** highlights **Start** or **Stop** based on whether `OVRService` is running; optional **Manual-at-boot** (no OVRService at Windows sign-in) with documented limits
- Newer Meta runtimes may reject some `server:` ASW commands; check **Log** if something does not apply.
- Pixel density changes often need a **new VR session** to take effect.
- Meta’s old **Oculus Home** is gone — use **Dash → SteamVR** and optional **SteamVR Home** instead.

---

## Requirements

- Windows 10 / 11 (64-bit)
- [Meta Quest PC app](https://www.meta.com/quest/setup/) and/or [SteamVR](https://store.steampowered.com/app/250820/SteamVR/) for the features you use
- For **Headset (ADB)**: Quest **Developer Mode** + USB debugging approval, **or** Wireless ADB (same Wi‑Fi; Enable tcpip once, Wireless debugging **Pair** + Connect, or SideQuest running on the headset which can open port **5555**). ADB is bundled — no Android Studio required

### Steam Link vs Quest Link — which one do I need?

These are **two different PCVR pipes**. This tray can push far more over **Quest Link** than over the **Steam Link app**. Plain-English guide: [wiki — Quest Link vs Steam Link](https://github.com/Eliminater74/MetaQuestTrayTool/wiki/Quest-Link-vs-Steam-Link).

| You start on the Quest… | What carries the picture | What this tray can change from the PC |
| --- | --- | --- |
| **Quest Link** or **Air Link** (Meta) | Meta Horizon Link + `OVRService` | **Full tweaks:** Game Settings SS / ASW / FOV, Quest Link bitrate / encode / sharpening, OpenXR, audio, power. Optional **PreventDashLaunch** so SteamVR games run *over Link* instead of Dash. Headset CPU/GPU still needs **ADB**. |
| **Steam Link** (Steam app) | Steam only — **no Meta PC app required** | **Not** SS / ASW / FOV / Link bitrate (skipped on purpose — Steam owns the stream). OpenXR assist, audio, power, HotKeys still work. **Headset tweaks = ADB only.** Quality = Steam Link in the headset + SteamVR Video. |

**Want SteamVR games *and* the full tray (OTT-style)?** Do **not** use the Steam Link app. Use **Quest Link / Air Link** + **PreventDashLaunch** on Service & Startup (Dash is blocked via registry — this tool does not kill Meta processes). That is the intended Steam-first path.

**Steam Link app only (no Meta)?** You can uninstall Meta Horizon Link. Skip Quest Link, OVRService, and PreventDashLaunch. Use **Headset** (ADB) if you still want CPU / GPU / FFR on the Quest.

**This tray tool targets Quest Link + SteamVR OpenXR.** Steam Link / Virtual Desktop sessions still get OpenXR assist, audio, power, and ADB; Game Settings / Quest Link pages will show they are skipped.

**Meta Horizon Link at startup:** Meta usually opens because **`OVRService`** is a Windows service set to **Automatic** at install (not listed in Settings → Startup apps). **Service & Startup** can set **Manual** so the runtime does not start at boot — with clear notes on what that does and does not do. You must **Start** OVRService, open Meta Horizon Link, or enable **Start Oculus service when tool starts** before Quest Link PCVR. Meta updates may reset Automatic; the tray can re-apply Manual on launch. This does **not** block Meta when you deliberately start Link. Steam Link–only with no Quest Link: uninstall Meta PC software.

---

## What works now

Release **[v1.1.14](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)** is current — see **[CHANGELOG.md](CHANGELOG.md)** for every release. Tray **Pause ADB** (until resume or 2 hours) and **VR headsets only** toggle so phones/TVs can use ADB without quitting. Also: headset wait cues, unelevated SteamVR helper, 10s OVR drop, voice picker. **v1.1.11 crashed on start**; 1.1.12+ fixed that.

### Shell & tray

- Notification-area host with themed menu (**Pure Black** / Dark / Light)
- OTT-style sidebar: **Status** (default), Game Settings, Tray Tool, Power Options, Service & Startup, Log Window, Advanced, Quest Link, Headset, **VR Tools**, Info — plus Donate / About
- Hover tooltips; close-to-tray; start minimized; hide from Alt+Tab; single-instance
- **Hands-free Administrator mode** (on by default): one Windows approval, then elevated at logon so OpenXR / OVRService / profiles never need UAC in-headset. **SteamVR cannot click that elevated tray** (Air Link → SteamVR included) — use HotKeys, voice, and automation mid-session
- Settings in `%AppData%\MetaQuestTrayTool\settings.json` (profiles in `profiles.json`)

### Status & SteamVR awareness

- Live **Status** chips: PCVR Ready, SteamVR install/running/Stable|Beta, OpenXR, OVRService, elevation, session type, ADB, battery/Wi‑Fi, active profile, HotKeys/Voice, Dash→SteamVR armed, GPU, audio
- **SteamVR install detect** (path, file version, Stable vs Beta) with Install SteamVR action
- **PCVR Ready** checklist on Info (Steam-biased) with fix actions
- **Recover PCVR** after a Link / Steam / VD drop (tray + Info)
- Session probe: Meta Air Link vs wired (`DeviceCache` `isUsingAirLink`), Steam Link / SteamVR, Virtual Desktop

### Game settings & profiles

- ODT via `OculusDebugToolCLI`: Super Sampling, ASW (Auto / Off / 45 / 30 / 18), FOV H/V, Adaptive GPU, mip flags, FOV stencil, Visual HUD, OVR priority — plus tray **Cycle Perf HUD**
- **Open Oculus Debug Tool GUI** (`OculusDebugTool.exe`) from shell / tray / hotkey / voice
- Personal profiles: auto-apply on process start → restore global on exit; Steam/Meta library picker + cover art; **Launch** (`steam://run`); ignore list; **save last-good**; Link + OpenXR overrides; custom CLI / ADB lines; built-in global + game presets (MSFS 2024, Beat Saber, etc.)
- Optional **close overlays on Link connect** (Afterburner / RTSS / CAM / iCUE-style list)

### Quest Link / Dash → SteamVR

- Link registry (`RemoteHeadset`): bitrate, encode width, HEVC, sliced encoding, sharpening, distortion, DBR / max / offset, Mobile ASW — presets on Quest Link page — see [docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md)
- **PreventDashLaunch → SteamVR over Link**: registry blocks Dash (no Meta process killing); auto-start SteamVR on Meta Link connect; optional restart OVRService when SteamVR exits (tray / Ctrl+Num 0 / voice “dash to steam v r”)
- **SteamVR Home** (`steamtours.exe`) on demand — Service & Startup / tray / hotkey / voice (Meta’s old Oculus Home is gone)
- **Open Meta Horizon Link** from tray / hotkey / voice

### OpenXR / audio / power

- Switch ActiveRuntime Meta vs SteamVR (global + per-profile); apply preferred on start
- **Steam Link assist**: force SteamVR OpenXR while Steam Link / SteamVR is active, restore preferred when it ends
- Under Steam Link / VD, Meta Link registry + ODT are gated (there is no PC command path like Quest Link); ADB / OpenXR / power / audio still apply — [full comparison](https://github.com/Eliminater74/MetaQuestTrayTool/wiki/Quest-Link-vs-Steam-Link)
- Audio auto-switch when Link audio is active (does not steal speakers just because Meta virtual audio is installed); separate **communications** playback/recording pickers
- Power plan auto-switch, USB selective suspend off, restart `OVRService` after sleep

### Headset (ADB)

- Bundled Google platform-tools; CPU/GPU, texture size, refresh, FFR, chroma, capture; paste text / proximity / guardian helpers
- Auto-apply on connect (props reset on Quest reboot)
- **Wireless ADB**: host, connect port, **Pair** (pairing port + code), Connect / Disconnect, Enable tcpip over USB, auto-reconnect (saved IP only — no LAN scan) — SideQuest on the headset can also open an ADB port (often 5555)
- **VR headsets only** (on by default; Headset page + tray → Headset (ADB)): disconnect phones/tablets/TVs that show up over wireless ADB; uncheck to leave any ADB device connected. Tweaks still never run on non-headsets
- **Pause ADB** (tray → Headset (ADB)): stop polling / reconnect / disconnect until you resume, or for 2 hours — use while debugging a phone or TV without quitting the tray
- Trusted VR headset serial only — phones / tablets / emulators ignored
- Battery / charge / Wi‑Fi status via ADB dumpsys

### HotKeys, voice, updates, tools

- HotKeys: default **Ctrl+Numpad 0–9**; configure UI; assign Debug Tool / SteamVR Home / recover / OpenXR / overlays / GPU preset
- Voice: Windows speech, PTT (**Ctrl+Shift+V**) or always-on, mic picker, min confidence, custom phrases, spoken confirm — recover PCVR, desktop/VR audio, OpenXR meta/steam, close overlays, GPU preset — [docs/VOICE-AND-HOTKEYS.md](docs/VOICE-AND-HOTKEYS.md)
- **Headset announcements** (Tray Tool): TTS in the Quest on connect, the wait before SteamVR, SteamVR closed + 10s Meta service stop (spoken *before* OVR drops), profile apply, launch — when desktop toasts are not visible in-headset
- In-app updates from GitHub `v*` (on start, schedule, or Check now) — shows **what's new** before you install; ADB stopped before Setup; Setup itself shows the changelog page
- **VR Tools** page + tray: curated third-party links (Play more games, Overlays, Performance, Wireless PCVR, Quest & sideloading, Tracking, Essentials)
- Backup export/import from Advanced; Donate (PayPal); **quiet tray idle** — adaptive watcher cadence (~30–45s when unused, faster only in PCVR / armed features); timers stop when features are off; Status/Info pause when the shell is hidden to the tray; shared Link probe caches

### Out of scope / not planned

- **Permanent AirLink** (Meta-side)
- Full Dash Manager–style dash customizer / permanently replacing `OculusDash.exe`
- Reviving **Oculus Home / Homeless** (use Dash → SteamVR + optional SteamVR Home)
- Authenticode code-signed Setup.exe (SmartScreen may warn until budget allows)
- macOS / Linux; hotkey profiles per game (global only today)

### Runtime caveats

- Newer Meta runtimes may reject `server:` ASW CLI — check **Log**
- Pixel density / SS often needs a **new VR session**
- Link registry changes usually need a **Link reconnect** or `OVRService` restart
- ADB `debug.oculus.*` props **reset on Quest reboot** — leave apply-on-connect on
- Wireless ADB is **not** Air Link — separate Developer Mode / pair flow
- PreventDashLaunch blocks Dash via registry only — this tool does **not** kill Meta processes or replace `OculusDash.exe` on disk

See [ROADMAP.md](ROADMAP.md) and [TODO.md](TODO.md) for history and remaining housekeeping.

---

## Project docs

| File | Use when you need to… |
| --- | --- |
| [docs/README.md](docs/README.md) | Index of all documentation |
| [docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md) | ODT registry keys vs CLI commands (from Meta binaries) |
| [docs/VOICE-AND-HOTKEYS.md](docs/VOICE-AND-HOTKEYS.md) | Hotkey shortcuts and voice phrase reference |
| [docs/media/README.md](docs/media/README.md) | Screenshots and demo video |
| [ROADMAP.md](ROADMAP.md) | See the planned phases and why they exist |
| [TODO.md](TODO.md) | Check what is done vs next, checkbox style |
| [REDDIT.md](REDDIT.md) | Copy-paste Reddit announcement (title + body) |
| [installer/README.md](installer/README.md) | Build the Windows Setup.exe locally |

---

## Build from source (developers)

**Requirements:** [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) with the **.NET desktop development** workload, or .NET 8 SDK.

**Visual Studio:** open `MetaQuestTrayTool.sln`, choose `Debug` or `Release`, press F5.

**Command line:**

```powershell
dotnet build .\MetaQuestTrayTool.sln -c Release
dotnet run --project .\src\MetaQuestTrayTool\MetaQuestTrayTool.csproj
```

Starting or stopping `OVRService` during development may require running as Administrator.

### Build Setup.exe locally

```powershell
winget install --id JRSoftware.InnoSetup -e   # once
.\scripts\build-installer.ps1
```

Output: `dist\MetaQuestTrayTool-Setup-<version>.exe`. Details: [installer/README.md](installer/README.md).

### GitHub Actions

- Every push to `main` runs **CI** (build + publish smoke test).
- Pushing a tag `v*` (or **Actions → Release → Run workflow**) builds Setup.exe and publishes a [GitHub Release](https://github.com/Eliminater74/MetaQuestTrayTool/releases):

```powershell
git tag v1.0.1
git push origin v1.0.1
```

---

## Inspired by

- [Oculus Tray Tool](https://www.guru3d.com/download/oculus-traytool-download/) by ApollyonVR
- [Oculus_Tray_Manager](https://github.com/DevOculus-Meta-Quest/Oculus_Tray_Manager) (earlier C# conversion of OTT)
- [MetaQuestTrayManager](https://github.com/DevOculus-Meta-Quest/MetaQuestTrayManager) (earlier WPF rewrite)
- [Oculus VR Dash Manager](https://github.com/DevOculus-Meta-Quest/Oculus-VR-Dash-Manager)

This repo is a clean start. The old conversion work stays in those repositories.
