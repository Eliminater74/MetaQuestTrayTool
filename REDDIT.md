# Reddit post — Meta Quest Tray Tool

Copy everything under **TITLE** and **BODY** into a new Reddit post (or edit your existing one).
Suggested subs: `r/OculusQuest`, `r/virtualreality`, `r/SteamVR`, `r/MetaQuestVR` (check each sub’s self-promo rules).

**Latest public installer:** [v1.0.4](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)  

---

## TITLE

```
[PC] Meta Quest Tray Tool v1.0.4 — free modern OTT-style tray app for Quest Link / SteamVR / Steam Link / VD
```

---

## BODY

```markdown
**Meta Quest Tray Tool** is a free Windows system-tray app for Meta Quest PCVR (Link / Air Link) and SteamVR OpenXR.

It’s a **brand-new C# app**, not a decompile and not a continuation of older unfinished ports. Goal: keep the Oculus Tray Tool workflow alive on today’s Meta stack.

**Author:** Eliminater74  
**Status:** public releases on GitHub (**v1.0.4** latest) — Windows 10/11, self-contained Setup.exe (no separate .NET install)

**Download:** https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest  
**Repo / docs / screenshots / short demo:** https://github.com/Eliminater74/MetaQuestTrayTool

---

### Inspired by (please give credit)

Huge respect to **ApollyonVR** and the original **Oculus Tray Tool (OTT)** — the classic tray app that made SS / ASW / profiles / Link tweaks easy for years.

- Original OTT: https://techtipsvr.com/oculus-tray-tool/

Meta Quest Tray Tool is **inspired by OTT’s ideas and layout**, rebuilt from scratch for current Meta Quest Link, SteamVR OpenXR, and modern Windows. It is **not** affiliated with ApollyonVR or Meta. If you loved OTT, this is meant to fill that gap — not replace the credit OTT earned.

---

### What it does (short)

Lives in the notification area. **Global VR defaults** stay applied until a game with a **personal profile** launches → that profile’s tweaks apply → when the game exits, **global defaults come back**, with a tray notification.

You can also push SideQuest-style **ADB headset** tweaks when a real Quest connects, switch **OpenXR** Meta vs SteamVR, manage Link bitrate / sharpening, use **hotkeys / voice**, and **update in-app** from GitHub — without digging through registries.

---

### What works now (v1.0.4)

**Tray + shell**
- Notification-area host with themed menu (Pure Black / Dark / Light)
- OTT-style sidebar: Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link, Headset, Profiles, Info / About
- Close-to-tray, start minimized, hide from Alt+Tab, single-instance
- Screenshots + short demo video in the repo README

**Oculus / Meta PC runtime**
- Detect Meta/Oculus install + `OVRService`
- Start / stop / restart service (optional automation on tool start/exit / after sleep)
- **Open Meta Horizon Link** from tray, hotkey (**Ctrl+Numpad 9**), and voice
- Hands-free elevated start (one UAC, then silent elevated at logon) so OpenXR / service / profiles don’t pop UAC while you’re in the headset

**Game settings (OculusDebugToolCLI)**
- Super Sampling, ASW (Auto / Off / 45 / 30 / 18), FOV, Adaptive GPU, mip flags, FOV stencil, Visual HUD
- Custom extra CLI lines on global + personal profiles

**Profiles**
- Personal per-process profiles (Steam / Meta library picker with cover art, or custom)
- Auto-apply on game launch → restore **global** when the game exits (tray balloons) — including Link + OpenXR restore
- Built-in **global presets** (Balanced, Performance, Quality, Sim, Competitive)
- Built-in **PCVR game presets** (MSFS 2024, Beat Saber, Half-Life: Alyx, DCS, iRacing, and more)
- Profiles in `%AppData%\MetaQuestTrayTool\profiles.json`
- Settings export/import backup from Advanced

**Quest Link / Air Link**
- Bitrate, encode width, HEVC, sliced encoding, sharpening, distortion curve, DBR / DBR max / offset, Mobile ASW (`HKCU\Software\Oculus\RemoteHeadset`)
- Full Link fields in the tray Link window + shell Quest Link page
- Per-profile Link overrides (inherit = keep global)
- ODT registry reference in the repo (`docs/ODT-REGISTRY.md`)

**HotKeys + voice (preview)**
- Global hotkeys (default **Ctrl+Numpad 1–8**, plus Open Link on **Ctrl+Numpad 9**): ASW, SS cycle, apply global, restart OVRService, perf HUD, open Meta Horizon Link
- Configure UI (themed lists); conflicts / import reload handled
- Voice commands via Windows speech — push-to-talk (**Ctrl+Shift+V** default), optional always-on
- Full phrase list: `docs/VOICE-AND-HOTKEYS.md`

**In-app updates**
- Checks GitHub latest release (`v*`), downloads Setup.exe, closes the app, installs over the current copy
- **Check on start** (default on), **Check now** (Tray Tool / Advanced / tray menu)
- **Also check while running** on a schedule you choose: off / daily / every 3 days / weekly (default) / every 2 weeks / monthly

**OpenXR**
- Switch ActiveRuntime between **Meta / Oculus** and **SteamVR** (global + per-profile)
- Restore preferred / previous runtime after a profile exits

**Audio / power**
- Auto-switch to headset audio when Link is actually active (Windows default output / headset endpoint) — **does not steal speakers** just because Meta’s virtual audio device is installed
- Restore desktop devices when Link drops; startup heal if headset was left as default with no HMD
- Power plan switch, USB selective suspend option, restart `OVRService` after sleep

**Headset ADB (standalone Quest tweaks)**
- Bundled Google platform-tools ADB
- CPU/GPU level, texture size, refresh, FFR, chroma, capture settings
- Auto-apply when a **real VR headset** connects (Quest 2/3/3S/Pro, plus allowlist for Galaxy XR / HTC Vive standalone / Pico / Steam Frame)
- Phones, tablets, and **Android emulators are ignored** — they never get commands or trust
- Trusted serial (first real headset remembered; other VR serials blocked)
- Paste text / proximity / guardian helpers
- Custom ADB lines on global + personal profiles

**Log / Info / Donate**
- Log page: clear log, color-coded ERROR / WARN / INFO
- Info page: live OpenXR, OVRService, headset identity
- **PCVR connection probe**: Meta Air Link vs wired (`%LocalAppData%\Oculus\DeviceCache.json` → `isUsingAirLink`), Steam Link / SteamVR, Virtual Desktop
- Donate button (PayPal) if you want to support development
- Root LICENSE in the repo

---

### What does **not** work yet / known limits

**Not implemented yet**
- Authenticode / code-signed Setup.exe (SmartScreen may warn until budget allows a cert)
- Oculus Homeless / some classic OTT extras
- Permanent AirLink
- Wireless ADB pairing UI (USB ADB works)
- Launch games directly from the library picker
- Profile process ignore-list
- Voice polish: custom phrases, mic picker, always-on tuning
- Separate communications (vs multimedia) audio device pickers in the UI
- Full Dash Manager–style dash customizer (out of scope)

**Runtime caveats (Meta / Windows)**
- Newer Meta runtimes sometimes **reject** `server:` ASW CLI commands — the log will say so
- Pixel density / SS often needs a **new VR session** to stick
- Link registry changes usually need a **Link reconnect** or `OVRService` restart
- ADB `debug.oculus.*` props **reset on Quest reboot** — leave apply-on-connect on
- Admin rights help for OpenXR HKLM + some service control (hands-free elevated mode is built in)
- Windows-only

---

### Planned next

- Authenticode signing for Setup.exe (when budget allows)
- Wireless ADB pairing UI
- Profile ignore-list
- Library “launch game”
- Better Link detection verification / DBR verification
- Voice polish (custom phrases, mic picker)
- More built-in presets as people request process names

Feedback and bug reports welcome — especially “profile didn’t apply for game X (process name: …)” or audio switch edge cases.

---

### Requirements

- Windows 10 / 11 (64-bit)
- Meta Quest PC software (for Link / Debug Tool CLI) and/or SteamVR (for Steam OpenXR)
- Quest Developer Mode + USB debugging approval for ADB headset features (ADB is bundled)

---

### How to get it

**Installer (recommended):**  
https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest  

Run `MetaQuestTrayTool-Setup-*.exe` — Program Files, Start Menu, Uninstall. Self-contained (includes .NET 8). Settings under `%AppData%\MetaQuestTrayTool\` survive uninstall.

**From source:** open `MetaQuestTrayTool.sln` → F5, or `dotnet build` / `.\scripts\build-installer.ps1`.

---

### Support the project (optional)

Donate (PayPal):  
https://www.paypal.com/donate/?business=X76ZW4RHA6T9C&no_recurring=0&item_name=Eliminater74+builds+Meta+Quest+Tray+Tool+%E2%80%94+free+Quest+Link+%26+SteamVR+tray+settings.+Your+gift+keeps+it+going.&currency_code=USD

Free tool either way. Donations just help keep development going.

---

### Credits again

- **ApollyonVR** — Oculus Tray Tool (the inspiration)
- Meta / Oculus Debug Tool CLI & Link stack
- SideQuest-style ADB property ideas for standalone Quest tweaks
- Community PCVR presets will grow from feedback

Not affiliated with Meta, ApollyonVR, Valve, Samsung, HTC, or Pico.

Thanks for reading — happy to take feature requests and “does this work on Quest 3S / Steam Link?” reports.
```

---

## Optional short comment (first reply)

```text
TL;DR: free modern OTT-inspired tray app (v1.0.4) — global defaults + per-game auto apply/restore, Link + OpenXR switch, Air Link/wired/Steam Link/VD detection, fixed audio auto-switch, hotkeys/voice, Open Meta Horizon Link, scheduled in-app updates, Quest ADB (real headsets only). Inspired by ApollyonVR’s Oculus Tray Tool; clean C# rewrite, not a decompile. https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest
```
