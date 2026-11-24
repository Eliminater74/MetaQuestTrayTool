# Independent security and reliability review

This review covers the advanced performance and PCVR provider changes. Passing the existing suite was not treated as the review. The notes below are the evidence. No physical headset, ALVR server, or Pyrowave install was used.

## ADB command construction

Property names must match `^[A-Za-z][A-Za-z0-9._]{0,127}$` and start with `debug.oculus.` or `debug.q3pw.`. Values must match `^[A-Za-z0-9._:+-]{1,64}$`. A name or value with a newline, semicolon, or other rejected character fails before a write. The transaction test covers both cases.

Writes go through the existing `AdbService.SetProp` for the serial the caller already selected. The new code does not call `adb kill-server`, does not disconnect other Android devices, and does not poll ADB while idle. `TelemetryCollectionPolicy.AllowBackgroundAdbWhileIdle` remains false.

## Recovery and races

The transaction lock is held for apply, restore, and discard. A second apply on the same headset keeps the first original value. A different serial or a different firmware fingerprint does not overwrite the snapshot. A value that matches neither the original nor the applied value is left unchanged. The recovery file is written to a temp file and moved or replaced. A failed save restores the previous in-memory snapshot.

Originally absent properties still cannot be deleted with `setprop`. That result stays incomplete and asks for a reboot.

## Experimental controls

GPU level 7 is refused when battery temperature is missing or at or above 45 °C. That threshold is an application gate on the battery reading, not GPU junction temperature. Refresh property readback is reported separately from a parsed `dumpsys display` rate, and neither is reported as OpenXR, streaming, or fresh-frame FPS. A display dump that positively shows a different rate is restored. `debug.oculus.forceDisplayScaling` is not written.

## IPC

`MetaQuestTrayTool.PerformanceOverlay` is created only from the Performance page. The discretionary ACL is the current user only. A medium integrity label is applied with `SetKernelObjectSecurity` because the pipe security descriptor does not keep that SACL. Each command must present the token created when that pipe starts. Reads longer than 1024 bytes are rejected. Reads and writes time out after two seconds. The command allow list does not include ADB, shell, profile changes, or process termination. The tray does not load an OpenVR native library. The dashboard companion is a separate executable. Its start info has an empty argument list, and the token is written to its standard input. An elevated tray starts it with the Explorer token and does not fall back to an elevated process. A missing library or a failed `OpenVR.Init` exits that process with code 0. The companion does not restart itself after a crash.

Remaining limit: a process that can read this tray's memory can recover the token. The pipe is local isolation, not a remote authentication protocol. Physical SteamVR acceptance was not performed.

## Provider files

The generic numeric editor and its running-server override are gone. ALVR can change preferred FPS, an existing ConstantMbps number from 5 to 1000, and an existing resolution leaf, only while the caller says the server is not running. It does not switch a variant or create a missing object. Enum objects and unknown paths are refused. Unknown JSON is preserved. `WriteWithBackup` refuses a path containing `..` and keeps a `.bak`. A backup restore also refuses a running server. A session that looks like Pyrowave is not edited by the ALVR editor. No ALVR HTTP endpoint is called.

Pyrowave session files are not edited. `PyrowaveControl` builds the SetValues body from tools/quest3/control.py at 1b529ef. A post is refused unless the caller supplies session JSON whose `session_settings.video.pyrowave` value is an object, and unless the body is not `RestartSteamvr` or `UpdateClientList`. The only URL the code will use is `http://127.0.0.1:8082/api/dashboard-request`. No page calls that post. Building the body is not provider management. Wired mode, automatic bitrate, and refresh are not sent. A game profile does not post this body. Dashboard launch requires an explicit authorization flag and adds no arguments. SteamVR driver registration still only appends, and only when authorized. The ALVR editor has no production caller. A caller can still pass `serverRunning: false`. There is no in-library bypass flag.

## Profiles and processes

A game profile can write GPU level 7 only when the named provider is already the active session, the Headset page opt-in is on, the trusted serial matches, a fingerprint was read, the battery gate allows it, and the current GPU level is not already 7. If it is already 7, the profile does not write and does not restore that value when the game exits. An unreadable current value writes nothing. Experimental refresh and named ALVR or Pyrowave presets are not written. ALVR, Pyrowave, and Steam Link sessions do not authorize the write. Auto-switch does not launch or stop a provider. Command-shaped provider names write nothing. Benchmark cancellation does not kill a process. The PC benchmark does not restart SteamVR. Identical PC samples are refused before any apply. The thermal watch samples only after an explicit level 7 apply that this app owns. Cancellation and application exit restore that owned value. A replaced watch does not restore the newer apply. The watch does not poll while idle. A snapshot that is cancelled before any property is written is discarded.

## Integration audit on 2026-11-24

The review started at `399cddb` and treated the existing source as untrusted. Confirmed defects were repaired in the commits recorded in `STAGE-TRACKER.md`. Automated tests were added for thermal cancellation, an unwritten snapshot, the overlay launch plan and pipe ACL, Pyrowave session identity, identical benchmark refusal, ABBA order, and a profile that finds GPU level 7 already applied.

Not repaired, because the implementation is absent rather than awaiting hardware:

- No page edits ALVR or posts a Pyrowave request.
- The overlay companion does not restart after it exits.
- Controller and dashboard-laser interaction were not executed. Requesting mouse input is not acceptance.
- `DiscardRecovery` is not shown in the UI. After a reboot leaves a third property value, a new apply stays blocked until the property is absent or the recovery file is removed.
- The overlay publish includes its own self-contained runtime. That is required for a machine without the shared runtime. The installer was not shrunk.

## What this review did not prove

- No race was injected against a live ADB server.
- No SteamVR, ALVR, or Pyrowave process was started.
- File ACLs on the recovery and benchmark history files are the default for the user profile. They were not given a custom DACL in this pass.
- The display-dump parser can miss a firmware format. A miss stays "not parsed" rather than being treated as success.
