# Headset (ADB)

SideQuest-style props via **bundled Google platform-tools**. Not the same as Air Link.

v1.1.29 fixes WPF thread-access failures in wireless Connect/Pair and Send text by capturing UI input before background ADB work. The Unreleased source checkout adds trusted USB/wireless headset selection, independent CPU/GPU levels, headset-aware refresh choices, Dynamic FFR, recording start/stop, guarded experimental rendering controls, and reset of documented live ADB defaults. Physical headset validation remains open. Device/app-default options stop applying their override; they do not necessarily reset a property already active on the headset. Use **Reset live ADB overrides** where documented defaults exist, and reboot the headset for temporary overrides that do not have a proven safe clear. The legacy capture-eye option is experimental and does not promise a specific stereo layout across firmware versions.

![Headset performance](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/10-headset-performance.png)

![Headset capture / trust](https://raw.githubusercontent.com/Eliminater74/MetaQuestTrayTool/main/docs/media/09-headset-capture.png)

## Trust and Developer Mode

1. Enable **Developer Mode** on the Quest (Meta developer account).
2. USB: plug in, allow debugging, set **Always allow**.
3. Or **Wireless debugging**: Pair (pairing port + code), then Connect (IP:connect port).
4. **Enable tcpip** over USB once if you prefer `adb connect` without pairing every time.
5. **SideQuest on the headset** can also open an ADB port while it is running — use the headset LAN IP and that port (often **5555**) with **Connect**. No USB pair step required for that session.
6. Optional **auto-reconnect** to the saved wireless endpoint (that host:port only — the tray does **not** scan the LAN for random ADB devices).
7. **VR headsets only** is **on by default**: if a phone/tablet/TV shows up on wireless ADB (or you Connect the wrong IP), that session is dropped. **Uncheck** it (Headset page or tray → Headset (ADB)) if you want other gadgets to stay connected. USB phones are never disconnected. Quest tweaks still never run on non-headsets.
8. **Pause ADB** (tray → Headset (ADB)): stop polling / reconnect / disconnect while you use another device — until you resume, or for 2 hours. The tray stays running; tooltip shows when ADB is paused.

Only **real VR headsets** are trusted. Phones, tablets, and emulators never receive headset commands.

## What you can set

Independent CPU and GPU levels, texture size, model-aware refresh rate, Dynamic or Fixed FFR, chroma, capture size/FPS/bitrate, headset recording start/stop, Quest Pro local dimming, subsampled foveation, live documented-default reset, paste-text / proximity / guardian helpers. High Top FFR is labelled legacy/VrApi because OpenXR treats it as High, and Capture FPS is labelled legacy/firmware-dependent until physical headset validation proves current firmware honors `debug.oculus.capture.fps`.

## Headset ADB screenshots

The ADB screenshot path saves the current headset view as a PNG:

- Tray → **Screenshots → Take headset screenshot (ADB)**
- Tray → **Headset (ADB) → Take headset screenshot (ADB)**
- Headset page → **Take headset screenshot (ADB)**
- **Ctrl+Shift+Num 9**
- Voice: “take headset screenshot”, “quest screenshot”, or “ADB screenshot”

Files save under `%AppData%\MetaQuestTrayTool\screenshots\`. The command uses the same real-VR/trusted-headset guard as other ADB actions, validates the PNG before reporting success, and queues **“Screenshot taken.”** through headset announcements when the Quest audio route is available.

The **Take screenshot (Quest Link preferred)** action on the tray Screenshots menu and Headset page first tries Quest Link / Air Link mirror capture when Meta Link is actively streaming, then falls back to this ADB path.

**Apply when headset connects** re-pushes props (Quest **resets `debug.oculus.*` on reboot**).

Battery, charge, and Wi‑Fi show on Status / Info via `dumpsys` when ADB is up.

## Wireless ADB vs Air Link

| | Wireless ADB | Air Link |
| --- | --- | --- |
| Purpose | Debug / props | PCVR video stream |
| Needs | Developer Mode + pair/connect, tcpip, **or SideQuest on-headset ADB** | Meta Horizon Link |
| Same Wi‑Fi | Yes | Yes |

You can use Air Link **without** ADB. ADB is only for headset tweaks (CPU/GPU, FFR, refresh, …).

**Steam Link app:** there is **no** Meta Debug Tool / Link-registry path. ADB is the **only** way this tray can tweak the Quest itself. SS / ASW / bitrate still belong in Steam’s UI. See [[Quest-Link-vs-Steam-Link]].

### SideQuest on the headset

If the **SideQuest app is running on the Quest**, it often exposes a wireless ADB listener. Enter the headset’s Wi‑Fi IP and the port SideQuest opened (commonly `5555`) on the Headset page, click **Connect**, then enable **Auto-reconnect** if you want the tray to keep using that endpoint. This is separate from the SideQuest **PC** app; the tray already bundles its own `adb`.

## Related

- [[Quest-Link]]
- [[Troubleshooting]]
