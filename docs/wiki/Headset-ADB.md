# Headset (ADB)

SideQuest-style props via **bundled Google platform-tools**. Not the same as Air Link.

![Headset performance](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/10-headset-performance.png)

![Headset capture / trust](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/09-headset-capture.png)

## Trust and Developer Mode

1. Enable **Developer Mode** on the Quest (Meta developer account).
2. USB: plug in, allow debugging, set **Always allow**.
3. Or **Wireless debugging**: Pair (pairing port + code), then Connect (IP:connect port).
4. **Enable tcpip** over USB once if you prefer `adb connect` without pairing every time.
5. **SideQuest on the headset** can also open an ADB port while it is running — use the headset LAN IP and that port (often **5555**) with **Connect**. No USB pair step required for that session.
6. Optional **auto-reconnect** to the saved wireless endpoint.

Only **real VR headsets** are trusted. Phones, tablets, and emulators are ignored.

## What you can set

CPU / GPU levels, texture size, refresh rate, FFR, chroma, capture helpers, paste-text / proximity / guardian helpers.

**Apply when headset connects** re-pushes props (Quest **resets `debug.oculus.*` on reboot**).

Battery, charge, and Wi‑Fi show on Status / Info via `dumpsys` when ADB is up.

## Wireless ADB vs Air Link

| | Wireless ADB | Air Link |
| --- | --- | --- |
| Purpose | Debug / props | PCVR video stream |
| Needs | Developer Mode + pair/connect, tcpip, **or SideQuest on-headset ADB** | Meta Horizon Link |
| Same Wi‑Fi | Yes | Yes |

You can use Air Link **without** ADB. ADB is only for headset tweaks.

### SideQuest on the headset

If the **SideQuest app is running on the Quest**, it often exposes a wireless ADB listener. Enter the headset’s Wi‑Fi IP and the port SideQuest opened (commonly `5555`) on the Headset page, click **Connect**, then enable **Auto-reconnect** if you want the tray to keep using that endpoint. This is separate from the SideQuest **PC** app; the tray already bundles its own `adb`.

## Related

- [[Quest-Link]]
- [[Troubleshooting]]
