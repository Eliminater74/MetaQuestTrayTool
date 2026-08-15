# Getting started

## Install

1. Download **[MetaQuestTrayTool-Setup-*.exe](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)** from the latest GitHub Release.
2. Run the installer. It is self-contained (includes .NET 8).
3. Launch **Meta Quest Tray Tool** from the Start Menu, or let it start with Windows if you enabled that.

Settings are stored in `%AppData%\MetaQuestTrayTool\` (`settings.json`, `profiles.json`, logs). Uninstall does **not** delete them.

## Find the tray icon

The app lives in the **notification area** (system tray), not as a full-time taskbar window.

- If you do not see a headset icon, click the **^** overflow chevron near the clock and drag the icon onto the visible tray.
- **Left-click** the icon → settings shell (Status page).
- **Right-click** → quick actions (Link, SteamVR, Recover PCVR, OVRService, updates, Exit).

## Administrator mode (recommended)

OpenXR switching, `OVRService` control, and some profile/registry writes need **Administrator**.

1. On first run, approve the elevation prompt if offered.
2. Or use tray **Restart as Administrator…** / **Service & Startup → Run with Administrator rights at logon**.

After one Windows approval, a logon scheduled task starts the tray elevated so you do **not** get UAC while wearing the headset.

**Trade-off:** SteamVR (including Air Link → SteamVR) **cannot click** an elevated tray menu. Set [[HotKeys-Voice-and-Announcements]] *before* you put the headset on.

## First-run checklist

1. **Tray Tool** — Start with Windows, elevation, audio switcher, notifications, theme.
2. **Quest Link** — bitrate / encode width / HEVC / sharpening (needs reconnect or OVRService restart).
3. **Game Settings** — Super Sampling, ASW, FOV, OpenXR preferred runtime.
4. **Service & Startup** — OVRService start/stop; optional **Manual-at-boot**; PreventDashLaunch if you want SteamVR over Link.
5. **Headset** — Developer Mode + USB or Wireless ADB if you want CPU/GPU/refresh props.
6. **Profiles** — optional per-game overrides.

## Steam Link vs Quest Link (read this first)

**Read [[Quest-Link-vs-Steam-Link]]** — two pipes, not one.

- **Full tweaks** (SS / ASW / FOV / Link bitrate) need **Quest Link or Air Link**. The Steam Link *app* cannot receive those commands. Headset CPU/GPU still needs [[Headset-ADB]] on either path.
- **SteamVR games + those tweaks:** Quest Link / Air Link + [[Dash-to-SteamVR|PreventDashLaunch]] — do **not** start the Steam Link app.
- **Steam Link app only:** Meta optional. This tray will skip Game Settings / Quest Link writes. Quality lives in Steam. ADB is the only way this tool can tweak the Quest itself.

If you *do* use Quest Link, Meta often opens at Windows sign-in because **OVRService is Automatic** (it is not in Settings → Startup apps). See [[Service-and-Startup]].

## Updates

**Tray Tool → Check for updates on start** (on by default), a running schedule (weekly default), or **Check now** / tray **Check for updates…**.

The updater downloads Setup.exe from GitHub, stops ADB so bundled platform-tools can be replaced, then installs over the current copy.

## Next

- [[Shell-and-Tray]] — every sidebar page
- [[Game-Settings-and-Profiles]]
- [[Troubleshooting]]
