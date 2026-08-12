# Reddit post — Meta Quest Tray Tool

Copy everything under **TITLE** and **BODY** into a new Reddit post.
Suggested subs: `r/OculusQuest`, `r/virtualreality`, `r/SteamVR`, `r/MetaQuestVR` (check each sub’s self-promo rules).

---

## TITLE

```
[PC] Meta Quest Tray Tool — modern free OTT-style tray app for Quest Link / SteamVR (v1.0+)
```

---

## BODY

```markdown
**Meta Quest Tray Tool** is a free Windows system-tray app for Meta Quest PCVR (Link / Air Link) and SteamVR OpenXR.

It’s a **brand-new C# rewrite**, not a decompile and not a continuation of older unfinished ports. Goal: keep the Oculus Tray Tool workflow alive on today’s Meta stack.

**Author:** Eliminater74  
**Status:** early public / build-from-source friendly (Windows 10/11, .NET 8)

---

### Inspired by (please give credit)

Huge respect to **ApollyonVR** and the original **Oculus Tray Tool (OTT)** — the classic tray app that made SS / ASW / profiles / Link tweaks easy for years.

- Original OTT: https://techtipsvr.com/oculus-tray-tool/  
- Also listed on Guru3D’s OTT download page historically

Meta Quest Tray Tool is **inspired by OTT’s ideas and layout**, rebuilt from scratch for current Meta Quest Link, SteamVR OpenXR, and modern Windows. It is **not** affiliated with ApollyonVR or Meta. If you loved OTT, this is meant to fill that gap — not replace the credit OTT earned.

---

### What it does (short)

Lives in the notification area. Global VR defaults stay applied until a game with a **personal profile** launches → that profile’s tweaks apply → when the game exits, **global defaults come back**, with a tray notification.

You can also push SideQuest-style **ADB headset** tweaks when a real Quest connects, switch **OpenXR** Meta vs SteamVR, and manage Link bitrate / sharpening without digging through registries.

---

### What works now

**Tray + shell**
- Notification-area host with themed right-click menu (Pure Black / Dark / Light)
- OTT-style sidebar: Game Settings, Tray Tool, Power, Service & Startup, Log, Advanced, Quest Link, Headset, Info
- Close-to-tray + single-instance (won’t open two copies by mistake)

**Oculus / Meta PC runtime**
- Detect Meta/Oculus install + `OVRService`
- Start / stop / restart service (optional automation on tool start/exit / after sleep)
- Hands-free elevated start (one UAC, then silent elevated at logon) so OpenXR / service / profiles don’t pop UAC while you’re in the headset

**Game settings (OculusDebugToolCLI)**
- Super Sampling, ASW (Auto / Off / 45 / 30 / 18), FOV, Adaptive GPU, mip flags, FOV stencil, Visual HUD
- Custom extra CLI lines on global + personal profiles

**Profiles**
- Personal per-process profiles (Steam / Meta library picker with cover art, or custom)
- Auto-apply on game launch → restore **global** when the game exits (tray balloons)
- Built-in **global presets** (Balanced, Performance, Quality, Sim, Competitive)
- Built-in **PCVR game presets** (MSFS 2024, Beat Saber, Half-Life: Alyx, DCS, iRacing, and more)
- Profiles stored in `%AppData%\MetaQuestTrayTool\profiles.json`
- Settings export/import backup from Advanced

**Quest Link / Air Link**
- Bitrate, encode width, HEVC, sliced encoding, sharpening (`HKCU\Software\Oculus\RemoteHeadset`)
- Per-profile Link overrides (inherit = keep global)

**OpenXR**
- Switch ActiveRuntime between **Meta / Oculus** and **SteamVR** (global + per-profile)
- Restore preferred / previous runtime after a profile exits

**Audio / power**
- Auto-switch to headset audio when Link audio is active; restore desktop devices when Link drops
- Power plan switch, USB selective suspend option, restart `OVRService` after sleep

**Headset ADB (standalone Quest tweaks)**
- Bundled Google platform-tools ADB
- CPU/GPU level, texture size, refresh, FFR, chroma, capture settings
- Auto-apply when a **real VR headset** connects (Quest 2/3/3S/Pro, plus allowlist for Galaxy XR / HTC Vive standalone / Pico / Steam Frame)
- Phones, tablets, and **Android emulators are ignored** — they never get commands or trust
- Trusted serial (first real headset remembered; other VR serials blocked)
- Paste text / proximity / guardian helpers
- Custom ADB lines on global + personal profiles

**Info / Donate**
- Info page: live OpenXR, OVRService, headset identity
- Donate button (PayPal) if you want to support development

---

### What does **not** work yet / known limits

**Not implemented (planned or undecided)**
- Hotkeys
- Voice commands
- Oculus Homeless / some classic OTT extras
- Permanent AirLink
- Wireless ADB pairing UI (USB ADB works)
- In-app update checker
- Launch games directly from the library picker
- Profile process ignore-list
- Detect wired Link vs Air Link reliably
- Full Dash Manager–style dash customizer (out of scope)

**Runtime caveats (Meta / Windows)**
- Newer Meta runtimes sometimes **reject** `server:` ASW CLI commands — the log will say so
- Pixel density / SS often needs a **new VR session** to stick
- Link registry changes usually need a **Link reconnect** or `OVRService` restart
- ADB `debug.oculus.*` props **reset on Quest reboot** — leave apply-on-connect on
- Admin rights help for OpenXR HKLM + some service control (hands-free elevated mode is built in)
- This is Windows-only

---

### Planned next

- Hotkeys
- Wireless ADB pairing UI
- Profile ignore-list
- Library “launch game”
- In-app updates
- More Link detection / dynamic bitrate verification
- Decide on license before a big public binary release
- More built-in presets as people request process names

Feedback and bug reports welcome — especially “profile didn’t apply for game X (process name: …)”.

---

### Requirements

- Windows 10 / 11
- Meta Quest PC software (for Link / Debug Tool CLI) and/or SteamVR (for Steam OpenXR)
- Visual Studio Community + .NET 8 to build, **or** a Release build when I publish one
- Quest Developer Mode + USB debugging approval for ADB headset features

---

### How to get it

Repo / build instructions are in the project README (open `MetaQuestTrayTool.sln` → F5, or `dotnet build`).

Settings live under:  
`%AppData%\MetaQuestTrayTool\`

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
TL;DR: free modern OTT-inspired tray app — global defaults + per-game profiles that auto-apply/restore, Link + OpenXR switch, audio/power helpers, Quest ADB tweaks (real headsets only). Inspired by ApollyonVR’s Oculus Tray Tool; clean C# rewrite, not a decompile.
```
