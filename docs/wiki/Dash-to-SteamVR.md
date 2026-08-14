# Dash → SteamVR (PreventDashLaunch)

Play **SteamVR games over Quest Link / Air Link** without Meta Dash owning the session.

This follows the [OculusKiller](https://github.com/DevOculus-Meta-Quest/OculusKiller) **registry** approach. The tray does **not** kill `OculusClient`, `OculusDash`, or replace `OculusDash.exe` on disk.

## How to enable

**Service & Startup** (or Quest Link / tray):

1. Turn on **PreventDashLaunch**.
2. Apply (elevated). Registry:

   `HKLM\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus\Config\PreventDashLaunch` = `1`

3. Optional: **Also switch OpenXR to SteamVR** when starting SteamVR over Link.
4. Optional: **Restart OVRService when SteamVR exits** so Link can drop and Quest Home can return.
5. Optional: **CoreChannel** `LIVE` / `PublicTest` / `NO_UPDATES`.

Connect with **Meta Link / Air Link**. After a confirmed live stream (not Wi‑Fi auto-connect), the tray auto-starts SteamVR.

Manual: tray / **Ctrl+Num 0** / voice **dash to steam v r**.

## What “live stream” means

Auto SteamVR starts only when Link is actually streaming, for example:

- Air Link with headset audio routed + operable DeviceCache
- Wired Link with USB + connected cache
- Remote desktop (`rdConnectionState`) connected

EnumHmd-only or sticky DeviceCache **will not** launch SteamVR (that used to fire while the headset was merely on Wi‑Fi).

## SteamVR Home

Meta’s old Oculus Home / Homeless is gone. **Open SteamVR Home** (`steamtours`) is on-demand: Service & Startup, tray, hotkey, or voice **steam v r home**. SteamVR should be running first.

## Related

- [[Service-and-Startup]]
- [[Quest-Link]]
- [[HotKeys-Voice-and-Announcements]]
