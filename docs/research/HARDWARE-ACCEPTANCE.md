# Physical hardware acceptance

Nothing in this file has been run on a headset by the implementation agent. Host-side tests do not check these boxes.

## Pending

- Quest 3, Quest 3S, Quest 2, Quest Pro, Quest 1 over USB ADB and wireless ADB.
- Confirm automatic ADB still waits for a PCVR session and does not disconnect unrelated Android devices.
- Confirm the shared ADB server is not killed when a session ends.
- Quest Link, Air Link, Steam Link, Virtual Desktop, stock ALVR, and Quest3-Pyrowave coexistence. Confirm SteamVR-over-Link is not labeled as the Steam Link app.
- Standard GPU levels 0–4 still apply and read back.
- Experimental GPU level 7 on a Quest 3 only: property readback, VrApi GPU level, actual GPU clock, battery temperature, and restore of the previous property. Confirm Quest 3S refuses it. Confirm the session watch restores the GPU property when battery temperature rises to the application gate during play, when the watch is cancelled, and when the tray exits. Confirm a crash leaves a lease that the next start restores. Confirm the watch does not poll while no override is active, and that a GPU level set outside this app is not overwritten by a profile exit.
- Confirm firmware thermal limits still throttle. The 45 °C battery gate is a tray refusal on the battery reading, not GPU junction temperature, not a firmware limit, and not an established universal hardware safety limit. A missing battery reading must refuse GPU level 7.
- Standard refresh rates.
- Experimental 144 Hz and 207 Hz: requested property, property readback, effective panel rate, and restore. The host parser does not replace a headset check. Confirm the streaming app's delivered FPS separately.
- Confirm 240 Hz and `debug.oculus.forceDisplayScaling` are not written.
- Benchmark numbers against a known session. 1% lows must come from frame times, not one-second FPS samples.
- Desktop performance companion and the separate SteamVR dashboard process. CreateDashboardOverlay was not executed here. Mouse input is requested in source and was not accepted. Confirm compact, detail, and hidden modes, the dashboard laser click, and that a missing SteamVR install does not exit the tray. Confirm an elevated tray does not leave the companion elevated. The companion does not restart itself after a crash.
- ALVR preferred FPS, an existing ConstantMbps value, and an existing resolution leaf while the server is stopped, plus backup restore. A running ALVR server must not be edited. There is no override. Confirm the edit does not apply until the next ALVR start, because this tray does not restart SteamVR.
- Pyrowave SetValues for bitrate, chroma, wavelet, and resolution against a live dashboard at 127.0.0.1:8082. Confirm RestartSteamvr is not sent. Wired mode, automatic bitrate, and refresh writes are still unavailable.
- Disconnect, reconnect, and reboot recovery. Properties that cannot be cleared without reboot must say so.
- A game profile that names the already-active provider applies opted-in GPU level 7 only when the property is not already 7, and restores only the value that profile applied. A profile must not launch ALVR, Pyrowave, or SteamVR, and must not change the panel rate.
- An upgrade with `MetaQuestTrayTool.Overlay.exe` already running. The installer filter names that process. This audit did not run Setup over an existing installation, including a path that contains spaces.

## Unsupported or not claimed

- Wavelet encode/decode, parallel USB video, and Pyrowave fresh-FPS inside Quest Link.
- Optical motion-to-photon latency.
- Sustained thermal safety of GPU level 7 or experimental refresh. Pyrowave's published screens were short and said sustained thermals were not measured.
