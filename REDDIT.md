# Reddit post — Meta Quest Tray Tool

Copy everything under **TITLE** and **BODY** into a new Reddit post (or edit your existing one).
Suggested subs: `r/OculusQuest`, `r/virtualreality`, `r/SteamVR`, `r/MetaQuestVR` (check each sub’s self-promo rules).

**Latest public installer:** [v1.1.18](https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest)

---

## TITLE

```
[PC] Meta Quest Tray Tool v1.1.18 — free modern OTT-style tray app for Quest Link / SteamVR (neon icon + settings survive power loss)
```

---

## BODY

```markdown
**Meta Quest Tray Tool** is a free Windows system-tray app for Meta Quest PCVR (Link / Air Link) and SteamVR OpenXR.

It’s a **brand-new C# app**, not a decompile and not a continuation of older unfinished ports. Goal: keep the Oculus Tray Tool workflow alive on today’s Meta stack — Steam-first PCVR friendly.

**Author:** Eliminater74  
**Status:** public releases on GitHub (**v1.1.18** latest) — Windows 10/11, self-contained Setup.exe (no separate .NET install)

**Download:** https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest  
**Changelog:** https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/CHANGELOG.md  
**Wiki (install, pages, Dash→SteamVR, voice, troubleshooting):** https://github.com/Eliminater74/MetaQuestTrayTool/wiki  
**Repo / screenshots / short demo:** https://github.com/Eliminater74/MetaQuestTrayTool

---

### Inspired by (please give credit)

Huge respect to **ApollyonVR** and the original **Oculus Tray Tool (OTT)** — the classic tray app that made SS / ASW / profiles / Link tweaks easy for years.

- Original OTT: https://techtipsvr.com/oculus-tray-tool/

Meta Quest Tray Tool is **inspired by OTT’s ideas and layout**, rebuilt from scratch for current Meta Quest Link, SteamVR OpenXR, and modern Windows. It is **not** affiliated with ApollyonVR or Meta. If you loved OTT, this is meant to fill that gap — not replace the credit OTT earned.

---

### What it does (short)

Lives in the notification area. Opens an OTT-style sidebar on **Status** by default.

**Global VR defaults** stay applied until a game with a **personal profile** launches → that profile’s tweaks apply → when the game exits, **global defaults come back**, with a tray notification.

You can also push SideQuest-style **ADB headset** tweaks (USB or Wireless Pair), switch **OpenXR** Meta vs SteamVR, manage Link bitrate / sharpening, run **Dash → SteamVR** over Air Link, open **SteamVR Home** on demand, use **hotkeys / voice**, and **update in-app** from GitHub — without digging through registries.

**Important for SteamVR users:** when the tray runs elevated (recommended), **SteamVR cannot click that tray menu**. Mid-session control is HotKeys, voice, and automation.

---

### What works now (v1.1.18)

**Tray + shell**
- Notification-area host with themes (Pure Black / Dark / Light)
- Sidebar: **Status**, Game Settings, Tray Tool, Power Options, Service & Startup, Log, Advanced, Quest Link, Headset, **VR Tools**, Info / About
- Hover tooltips; close-to-tray; start minimized; hide from Alt+Tab; single-instance
- Hands-free elevated start (one UAC, then silent elevated at logon)
- Refreshed neon VR headset icon/logo (tray, About, settings sidebar)
- Durable `settings.json` / `profiles.json` (flush + `.bak`/`.bak2` restore after power loss)
- Fresh **v1.1.18 screenshots + walkthrough video** in the repo README; **full gallery + user guide:** https://github.com/Eliminater74/MetaQuestTrayTool/wiki/Screenshots-and-Videos

**Status & SteamVR**
- Live Status chips (PCVR Ready, SteamVR install/running/Stable|Beta, OpenXR, OVRService, session type, ADB, battery/Wi‑Fi, profile, HotKeys/Voice, Dash→SteamVR, GPU, audio)
- SteamVR install detect + Install SteamVR action
- PCVR Ready checklist (Info) with fix actions
- Recover PCVR after Link / Steam / VD drop
- Session probe: Meta Air Link vs wired, Steam Link / SteamVR, Virtual Desktop

**Oculus / Meta PC runtime**
- Detect Meta/Oculus install + `OVRService` (start / stop / restart + automation)
- **Open Meta Horizon Link** — tray, hotkey (**Ctrl+Num 9**), voice
- **Open Oculus Debug Tool GUI** (`OculusDebugTool.exe`) — classic OTT shortcut — tray / shell / hotkey / voice
- **OVRService Manual-at-boot** (Service & Startup) — optional Manual startup so Meta Horizon Link does not auto-open at Windows sign-in; documented what it does/does not do; re-applies after Meta updates
- **PreventDashLaunch → SteamVR over Link** (OculusKiller registry — no Meta process killing): auto SteamVR on Link — tray, Quest Link, Service & Startup, **Ctrl+Num 0**, voice (“dash to steam v r”); **stop OVRService 10s** when SteamVR exits (headset speaks the wait, then Quest Home)
- **PreventDashLaunch** + **CoreChannel** (`LIVE` / `PublicTest` / `NO_UPDATES`)
- **SteamVR Home** (`steamtours`) on demand — Service & Startup / tray / hotkey / voice  
  (Meta’s old Oculus Home / Homeless is gone — not coming back)

**Game settings (OculusDebugToolCLI)**
- Super Sampling, ASW (Auto / Off / 45 / 30 / 18), FOV, Adaptive GPU, mip flags, FOV stencil, Visual HUD
- Tray **Cycle Perf HUD**; optional close overlays on Link connect
- Custom extra CLI / ADB lines on global + personal profiles

**Profiles**
- Personal per-process profiles (Steam / Meta library picker with cover art, or custom)
- Auto-apply on launch → restore **global** on exit (including Link + OpenXR)
- **Launch** from library / profile (`steam://run/{appId}`)
- Profile **ignore list** + **save last-good** mid-session
- Built-in global presets + PCVR game presets (MSFS 2024, Beat Saber, HL:Alyx, DCS, iRacing, …)
- `%AppData%\MetaQuestTrayTool\profiles.json` + Advanced settings backup

**Quest Link / Air Link**
- Bitrate, encode width, HEVC, sliced encoding, sharpening, distortion, DBR / max / offset, Mobile ASW
- Presets on the Quest Link page; per-profile Link overrides (inherit = keep global)
- ODT registry reference: `docs/ODT-REGISTRY.md`

**HotKeys + voice**
- Defaults **Ctrl+Numpad 0–9** (ASW, SS cycle, apply global, restart OVRService, Perf HUD, Meta Link, Dash→SteamVR)
- Assign **Open Debug Tool** / **Open SteamVR Home** / recover / OpenXR / overlays / GPU preset in Configure
- Voice: Windows speech — push-to-talk (**Ctrl+Shift+V** default) or always-on; mic picker; min confidence; custom phrases
- New phrases: recover PCVR, desktop/VR audio, OpenXR meta/steam, close overlays, GPU preset
- **Headset announcements** (Tray Tool): spoken status in the Quest — connect waits for Link audio then says **“Connected. Air Link. Now starting SteamVR.”** (PreventDash) or **“Meta Horizon will load.”** (Dash path); SteamVR exit wait; profile apply; launch
- Full list: wiki → HotKeys, voice, and headset announcements (`docs/VOICE-AND-HOTKEYS.md` in the repo)

**OpenXR / audio / power**
- Switch ActiveRuntime Meta vs SteamVR (global + per-profile)
- Steam Link assist (force SteamVR OpenXR while Steam Link / SteamVR is active, restore after)
- Under Steam Link / VD, Meta Link registry + ODT are gated; ADB / OpenXR / power / audio still apply
- Audio auto-switch when Link audio is active (doesn’t steal speakers just because Meta virtual audio is installed)
- Separate **communications** playback/recording pickers
- Power plan switch, USB selective suspend, restart `OVRService` after sleep

**Headset ADB**
- Bundled Google platform-tools
- CPU/GPU, texture size, refresh, FFR, chroma, capture; paste / proximity / guardian helpers
- Auto-apply on connect (resets on Quest reboot)
- **Wireless ADB**: host, connect port, **Pair** (pairing port + code), Connect / Disconnect, Enable tcpip, auto-reconnect
- **VR headsets only** (default on; tray + Headset page): drop phone/TV wireless ADB — uncheck to allow any device
- **Pause ADB** (tray): until you resume, or for 2 hours — other gadgets can use ADB without quitting
- Battery / charge / Wi‑Fi via ADB

**Updates / tools / donate**
- In-app updates from GitHub `v*` (on start, schedule, or Check now); ADB stopped before Setup
- **VR Tools** page + tray: curated third-party links
- Donate (PayPal) optional

---

### What does **not** work yet / known limits

**Out of scope / not planned**
- Authenticode / code-signed Setup.exe (SmartScreen may warn until budget allows)
- Permanent AirLink (Meta-side)
- Full Dash Manager–style dash customizer / permanently replacing `OculusDash.exe`
- Reviving Oculus Home / Homeless
- macOS / Linux; hotkey profiles per game (global only)

**Runtime caveats (Meta / Windows)**
- Newer Meta runtimes sometimes **reject** `server:` ASW CLI commands — the log will say so
- Pixel density / SS often needs a **new VR session** to stick
- Link registry changes usually need a **Link reconnect** or `OVRService` restart
- ADB `debug.oculus.*` props **reset on Quest reboot** — leave apply-on-connect on
- Elevated tray cannot be clicked from SteamVR — use HotKeys / voice / auto
- Wireless ADB ≠ Air Link
- Windows-only

---

### Possible later

- Authenticode signing for Setup.exe (when budget allows)
- Optional dedicated elevated helper process (tray can already start elevated via scheduled task)
- More built-in presets as people request process names

Feedback and bug reports welcome — especially “profile didn’t apply for game X (process name: …)” or audio / SteamVR session edge cases.

---

### Requirements

- Windows 10 / 11 (64-bit)
- Meta Quest PC software (for Link / Debug Tool CLI) and/or SteamVR (for Steam OpenXR)
- Quest Developer Mode + USB debugging (or Wireless ADB Pair) for headset ADB features — ADB is bundled

---

### How to get it

**Installer (recommended):**  
https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest  

**User guide (wiki):**  
https://github.com/Eliminater74/MetaQuestTrayTool/wiki  

Run `MetaQuestTrayTool-Setup-*.exe` — Program Files, Start Menu, Uninstall. Self-contained (includes .NET 8). Settings under `%AppData%\MetaQuestTrayTool\` survive uninstall. Wiki covers first launch, each sidebar page, Manual-at-boot, PreventDashLaunch, voice phrases, and troubleshooting.

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
TL;DR: free modern OTT-inspired tray app (v1.1.18) — neon icon, settings survive power loss, Pause ADB / VR-headsets-only so phones and TVs keep ADB, headset wait cues, unelevated SteamVR helper, Quest Home after SteamVR exit, TTS voice picker, PreventDashLaunch over Link, Status board, profiles. Inspired by ApollyonVR’s Oculus Tray Tool; clean C# rewrite, not a decompile. Installer: https://github.com/Eliminater74/MetaQuestTrayTool/releases/latest  Wiki: https://github.com/Eliminater74/MetaQuestTrayTool/wiki Changelog: https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/CHANGELOG.md
```
