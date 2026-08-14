# Audio and power

## Audio switcher

**Tray Tool → Use Audio Switcher → Configure**

Policy:

1. At Windows / tray start — **do not** steal speakers (Oculus Virtual Audio can be installed even when the headset is off).
2. When a **PCVR session starts** — switch to configured VR playback/recording (optional communications devices).
3. When the session **ends** — restore desktop / fallback devices.

Capture current desktop devices as fallback. Trigger is Link-audio-as-Windows-default or OVRService, depending on the picker.

Voice: **switch to VR audio** / **restore desktop audio**.

## Power

**Power Options:**

- Auto-switch to a VR power plan when OVRService / Link is up (or on tool start/exit)
- Restore a fallback plan when it stops
- Optionally disable USB selective suspend while VR is running
- **Restart OVRService after sleep** (also on Service & Startup)

Sleep/wake: DeviceCache and audio often blip. The tray quiets false “Link session ended” toasts for ~20 seconds after resume and only treats a **live** stream as a drop. If you were actually in Link *and* restart-on-wake is on, OVRService restart **will** end that session by design.

## Related

- [[HotKeys-Voice-and-Announcements]]
- [[Troubleshooting]]
