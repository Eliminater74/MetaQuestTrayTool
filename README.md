# Meta Quest Tray Tool

Windows tray utility for Meta Quest / Oculus Link. This is a **new C# project**, not a continuation of the unfinished conversion of [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/).

Open `MetaQuestTrayTool.sln` in Visual Studio Community and press F5.

## Project docs

| File | Use when you need to… |
| --- | --- |
| [ROADMAP.md](ROADMAP.md) | See the planned phases and why they exist |
| [TODO.md](TODO.md) | Check what is done vs next, checkbox style |

Update both files whenever a phase ships so they stay useful after a break.

## What works now (v0.6)

- Runs in the notification area with a right-click popup menu
- Opens a modern **OTT-style sidebar shell** (Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link)
- Detects the Oculus PC install and `OVRService` status
- Start / stop / restart the Oculus runtime service, with optional start/stop automation
- Saves settings to `%AppData%\MetaQuestTrayTool\settings.json`
- Optional Start with Windows, start minimized, minimize-on-close, hide from Alt+Tab
- **Themes:** Pure Black (default), Dark, and Light — change anytime on the Tray Tool page
- Tray **Game Settings**: Super Sampling and ASW, applied through `OculusDebugToolCLI.exe`
- Create / edit / delete / apply **profiles** from the tray or shell
- Auto-apply a profile when the matching process starts, then restore defaults when it exits
- **Quest Link / Air Link**: bitrate, encode width, HEVC, sliced encoding, sharpening via `HKCU\Software\Oculus\RemoteHeadset`
- **Audio switching**: auto-switch when Link audio is active, restore desktop devices when Link drops
- **Power plan**: auto-switch plans, USB selective-suspend off, restart service after sleep
- **Steam / Meta library picker** for personal profiles, plus separate **global defaults**

Not yet: hotkeys, voice commands, Oculus Homeless, Permanent AirLink, in-app updates.

See [ROADMAP.md](ROADMAP.md) for remaining polish items.

Newer Meta runtimes sometimes reject `server:` ASW commands. The log will say so if that happens. Pixel density still needs a new VR session to take effect. Link registry changes usually need a Link reconnect or `OVRService` restart.

## Requirements

- Windows 10 / 11
- [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) with the **.NET desktop development** workload
- .NET 8 SDK (included with a current VS Community install)

No extra Visual Studio extensions are required.

## Build

**Visual Studio**

1. Open `MetaQuestTrayTool.sln`
2. Configuration: `Debug` or `Release`
3. Build → Build Solution, or press F5

**Command line**

```powershell
dotnet build .\MetaQuestTrayTool.sln -c Debug
dotnet run --project .\src\MetaQuestTrayTool\MetaQuestTrayTool.csproj
```

The app can start minimized to the tray (default). Left-click the headset icon or choose **Open Settings** to open the sidebar shell. If Windows hides the icon, open the overflow chevron.

Starting or stopping `OVRService` may require running Visual Studio / the exe as Administrator. The app itself starts unelevated so it can live in the tray without a UAC prompt.

## Inspired by

- [Oculus Tray Tool](https://www.guru3d.com/download/oculus-traytool-download/) by ApollyonVR
- [Oculus_Tray_Manager](https://github.com/DevOculus-Meta-Quest/Oculus_Tray_Manager) (earlier C# conversion of OTT)
- [MetaQuestTrayManager](https://github.com/DevOculus-Meta-Quest/MetaQuestTrayManager) (earlier WPF rewrite)
- [Oculus VR Dash Manager](https://github.com/DevOculus-Meta-Quest/Oculus-VR-Dash-Manager)

This repo is a clean start. The old conversion work stays in those repositories.
