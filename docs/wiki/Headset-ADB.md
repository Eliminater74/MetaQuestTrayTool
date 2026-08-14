# Headset (ADB)

SideQuest-style props via **bundled Google platform-tools**. Not the same as Air Link.

![Headset performance](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/10-headset-performance.png)

![Headset capture / trust](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/09-headset-capture.png)

## Trust and Developer Mode

1. Enable **Developer Mode** on the Quest (Meta developer account).
2. USB: plug in, allow debugging, set **Always allow**.
3. Or **Wireless debugging**: Pair (pairing port + code), then Connect (IP:connect port).
4. **Enable tcpip** over USB once if you prefer `adb connect` without pairing every time.
5. Optional **auto-reconnect** to the saved wireless endpoint.

Only **real VR headsets** are trusted. Phones, tablets, and emulators are ignored.

## What you can set

CPU / GPU levels, texture size, refresh rate, FFR, chroma, capture helpers, paste-text / proximity / guardian helpers.

**Apply when headset connects** re-pushes props (Quest **resets `debug.oculus.*` on reboot**).

Battery, charge, and Wi‑Fi show on Status / Info via `dumpsys` when ADB is up.

## Wireless ADB vs Air Link

| | Wireless ADB | Air Link |
| --- | --- | --- |
| Purpose | Debug / props | PCVR video stream |
| Needs | Developer Mode + pair/connect | Meta Horizon Link |
| Same Wi‑Fi | Yes | Yes |

You can use Air Link **without** ADB. ADB is only for headset tweaks.

## Related

- [[Quest-Link]]
- [[Troubleshooting]]
