# Troubleshooting

## Check the log first

**Log Window** (or `%AppData%\MetaQuestTrayTool\`). Look for profile apply, Link writes, voice recognition, Dash → SteamVR, and “system resumed”.

![Log](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/06-log-window.png)

## SteamVR starts when I am not in Link

Fixed in v1.1.2+: auto-start needs a **live** Meta Link stream (not Wi‑Fi DeviceCache / EnumHmd). Confirm PreventDashLaunch is what you want. Log will show `Meta Link streaming suspected (1/2)` while confirming.

## “Link session ended” / Recover PCVR toast when the PC wakes

DeviceCache and Oculus virtual audio often blip on resume. **v1.1.3** ignores connect/disconnect edges for ~20 seconds after wake and only toasts a real stream drop.

**Restart OVRService after sleep** (on by default) *will* kill an actual Link session — turn that off on Power / Service & Startup if you sleep mid-PCVR.

## Cannot click the tray in SteamVR

Expected when the tray is **elevated**. Use [[HotKeys-Voice-and-Announcements]].

## Exit leaves a hidden helper or the next launch is silent

v1.1.24 fixed the main exit/helper shutdown path. v1.1.25 adds **Advanced → Repair stuck helper** and **Copy diagnostics**. Use Repair if Task Manager shows a leftover **Meta Quest Tray Tool** background process after Exit; it asks the helper to quit first, then terminates only a validated same-exe helper if it is still stuck.

If the problem returns, use **Copy diagnostics** or **Info → Copy system info** and check the `App internals` lines for main PID, helper pipe PID, recorded helper state, same-exe child helpers, and settings/profile paths.

## OpenXR / OVRService / PreventDashLaunch does nothing

Run **as Administrator**. Use Restart as Administrator or the logon elevation task.

## ASW / SS does not stick

- New Meta runtimes may reject `server:` ASW — Log will say so
- Pixel density often needs a **new VR session**
- Under Steam Link / VD, ODT is skipped on purpose (no Meta command channel). Use Quest Link / Air Link for SS / ASW, or ADB only for headset CPU/GPU — see [[Quest-Link-vs-Steam-Link]]

## Link bitrate did not change

Reconnect Link or **Restart OVRService**. Confirm you are on Meta Link, not Steam Link/VD.

## Headset ADB not connecting

Developer Mode + USB debugging **or** Wireless Pair. Same Wi‑Fi for wireless. Phones/emulators never get Quest tweaks. Props reset on Quest reboot — leave apply-on-connect on.

If a **phone** or **TV** keeps disappearing from `adb devices`, **VR headsets only** is on (default). Uncheck it on the Headset page or tray → **Headset (ADB)** to leave other wireless ADB devices connected. Or use **Pause ADB** (until resume / 2 hours) so the watcher fully stops without quitting the tray.

**SideQuest on the headset:** if SideQuest is running in VR, it may open an ADB port (often 5555). Use that LAN IP:port with **Connect** on the Headset page — see [[Headset-ADB]].

## Quest screenshot fails

Screenshot capture needs a trusted, ready Quest over ADB. Check Headset status for unauthorized/disconnected/ignored devices, then try **Headset (ADB) → Take screenshot** again. PNG files save to `%AppData%\MetaQuestTrayTool\screenshots\`; corrupt output is deleted instead of reported as success.

## Meta will not start after Manual-at-boot

Start **OVRService** in the tray, then Open Meta Horizon Link. Or enable **Start Oculus service when tool starts**. See [[Service-and-Startup]].

## Audio stays on the headset after PCVR

Enable the audio switcher and set fallback devices. Session-end restore keys off a **live** stream ending, not “headset still Windows default”.

## Voice not recognized

PTT (Ctrl+Shift+V), matching Windows speech language, raise min confidence, check Log. Spell **A S W** / **steam v r**.

## Recover PCVR

Info page or tray: restarts OVRService, re-applies Link + globals + audio. Voice: **recover PCVR**.

## SmartScreen / unsigned installer

Setup.exe is not Authenticode-signed yet. Windows may warn; that is a known limit until signing budget exists.
