# Meta Quest Tray Tool

[![Latest release](https://img.shields.io/github/v/release/Eliminater74/MetaQuestTrayTool?label=Release)](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Eliminater74/MetaQuestTrayTool/total?label=Downloads)](https://github.com/Eliminater74/MetaQuestTrayTool/releases)
[![Stars](https://img.shields.io/github/stars/Eliminater74/MetaQuestTrayTool?label=Stars)](https://github.com/Eliminater74/MetaQuestTrayTool/stargazers)

Windows tray utility for Meta Quest / Oculus Link and SteamVR OpenXR, by **Eliminater74**. This is a **new C# project**, not a continuation of the unfinished conversion of [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/).

> **Screenshots:** coming soon — the README will be updated with UI images in a later release.

---

## Download

**Recommended:** install the latest Windows Setup from [GitHub Releases](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest).

| Release | Installer |
| --- | --- |
| [v1.0.0](https://github.com/Eliminater74/MetaQuestTrayTool/releases/tag/v1.0.0) | [MetaQuestTrayTool-Setup-1.0.0.exe](https://github.com/Eliminater74/MetaQuestTrayTool/releases/download/v1.0.0/MetaQuestTrayTool-Setup-1.0.0.exe) (~52 MB) |

The installer is **self-contained** (includes .NET 8 — no separate runtime install). Settings are stored in `%AppData%\MetaQuestTrayTool\` and are kept if you uninstall.

Badge counts above update automatically (release downloads and stars). GitHub does not expose public page-view / visitor totals on the README; repo owners can see traffic under **Insights → Traffic**.

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

The sidebar shell has pages for **Game Settings**, **Tray Tool**, **Power**, **Service & Startup**, **Log**, **Advanced**, **Quest Link**, **Headset (ADB)**, **Profiles**, and **Info**.

### 3. Set your global defaults

1. Open **Profiles** (or **Advanced → Global defaults**) and configure your everyday Link / OpenXR / audio / power preferences.
2. On **Quest Link**, set bitrate, encode width, sharpening, HEVC, and related Link options.
3. On **Game Settings** (tray menu or shell), adjust super sampling, ASW, FOV, and other PCVR tweaks.
4. Changes save automatically to `%AppData%\MetaQuestTrayTool\`.

Global defaults stay applied until a game with a personal profile launches.

### 4. Per-game profiles (optional)

1. Open **Profiles** → **New profile** (or pick a built-in preset such as MSFS 2024 or Beat Saber).
2. Set the **executable name** (e.g. `FlightSimulator.exe`) and any overrides (Link, OpenXR runtime, game settings).
3. When that game starts, the profile auto-applies and you get a tray notification.
4. When the game exits, **global defaults are restored** automatically.

Export/import profiles and full settings from **Advanced**.

### 5. Quest Link / headset tweaks

- **Quest Link** registry settings usually need a Link reconnect or **OVRService** restart — use **Service & Startup** or the tray menu.
- **Headset (ADB)** tweaks (CPU/GPU, refresh rate, texture size, etc.) apply when a trusted Quest connects. Enable **Developer Mode** on the headset and approve USB debugging once. These settings do not survive a headset reboot; the tray re-applies them on connect.
- Only **real VR headsets** are trusted. Phones, tablets, and Android emulators are ignored.

### 6. Tray menu quick actions

**Right-click** the tray icon for:

- Game Settings (SS, ASW, FOV, …)
- Start / stop / restart **OVRService**
- Apply or edit **profiles**
- **Open Settings**, **Info**, **Donate**, **Exit**

### 7. Tips

- **Themes:** Pure Black (default), Dark, or Light — change on the **Tray Tool** page.
- **Start minimized** and **minimize-on-close** keep the app in the tray instead of the taskbar.
- Newer Meta runtimes may reject some `server:` ASW commands; check **Log** if something does not apply.
- Pixel density changes often need a **new VR session** to take effect.

---

## Requirements

- Windows 10 / 11 (64-bit)
- [Meta Quest PC app](https://www.meta.com/quest/setup-link/) and/or [SteamVR](https://store.steampowered.com/app/250820/SteamVR/) for the features you use
- For **Headset (ADB)**: Quest **Developer Mode** + one-time USB debugging approval (ADB is bundled — no Android Studio required)

---

## What works now (v1.0.0)

- Runs in the notification area with a right-click popup menu
- Opens a modern **OTT-style sidebar shell** (Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link)
- Detects the Oculus PC install and `OVRService` status
- Start / stop / restart the Oculus runtime service, with optional start/stop automation
- Saves settings to `%AppData%\MetaQuestTrayTool\settings.json`
- **Hands-free Administrator mode** (on by default): one Windows approval, then the tray starts itself at logon already elevated so OpenXR, OVRService, and profiles apply while you are in VR — no UAC in-headset
- Optional **Restart as Administrator** if that first approval was skipped
- Start minimized, minimize-on-close, hide from Alt+Tab
- **Themes:** Pure Black (default), Dark, and Light — change anytime on the Tray Tool page
- Tray **Game Settings**: Super Sampling, ASW (including 45/30/18), FOV, Adaptive GPU, mip layer flags, FOV stencil, and Visual HUD via `OculusDebugToolCLI.exe`
- Startup probe of connected headset serials through `server:EnumHmd`
- Create / edit / delete / apply **profiles** from the tray or shell
- Personal profiles can override **Link sharpening / bitrate / encode width** (inherit keeps global Quest Link settings; restored when the game exits)
- **OpenXR runtime switch**: Meta / Oculus vs SteamVR via `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` — global preferred + per-game profile override (may prompt for Administrator)
- Auto-apply a profile when the matching process starts, then restore defaults when it exits
- **Quest Link / Air Link**: bitrate, encode width, HEVC, sliced encoding, sharpening via `HKCU\Software\Oculus\RemoteHeadset`
- **Audio switching**: auto-switch when Link audio is active, restore desktop devices when Link drops
- **Power plan**: auto-switch plans, USB selective-suspend off, restart service after sleep
- **Steam / Meta library picker** with cover art (Steam librarycache / CDN, Meta StoreAssets), plus separate **global defaults**
- **Headset (ADB)**: SideQuest-style CPU/GPU, texture size, refresh rate, FFR, chroma, and capture props — auto-applied when the Quest connects (they do not survive reboot)
- **Trusted headset**: first connected **VR headset** serial is remembered; a different VR serial is blocked. Phones, tablets, and Android emulators are ignored and never receive ADB commands or auto-apply
- **Profiles**: auto-apply on game launch with tray notification; restore global when the game exits. Stored in `profiles.json` (export/import still works). Built-in global + game presets (MSFS 2024, Beat Saber, etc.)
- **Backup**: export / import settings from Advanced
- **Info** page: live OpenXR (Meta vs SteamVR), OVRService, and detailed headset identity
- **Donate** button (sidebar, About, tray) opens the [PayPal donate page](https://www.paypal.com/donate/?business=X76ZW4RHA6T9C&no_recurring=0&item_name=Eliminater74+builds+Meta+Quest+Tray+Tool+%E2%80%94+free+Quest+Link+%26+SteamVR+tray+settings.+Your+gift+keeps+it+going.&currency_code=USD)

Not yet: hotkeys, voice commands, Oculus Homeless, Permanent AirLink, in-app updates.

See [ROADMAP.md](ROADMAP.md) for remaining polish items.

---

## Project docs

| File | Use when you need to… |
| --- | --- |
| [ROADMAP.md](ROADMAP.md) | See the planned phases and why they exist |
| [TODO.md](TODO.md) | Check what is done vs next, checkbox style |
| [REDDIT.md](REDDIT.md) | Copy-paste Reddit announcement (title + body) |
| [installer/README.md](installer/README.md) | Build the Windows Setup.exe locally |
| [docs/ODT-REGISTRY.md](docs/ODT-REGISTRY.md) | ODT registry keys vs CLI commands (from Meta binaries) |

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
