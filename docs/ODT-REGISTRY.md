# Oculus Debug Tool — registry & runtime map

Reference extracted from **`OculusDebugTool.exe`** and **`OculusDebugToolCLI.exe`** (Meta Quest PC app, oculus-diagnostics folder). Paths and value names are embedded as strings in those binaries.

Meta Quest Tray Tool uses the same hive for **Quest Link** settings. **Game Settings** (super sampling, ASW, FOV, etc.) still go through `OculusDebugToolCLI.exe` / OVRService — they are not stored under `RemoteHeadset`.

---

## Primary hive (per user)

**`HKCU\Software\Oculus\RemoteHeadset`**

Also referenced: **`HKLM\Software\Oculus\RemoteHeadset`** (machine-wide; rarely needed).

| Registry value | ODT GUI label | Tray tool |
| --- | --- | --- |
| `DistortionCurve` | Distortion Curvature | Quest Link page |
| `HEVC` | Codec (force HEVC / H.265) | Quest Link page |
| `NumSlices` | Sliced Encoding (`1` = off) | Quest Link page (`numSlices` alias on read/write) |
| `EncodeWidth` | Encode Resolution Width | Written with `EncodeResolutionWidth` |
| `EncodeResolutionWidth` | Same (OVRService alias) | Quest Link page |
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

**LinkSharpeningEnabled** (DWORD): `0` = Off, `1` = Normal, `2`/`3` = Quality (tray uses `3` for Quality).

**HEVC** (DWORD): `1` = prefer HEVC (common for Air Link). Delete for default / H.264 behavior.

**NumSlices** / **numSlices** (DWORD): `1` = single slice / sliced encoding off (wired Link artifact workaround). Windows registry keys are case-insensitive; ODT string table uses `NumSlices`.

**DBR** (DWORD): `1` = dynamic bitrate on, `0` = off. Delete for default / automatic.

**DBRMax**, **DBROffsetMbps**, **BitrateMbps**, **EncodeWidth**: `0` or delete often means “automatic” in ODT; tray treats `0` as “no override” and deletes the key when applying global defaults.

Link changes usually need a **Link reconnect** or **OVRService** restart.

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

The tray **Info** page probes this file, plus Oculus USB VIDs (`VID_2833` / `VID_2BEC`), `server:EnumHmd`, and SteamVR / Virtual Desktop processes for Steam Link / VD sessions.

---

## Runtime commands (not RemoteHeadset)

These are sent to **OVRService** via `OculusDebugToolCLI.exe` (same mechanism as the ODT GUI for PC VR tweaks):

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

Pixel density often **does not survive a PC reboot**; encode width and distortion usually do.

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
# Snapshot before changing one ODT control
Get-ItemProperty 'HKCU:\Software\Oculus\RemoteHeadset' | Out-File before.txt

# Change ONE setting in Oculus Debug Tool

Get-ItemProperty 'HKCU:\Software\Oculus\RemoteHeadset' | Out-File after.txt
Compare-Object (Get-Content before.txt) (Get-Content after.txt)
```

Or export the hive:

```powershell
reg export "HKCU\Software\Oculus\RemoteHeadset" RemoteHeadset.reg /y
```

---

## Source in this repo

Strings were harvested from:

- `TEMP/oculus-diagnostics/OculusDebugTool.exe` (GUI — full `RemoteHeadset` value list)
- `TEMP/oculus-diagnostics/OculusDebugToolCLI.exe` (CLI — `service` / `server:` commands)

Implementation: `LinkSettingsService.cs`, `OculusDebugToolService.cs`.

See also: [docs/README.md](README.md) · [VOICE-AND-HOTKEYS.md](VOICE-AND-HOTKEYS.md)
