# Capability matrix

Research commit for Quest3-Pyrowave: `1b529ef54b4f7b34a44c28ad9a54c6c4ac6887a7`.

States used in code: Supported (documented model or verified readback), Experimental, Unsupported, Unknown, Temporarily unavailable. "Verified" means this process read the value back. It does not mean a headset was measured in this audit.

| Capability | Quest Link / Air Link | Steam Link | Virtual Desktop | ALVR | Pyrowave | This tray after the upgrade |
| --- | --- | --- | --- | --- | --- | --- |
| Install / session detection | Existing Link probe | Existing session classification | Existing process probe | Files, processes, driver path | Distinct install and package name | Report only, no silent launch |
| Encode bitrate / codec | Existing Link registry | Not owned by this tray | Not owned | Existing ConstantMbps number while stopped | SetValues body, not a file edit | No invented API. RestartSteamvr is not sent |
| CPU / GPU levels 0–4 | ADB `debug.oculus.cpuLevel` / `gpuLevel` | Same headset props | Same headset props | Same headset props | Same headset props | Unchanged |
| GPU level 7 | Not a Link feature | Not a Steam Link feature | Not a VD feature | External ADB property | Documented on Quest 3 | Opt-in, Quest 3, snapshot, 45 C battery gate, session watch. Not a universal hardware limit |
| Refresh 60–120 | Model table | Provider must agree | Provider must agree | Session FPS if present | Session FPS if present | Unchanged model table |
| 144 / 207 Hz panel property | Not evidence of Link FPS | Not evidence of Steam FPS | Not evidence of VD FPS | Their client must agree | Their client sets and confirms modes | Opt-in property only |
| 240 Hz scaled panel | Not established for Link | Not established | Not established | Requires their scaling tool | Requires their client | Not applied |
| Frame-pipeline telemetry | Limited VrApi log | Limited | Limited | Only if logs/session expose it | Their fresh-FPS is theirs | Unavailable stays unavailable |
| In-headset overlay | Oculus HUD already exists | SteamVR dashboard would need OpenVR | Their own UI | Their dashboard | Their thumbstick UI | Separate process, Valve openvr v2.5.1. Tray does not load it. Physical acceptance not performed |
| Custom wavelet codec | No | No | No | No | Yes, inside their server | SetValues can name Cdf97, Cdf53, or Haar. This tray does not implement a codec |

## Headset family

| Model signal | Standard refresh fallback | Experimental GPU level 7 | Experimental 144/207 property | 240 Hz scaling |
| --- | --- | --- | --- | --- |
| Quest 3, and not 3S | 72, 80, 90, 120 | Experimental | Experimental | Not applied |
| Quest 3S | 72, 80, 90, 120 | Unsupported | Unsupported | Not applied |
| Quest Pro | 72, 80, 90 | Unsupported | Unsupported | Not applied |
| Quest 2 | Existing full enum | Unsupported | Unsupported | Not applied |
| Quest 1 | 60, 72 | Unsupported | Unsupported | Not applied |
| Unknown | Device default only | Unknown, not applied | Unknown, not applied | Not applied |

Quest 3S is classified before Quest 3 so the substring "Quest 3" cannot grant Quest 3-only experiments.
