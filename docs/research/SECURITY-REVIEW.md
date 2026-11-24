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

`MetaQuestTrayTool.PerformanceOverlay` is created only from the Performance page. The ACL is the current user and Administrators. Each command must present the token created when that pipe starts. Reads longer than 1024 bytes are rejected. The command allow list does not include ADB, shell, profile changes, or process termination. The tray does not load an OpenVR native library. The dashboard companion is a separate executable. Its start info has an empty argument list, and the token is written to its standard input. A missing library or a failed `OpenVR.Init` exits that process with code 0.

Remaining limit: a process that can read this tray's memory can recover the token. The pipe is local isolation, not a remote authentication protocol. Physical SteamVR acceptance was not performed.

## Provider files

The generic numeric editor and its running-server override are gone. ALVR can change preferred FPS, an existing ConstantMbps number from 5 to 1000, and an existing resolution leaf, only while the caller says the server is not running. It does not switch a variant or create a missing object. Enum objects and unknown paths are refused. Unknown JSON is preserved. `WriteWithBackup` refuses a path containing `..` and keeps a `.bak`. A backup restore also refuses a running server. A session that looks like Pyrowave is not edited by the ALVR editor. No ALVR HTTP endpoint is called.

Pyrowave session files are not edited. `PyrowaveControl` builds the SetValues body from tools/quest3/control.py at 1b529ef and posts it only through an injected transport to `http://127.0.0.1:8082/api/dashboard-request`. A body containing RestartSteamvr is rejected. Wired mode, automatic bitrate, and refresh are not sent. A game profile does not post this body. Dashboard launch requires an explicit authorization flag and adds no arguments. SteamVR driver registration still only appends, and only when authorized.

## Profiles and processes

A game profile can write GPU level 7 only when the named provider is already the active session, the Headset page opt-in is on, the trusted serial matches, a fingerprint was read, and the battery gate allows it. Experimental refresh and named ALVR or Pyrowave presets are not written. Auto-switch does not launch or stop a provider. Command-shaped provider names write nothing. Benchmark cancellation does not kill a process. The PC benchmark does not restart SteamVR. The thermal watch samples only after an explicit level 7 apply. It does not poll while idle.

## What this review did not prove

- No race was injected against a live ADB server.
- No SteamVR, ALVR, or Pyrowave process was started.
- File ACLs on the recovery and benchmark history files are the default for the user profile. They were not given a custom DACL in this pass.
- The display-dump parser can miss a firmware format. A miss stays "not parsed" rather than being treated as success.
