# FAQ

## Is this Oculus Tray Tool?

No. Inspired by ApollyonVR’s OTT, but a **new C# app** — not a VB.NET decompile or a continuation of the unfinished conversion repos.

## Do I need Meta installed for Steam Link only?

No. Steam Link is Valve’s path. Uninstall Meta Horizon Link if you never use Quest Link / Air Link. This tool’s Link / OVRService / PreventDashLaunch features will not apply.

## Why does Horizon Link start with Windows?

`OVRService` is a Windows service set to **Automatic**. Use [[Service-and-Startup]] Manual-at-boot.

## Does PreventDashLaunch kill Meta processes?

No. Registry only. Killing Client/Dash caused freezes and respawns.

## Can I revive Oculus Home?

No. Meta removed it. Use Dash → SteamVR and optional SteamVR Home.

## Where are settings?

`%AppData%\MetaQuestTrayTool\settings.json` and `profiles.json`. Backup from **Advanced**.

## macOS / Linux?

Not planned. Windows 10/11 only.

## Per-game hotkeys?

Not yet — HotKeys are global. Profiles still auto-apply per process.

## License / source

See the [GitHub repository](https://github.com/Eliminater74/MetaQuestTrayTool). Issues and PRs welcome.

## Related

- [[Getting-Started]]
- [[Troubleshooting]]
