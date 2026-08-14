# Quest Link

PCVR over **Meta Horizon Link** (wired USB or Air Link). Settings are written to:

`HKCU\Software\Oculus\RemoteHeadset`

![Quest Link page](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/08-quest-link.png)

## What you can set

| Option | Notes |
| --- | --- |
| Bitrate (Mbps) | `0` / empty = no override (Meta default) |
| Encode resolution width | e.g. 3664; `0` = no override |
| HEVC | Prefer H.265 for Air Link |
| Sliced encoding | `NumSlices` — `1` often used to reduce wired artifacts |
| Link sharpening | Off / Normal / Quality |
| Distortion curve | Low / High / default |
| Dynamic bitrate (DBR) | On/off + max + offset |
| Mobile ASW | Headset-side ASW |

GPU-tier **Apply recommended presets** fills Link + global game settings from the detected GPU.

**Apply** usually needs a **Link reconnect** or **Restart OVRService**.

Registry reference (value names from Meta’s Debug Tool binaries): see [ODT-REGISTRY.md in the repo](https://github.com/Eliminater74/MetaQuestTrayTool/blob/main/docs/ODT-REGISTRY.md).

## Air Link vs wired

There is **no** RemoteHeadset key for “Air vs cable”. Meta stores transport in:

`%LocalAppData%\Oculus\DeviceCache.json` → headset `isUsingAirLink`

The tray Status / Info probe also uses USB vendor IDs, Link audio, `rdConnectionState`, SteamVR (`vrserver`), and Virtual Desktop processes.

**Wi‑Fi auto-connect is not a Link session.** When the headset is on the network, DeviceCache often shows `connected` without opening Link. Dash → SteamVR and session-end toasts ignore those ghosts (they require a live stream).

## When Link settings are skipped

If **Steam Link / SteamVR** or **Virtual Desktop** is the live streamer, Meta Link registry + ODT SS/ASW are gated. Change bitrate in Steam / VD instead.

## Related

- [[Dash-to-SteamVR]]
- [[Service-and-Startup]]
- [[Troubleshooting]]
