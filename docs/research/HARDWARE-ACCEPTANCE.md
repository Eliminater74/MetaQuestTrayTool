# Physical hardware acceptance

Nothing in this file has been run on a headset by the implementation agent. Host-side tests do not check these boxes.

## Pending

- Quest 3, Quest 3S, Quest 2, Quest Pro, Quest 1 over USB ADB and wireless ADB.
- Confirm automatic ADB still waits for a PCVR session and does not disconnect unrelated Android devices.
- Confirm the shared ADB server is not killed when a session ends.
- Quest Link, Air Link, Steam Link, Virtual Desktop, stock ALVR, and Quest3-Pyrowave coexistence. Confirm SteamVR-over-Link is not labeled as the Steam Link app.
- Standard GPU levels 0–4 still apply and read back.
- Experimental GPU level 7 on a Quest 3 only: property readback, VrApi GPU level, actual GPU clock, battery temperature, and restore of the previous property. Confirm Quest 3S refuses it.
- Confirm firmware thermal limits still throttle. The 45 °C battery gate is a tray refusal, not a claim about Meta's thermal policy.
- Standard refresh rates.
- Experimental 144 Hz and 207 Hz property writes, effective panel rate, and restore. Confirm the streaming app's delivered FPS separately.
- Confirm 240 Hz and `debug.oculus.forceDisplayScaling` are not written.
- Benchmark numbers against a known session. 1% lows must come from frame times, not one-second FPS samples.
- Desktop performance companion. In-headset OpenVR overlay is not shipped.
- ALVR and Pyrowave discovery, backup, and rollback. Confirm a running server is not edited unless you explicitly override that refusal.
- Disconnect, reconnect, and reboot recovery. Properties that cannot be cleared without reboot must say so.

## Unsupported or not claimed

- Wavelet encode/decode, parallel USB video, and Pyrowave fresh-FPS inside Quest Link.
- Optical motion-to-photon latency.
- Sustained thermal safety of GPU level 7 or experimental refresh. Pyrowave's published screens were short and said sustained thermals were not measured.
