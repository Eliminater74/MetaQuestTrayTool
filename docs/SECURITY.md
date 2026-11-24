# Security and trust

## Updates

The in-app updater accepts only the exact `MetaQuestTrayTool-Setup-{version}.exe` asset from
the project's HTTPS GitHub release URL. Before it launches an installer, it validates the
release tag, download size, and GitHub-published SHA-256 digest.

v1.1.31+ builds also download into a private random temp directory, re-check the
installer size and SHA-256 immediately before launch, and hold the verified installer handle
open while starting Setup so a local process cannot replace the file between download and run.

v1.1.25+ installer builds generate a `MetaQuestTrayTool-Setup-{version}.exe.sha256.txt`
sidecar. The Release workflow verifies that sidecar against the generated Setup.exe
before uploading both files to GitHub Releases.

The Release workflow installs a pinned Inno Setup upstream installer and verifies its SHA-256
digest before building Setup.exe. CI also runs CodeQL, Dependabot coverage, test coverage
collection, and locked `win-x64` restores so package-lock drift is reviewable.

Release installers are not currently Authenticode-signed. Windows SmartScreen may therefore
show an additional warning. Maintainers should sign release installers with an
organization-controlled Authenticode certificate and timestamp service when one is available;
the release workflow should verify the signature before publishing.

## Local data

Settings, profiles, and logs are stored under `%AppData%\MetaQuestTrayTool\`. Logs rotate at
approximately 5 MiB and redact labeled headset serial, fingerprint, and Wi-Fi SSID values.
Use **Clear log** before sharing diagnostics, and review an exported report for hardware,
installation-path, and network details.

The Info page can export a support ZIP. That ZIP uses an allowlisted settings summary instead
of raw `settings.json`, includes counts for profiles/custom commands instead of their text,
includes a short performance-diagnostics note, and applies extra redaction for user-profile
paths, IP endpoints, serial/fingerprint/SSID labels, and long device-like tokens. A trusted
headset serial passed into that diagnostics note is replaced before export. Review the ZIP
before sending it to anyone.

## Experimental headset properties

Quest 3 GPU level 7 and 144/207 Hz refresh requests are off by default. They write only
`debug.oculus.gpuLevel` or `debug.oculus.refreshRate` through a snapshot bound to one headset
serial. A missing battery temperature refuses GPU level 7, and so does a missing firmware
fingerprint. While an override owned by this app is applied, a bounded watch samples battery
temperature and restores that property if the reading is missing, stale, or at the gate, and
also when that watch is cancelled or the application exits. A failed restore stays recorded and
blocks automatic reapplication. The 45 °C battery gate is not a GPU junction limit and not a
universal hardware limit. They do not write `debug.oculus.forceDisplayScaling`, and they do not delete an
originally unset property. A later apply keeps the first original value. A readback is not a GPU
clock, an effective panel rate, or a delivered frame rate. The review is recorded in
`docs/research/SECURITY-REVIEW.md`.

## Performance overlay

The Performance page can open named pipe `MetaQuestTrayTool.PerformanceOverlay` for the current
user only. Administrators are not granted access. The pipe is labeled medium integrity and is
not a network listener. Each command must present the token created when the pipe starts, and a
message longer than 1024 bytes is rejected. Reads and writes time out after two seconds. The
command list is fixed. Text that asks for ADB, a shell, command chaining, or a profile change is
rejected. The tray does not load `openvr_api.dll`. The dashboard overlay runs in
`MetaQuestTrayTool.Overlay.exe`. The token is written to that process's standard input, not its
command line. An elevated tray starts the companion with the interactive Explorer token and does
not fall back to an elevated process. If SteamVR or the native library is missing, the companion
exits and the tray keeps running. That has not been accepted inside a headset. A process that
can read this tray's memory can still recover the token.

## Device and process actions

Custom ADB commands require a classified VR headset and, by default, explicit trust for that
headset. Update cleanup does not stop shared ADB servers or unrelated ADB clients. Overlay
cleanup first requests a normal close and, if needed, terminates only the named process rather
than its child process tree.

Report security issues privately through the repository's GitHub security-advisory process.
