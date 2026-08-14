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

## OpenXR / OVRService / PreventDashLaunch does nothing

Run **as Administrator**. Use Restart as Administrator or the logon elevation task.

## ASW / SS does not stick

- New Meta runtimes may reject `server:` ASW — Log will say so
- Pixel density often needs a **new VR session**
- Under Steam Link / VD, ODT is skipped on purpose

## Link bitrate did not change

Reconnect Link or **Restart OVRService**. Confirm you are on Meta Link, not Steam Link/VD.

## Headset ADB not connecting

Developer Mode + USB debugging **or** Wireless Pair. Same Wi‑Fi for wireless. Phones/emulators are ignored. Props reset on Quest reboot — leave apply-on-connect on.

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
