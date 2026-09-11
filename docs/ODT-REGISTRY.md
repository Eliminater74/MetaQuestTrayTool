# Oculus Debug Tool — registry & runtime map

This map combines live ODT/registry observations from **2026-09-08** with older reverse-engineering notes. A binary string is evidence of a name, not proof of its behavior. See [the issue #4 investigation](investigations/issue-4-link-registry.md) for the tested binary identity, observations, fixes, and remaining limits.

Meta Quest Tray Tool uses the observed per-user key for **Quest Link** overrides. **Game Settings** (super sampling, PC ASW, FOV, etc.) still go through `OculusDebugToolCLI.exe` / OVRService. Their command path is separate and was not revalidated in this investigation.

## Verification scope

- **Observed persistent configuration:** changing ODT bitrate to 450 created DWORD `BitrateMbps=450` in HKCU. An external DWORD write of 500 appeared in reopened ODT without a service restart. The rebuilt tray service was also used to apply 500; reopened ODT showed it.
- **Observed mapping defects:** ODT writes sharpening Disabled/Normal/Quality as **1/2/3**. ODT edits `EncodeWidth`; it displayed 2912 even with `EncodeResolutionWidth=3664` present. The tray now prefers `EncodeWidth`, including an explicit zero, and retains the other name for legacy compatibility.
- **Registry verification:** apply compares every attempted DWORD write and deletion with a fresh registry read. Mismatches and access failures are reported; this verifies persistence only, not the active encoder or an independently running ODT process.
- **Refresh:** Read live registry loads overrides into the controls, including values outside the preset lists, without writing them or replacing saved settings. The next edit applies the loaded controls. Missing sharpening means no explicit override, not Disabled.
- **Runtime/reconnect/restart:** no active headset stream was measured. A service restart attempt was denied by Windows in this non-elevated session. Reconnect and restart requirements for individual fields remain unverified; neither is needed merely to reproduce the tested ODT reopen/read of persisted bitrate.
- **Mobile ASW:** retained for compatibility, but this ODT build does not expose it. No live effect was verified.

---

## Primary hive (per user)

**`HKCU\Software\Oculus\RemoteHeadset`**

Also referenced by older binary analysis: **`HKLM\Software\Oculus\RemoteHeadset`**. Its precedence/use was not established here. The tray does not mirror writes into HKLM or another user's HKCU.

| Registry value | ODT GUI label | Tray tool |
| --- | --- | --- |
| `DistortionCurve` | Distortion Curvature | Quest Link page |
| `HEVC` | Codec (force HEVC / H.265) | Quest Link page |
| `NumSlices` | Sliced Encoding (`1` = off) | Quest Link page (case-insensitive name) |
| `EncodeWidth` | Encode Resolution Width | Written with `EncodeResolutionWidth` |
| `EncodeResolutionWidth` | Legacy compatibility name; runtime ownership unverified | Written with `EncodeWidth`; read only when `EncodeWidth` is absent |
| `DBR` | Encode Dynamic Bitrate | Quest Link page |
| `DBRMax` | Dynamic Bitrate Max | Quest Link page |
| `DBROffsetMbps` | Dynamic Bitrate Offset (Mbps) | Quest Link page |
| `BitrateMbps` | Encode Bitrate (Mbps) | Quest Link page |
| `LinkSharpeningEnabled` | Link Sharpening | Quest Link page |
| `MobileASWMode` | Mobile ASW | Quest Link page |
| `LocalDimming` | Local Dimming (Quest Pro) | Not exposed yet |
| `DropFrames` | Debug: drop frames | Not exposed (dev only) |
| `FramesToDrop` | Debug: frames to drop | Not exposed |
| `DropFramesPeriod` | Debug: drop period | Not exposed |
| `AutoMergeTraces` | Trace merge | Not exposed |
| `AutoMergeTracesPath` | Trace path | Not exposed |

### Value notes

**DistortionCurve** (DWORD): `0` = Low, `1` = High. Delete the value for ODT “Default”.

**LinkSharpeningEnabled** (DWORD): observed ODT mapping is `1` = Disabled, `2` = Normal, `3` = Quality. Delete for no explicit override; ODT displayed Normal when absent on the tested installation. The previous 0/1/3 mapping was incorrect. The tray's saved enum values are unchanged; only registry conversion changed.

**HEVC** (DWORD): `1` = prefer HEVC (common for Air Link). Delete for default / H.264 behavior.

**NumSlices** / **numSlices** (DWORD): `1` displayed Off in ODT. Windows registry value names are case-insensitive; these are one value, so the tray writes/deletes it once. Any effect on artifacts or latency was not tested.

**DBR** (DWORD): `1` = dynamic bitrate on, `0` = off. Delete for default / automatic.

**DBRMax**, **DBROffsetMbps**, **BitrateMbps**, **EncodeWidth**: `0` or delete often means “automatic” in ODT; tray treats `0` as “no override” and deletes the key when applying global defaults.

For stream effects, reconnect Link or restart OVRService if needed; this is operational guidance, not a verified per-setting requirement. The observations above establish persisted ODT configuration, not stream behavior.

---

## Air Link vs wired (not RemoteHeadset)

There is **no** `RemoteHeadset` registry value for “Air Link vs cable”. Meta stores the live/last transport flag in:

**`%LocalAppData%\Oculus\DeviceCache.json`**

Headset entries include:

| JSON field | Meaning |
| --- | --- |
| `type` | `"headset"` for the HMD |
| `isUsingAirLink` | `true` = Air Link, `false` = wired Link (Meta’s own flag) |
| `connectionState` / `rdConnectionState` | e.g. `connected` / `disconnected` |
| `supportsOculusLink` | headset supports Link |
| `serialNumber` | HMD serial |

The tray **Info** page probes this file, plus Oculus USB VIDs (`VID_2833` / `VID_2BEC`), `server:EnumHmd`, and SteamVR / Virtual Desktop processes for Steam Link / VD sessions. Meta often **auto-connects** when the headset wakes on Wi‑Fi (DeviceCache `connectionState=connected`) without launching the Link UI — and can stay `connected` + `inoperable` while **Steam Link** is the real streamer. The probe prefers live `vrserver` / VD processes unless strong Meta evidence is present: a live operable Link cache line, Link audio without a competing streamer, or `rdConnectionState=connected`. `EnumHmd` alone does **not** beat SteamVR (that false positive used to re-enable SS / Link applies under Steam Link).

When **Virtual Desktop** or **Steam Link / SteamVR** is the active streamer, the tray automatically **skips live** Meta Link registry writes and OculusDebugToolCLI (SS / ASW / FOV). The Quest Link page still lets you edit and save presets for the next Meta Link / Air Link session. There is no equivalent live command channel on those pipes — headset ADB, OpenXR, power, and audio still run. Bitrate/codec for those streamers belongs in their own apps. User-facing comparison: [Quest Link vs Steam Link](https://github.com/Eliminater74/MetaQuestTrayTool/wiki/Quest-Link-vs-Steam-Link).

### PreventDashLaunch (OculusKiller registry alternative)

**`HKLM\SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus\Config`**

| Value | Type | Meaning |
| --- | --- | --- |
| `PreventDashLaunch` | DWORD `1` | Meta runtime should not start Oculus Dash |
| `CoreChannel` | string | `LIVE` (stable), `PublicTest` (PC PTC/beta), or `NO_UPDATES` (block updates; not a beta) |

Documented by [OculusKiller](https://github.com/DevOculus-Meta-Quest/OculusKiller): PreventDashLaunch blocks Dash entirely but **does not** start SteamVR by itself. The tray’s **Service & Startup** page can write these keys (needs Admin), restart `OVRService`, and while PreventDashLaunch is ON **auto-starts SteamVR** when Meta Link / Air Link connects. Setting `CoreChannel` is optional and never changes until you Apply; enabling PreventDashLaunch can optionally offer `NO_UPDATES` as a precaution.

**PreventDashLaunch does not block Meta Horizon Link (`Client.exe`).** A full registry sweep of `Oculus VR, LLC` (HKLM + HKCU) found no documented equivalent such as `PreventClientLaunch` / `AutoStartClient`. Boot launch is usually `OVRService` (Automatic) → `OVRServiceLauncher.exe` → per-session `LaunchedApplication`; `OVRServer_x64.exe` can **restore** a client window (`Restored top-level window for pid`) if the app was hidden, not fully quit. Stop/start/restart of `OVRService` after a **full quit** usually does **not** reopen the desktop app. Meta also does not register in Windows `Run` / Startup apps on most installs. **Steam Link only** users can uninstall Meta PC software entirely; Quest Link PCVR still needs the runtime and usually the Link app when connecting.

---

## Runtime commands (not RemoteHeadset)

The follow-up [v1.1.29 control audit](investigations/v1.1.29-control-audit.md) checked installed CLI help and HUD selection. FOV defaults now send an explicit 1/1 command; separate axes survive saved JSON migration. Other runtime effects still require headset validation.

| ODT setting | CLI / server command |
| --- | --- |
| Pixels Per Display Pixel Override | `service set-pixels-per-display-pixel-override` / `server:PixelsPerDisplayPixelOverride` |
| FOV-Tangent Multiplier (H / V) | `service set-client-fov-tan-angle-multiplier` / `server:ClientFovTanAngleMultiplierX/Y` |
| Force Mipmap on All Layers | `service set-force-mip-gen-on-all-layers` / `server:ForceMipGenOnAllLayers` |
| Offset Mipmap Bias | `service set-offset-mip-bias-on-all-layers` / `server:CompositorMipBias` |
| Use FOV Stencil | `service set-use-fov-stencil` / `server:UseFovStencil` |
| Adaptive GPU Perf Scale | `service enable-adaptive-gpu-perf-scale` / `server:EnableAdaptiveGpuPerfScale` |
| (PC) ASW | `server:asw.Auto`, `asw.off`, `asw.Clock45`, `asw.Sim45`, … |
| Visual HUD | `perfhud set-mode` / `server:PerfHudModeAll` |

The app's saved HUD enum numbers are **not** CLI mode numbers. Use `VisualHudMapping`: Performance/headroom summary=1, App Render Timing=3, Compositor Render Timing=4, ASW=6. Installed ODT displayed CLI 2 as Latency Timing. Version retains legacy mode 5; its label is absent in this ODT build, so display remains unverified. None sends `perfhud reset`.

Historical reports distinguish transient pixel density from persisted Link overrides. Reboot survival was not tested in this investigation.

---

## Other registry roots (install / config)

Referenced inside ODT binaries (not game-tweak values):

| Path | Purpose |
| --- | --- |
| `HKCU\SOFTWARE\Oculus\` | User Oculus config |
| `HKCU\SOFTWARE\Oculus VR, LLC\Oculus\` | Libraries, etc. |
| `HKCU\SOFTWARE\Oculus\Dash\` | Dash |
| `HKLM\SOFTWARE\Oculus VR, LLC\Oculus\` | Install path (`Base`), version |
| `HKLM\SOFTWARE\Wow6432Node\Oculus VR, LLC\Oculus\` | 32-bit view of install |
| `HKLM\SOFTWARE\Oculus VR, LLC\LibOVR\` | Legacy LibOVR |
| `HKLM\SOFTWARE\Khronos\OpenXR\1` | OpenXR active runtime (tray writes this) |

Community ASW workarounds sometimes mention `AswDisabled` under `HKCU\Software\Oculus` or `HKLM\SOFTWARE\Oculus` — not in the ODT GUI string table for `RemoteHeadset`; prefer ODT/CLI ASW modes when possible.

---

## Rediscover keys after a Meta update

```powershell
# Snapshot only the owned Link value names, with their types and registry views
.\scripts\Get-LinkRegistrySnapshot.ps1 | ConvertTo-Json -Depth 6 | Set-Content before.json

# Change ONE setting in Oculus Debug Tool

.\scripts\Get-LinkRegistrySnapshot.ps1 | ConvertTo-Json -Depth 6 | Set-Content after.json
Compare-Object (Get-Content before.json) (Get-Content after.json)
```

Run from the repository root. Compare snapshots taken under the same Windows account as each tool; HKCU belongs to the process account, and using another administrator account changes it. The script is read-only and excludes unrelated subkeys (including Air Link pairing data). A mismatch on another Meta version needs that version's observed ODT write, not a speculative alternate registry path.

---

## Source in this repo

Strings were harvested from:

- `TEMP/oculus-diagnostics/OculusDebugTool.exe` (GUI — full `RemoteHeadset` value list)
- `TEMP/oculus-diagnostics/OculusDebugToolCLI.exe` (CLI — `service` / `server:` commands)

Implementation: `LinkSettingsService.cs`, `OculusDebugToolService.cs`.

See also: [docs/README.md](README.md) · [VOICE-AND-HOTKEYS.md](VOICE-AND-HOTKEYS.md)
