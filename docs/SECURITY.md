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
and applies extra redaction for user-profile paths, IP endpoints, serial/fingerprint/SSID
labels, and long device-like tokens. Review the ZIP before sending it to anyone.

## Device and process actions

Custom ADB commands require a classified VR headset and, by default, explicit trust for that
headset. Update cleanup does not stop shared ADB servers or unrelated ADB clients. Overlay
cleanup first requests a normal close and, if needed, terminates only the named process rather
than its child process tree.

Report security issues privately through the repository's GitHub security-advisory process.
