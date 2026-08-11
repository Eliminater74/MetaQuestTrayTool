# Meta Quest Tray Tool

Windows tray utility for Meta Quest / Oculus Link. This is a **new C# project**, not a continuation of the unfinished conversion of [Oculus Tray Tool](https://techtipsvr.com/oculus-tray-tool/).

Open `MetaQuestTrayTool.sln` in Visual Studio Community and press F5.

## What this first build does

- Runs in the notification area with a right-click popup menu
- Opens a small dashboard on left-click / Open Dashboard
- Detects the Oculus PC install and `OVRService` status
- Start / stop / restart the Oculus runtime service
- Saves settings to `%AppData%\MetaQuestTrayTool\settings.json`
- Optional Start with Windows (current user Run key)

Game profiles, supersampling, ASW, Quest Link / Air Link, audio switching, and power-plan tweaks are on the tray menu as placeholders for the next pass.

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

The app has no main window on startup. Look for the headset icon in the system tray. If Windows hides it, open the overflow chevron.

Starting or stopping `OVRService` may require running Visual Studio / the exe as Administrator. The app itself starts unelevated so it can live in the tray without a UAC prompt.

## Inspired by

- [Oculus Tray Tool](https://www.guru3d.com/download/oculus-traytool-download/) by ApollyonVR
- [Oculus_Tray_Manager](https://github.com/DevOculus-Meta-Quest/Oculus_Tray_Manager) (earlier C# conversion of OTT)
- [MetaQuestTrayManager](https://github.com/DevOculus-Meta-Quest/MetaQuestTrayManager) (earlier WPF rewrite)
- [Oculus VR Dash Manager](https://github.com/DevOculus-Meta-Quest/Oculus-VR-Dash-Manager)

This repo is a clean start. The old conversion work stays in those repositories.
