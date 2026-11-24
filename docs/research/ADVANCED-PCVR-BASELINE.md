# Advanced PCVR baseline

Audit date: 2026-11-24.

MetaQuestTrayTool starting HEAD: `52d961bb5a456f8cd44313a5fbec266cfe789704` (`release: prepare v1.1.37`) on `main`. Working tree was clean. No developer changes were present to preserve.

Quest3-Pyrowave research commit: `1b529ef54b4f7b34a44c28ad9a54c6c4ac6887a7` (main, 2026-10-09, "Merge PR #25: first run (.136)"). Docs and `tools/quest3/gpu_level.py` were read from that commit. Pyrowave source was not vendored into this repository.

This is a host-side architecture and feature audit. It is not a physical headset test and it does not treat Pyrowave's short-screen measurements as Quest Link results. Later implementation status is in `STAGE-TRACKER.md`. This file stays the starting audit.

## What each project is

MetaQuestTrayTool is a native Windows tray utility (.NET 8, WPF) that manages Meta Quest Link, SteamVR, OpenXR, per-game profiles, audio, power, and trusted-headset ADB. It does not encode or transport video.

Quest3-Pyrowave is an ALVR-derived streaming stack (custom Quest APK `io.github.jms1717.quest3pyrowave`, custom SteamVR driver, GPU wavelet codec, Vulkan decode). Its GPU and refresh research is useful. Its codec, USB transport, and driver are not settings this tray can turn on inside Quest Link.

ALVR itself is the upstream of Pyrowave. This tray may detect and manage an installed ALVR or Pyrowave server. It must not redistribute their binaries and must not fork their streaming engine.

## Existing headset controls (verified in source)

| Area | Current behavior | Source |
| --- | --- | --- |
| CPU and GPU levels | `HeadsetPerformanceLevel` is App default or 0–4. Level 5 throws. | `HeadsetSettings.cs`, `HeadsetSettingsService.PerformanceOverrides`, `HeadsetOverrideTests` |
| Refresh | Enum stops at 120 Hz. Choices come from the model name. | `HeadsetCapabilities.RefreshRates` |
| Quest 3 vs Quest 3S | `"Quest 3"` is a substring of `"Quest 3S"`, so both receive the same standard 72/80/90/120 list. That is intentional for standard rates and unsafe for undocumented Quest 3-only modes. | `HeadsetCapabilities` |
| Apply path | Writes `debug.oculus.gpuLevel`, `debug.oculus.cpuLevel`, `debug.oculus.refreshRate`, texture, foveation, and capture props after a trusted-headset check. | `HeadsetSettingsService.Apply` |
| Reset | Documented capture defaults can be rewritten. CPU/GPU, foveation, chroma, local dimming, and subsampled foveation are explicitly not cleared because no safe clear is known. The UI says a reboot is required. | `DocumentedDefaultOverrides` |
| Performance sample | Logcat `VrApi` lines. Last and average FPS, CPU/GPU level and utilization, foveation, SpaceWarp. No clocks, thermals, or frame-time percentiles. Duration is 1–30 seconds and does not clear logcat history. | `HeadsetPerformanceSample` |
| ADB lifecycle | Automatic ADB only after a confirmed PCVR session. Manual actions are explicit. Idle tray does not poll ADB. Session end does not kill the shared ADB server. Other Android devices are not disconnected. | `AdbSessionGate`, `AdbService` |

## Pyrowave findings that are usable from Windows

Verified in `tools/quest3/gpu_level.py` and `docs/HIGH-REFRESH.md` at the research commit:

- Property: `debug.oculus.gpuLevel`.
- Their tool accepts integers 0–7, then refuses any model other than `ro.product.model == "Quest 3"`.
- They describe level 4 as a 640 MHz governor cap and level 7 as a 690 MHz pin on the Adreno 740 in that test environment. Intermediate levels 5 and 6 are accepted by the script and are **not** independently documented as distinct clocks.
- Enable saves `{device serial, previous getprop}` and refuses to overwrite an existing rollback file. Restore refuses a different serial. A rejected write attempts to put the original value back.
- Their note: thermal protection still throttles. Sustained thermals were not measured. Short screens stayed about 35–41 °C.
- One cited cell (207 Hz, CDF 5/3, 700 Mbps, their stack) moved from about 177.3 FPS at level 4 to about 192 FPS at level 7. That is a short Pyrowave screen, not a Quest Link measurement.

Panel notes from the same document:

| Rate | What they observed | Safe for this tray to write |
| --- | --- | --- |
| 72, 90, 120 | Standard consumer modes | Already supported for Quest 3 / 3S |
| 144, 207 | Native panel mode they list as 4128×2208 (2064×2208 per eye) | Experimental property write only, Quest 3, opt-in, snapshot first. Does not make Quest Link deliver that frame rate. |
| 240 | Scaled panel 3104×1664 via `debug.oculus.forceDisplayScaling=1`. Their runtime still refused a plain 240 request until their own client confirmed the running mode. | Research only. This tray will not write the scaling property. Restoration needs their awake-mode / kernel DSI check, which an external utility cannot honestly claim. |
| 165, 180, 200 | Not established as panel modes in the researched document | Not exposed |

`debug.q3pw.*` properties belong to the Pyrowave client. They are not Quest Link features.

## What cannot be imported

Wavelet codecs (Haar, CDF 5/3, CDF 9/7), Vulkan compute decode, four parallel USB video connections, D3D11/Vulkan interop, and direct eye-swapchain rendering require their APK and driver. License review: do not copy their implementation. Research conclusions may be cited. Their reported fresh-FPS figures stay labeled as Pyrowave short screens.

## Proposed architecture

Keep the current tray, profile, Link, and ADB lifecycle. Add a side subsystem:

1. **Capability report.** Model fallback plus optional ADB snapshot. Explicit states: verified, documented model, experimental, unsupported, unknown, temporarily unavailable. Quest 3 and Quest 3S stay distinct for experimental features.
2. **Property transactions.** Snapshot, apply, read back, restore. Refuse another headset's snapshot. Record when a property cannot be cleared without reboot.
3. **Experimental GPU level 7.** Separate from levels 0–4. Off by default. Quest 3 only. Refuse at a conservative battery-temperature gate. Property readback is not a clock measurement.
4. **Telemetry.** Extend the log parser. PC metrics degrade when a vendor tool is absent. Every metric has source, unit, time, and measured/estimated/inferred.
5. **Bottleneck analyzer.** Advisory rules with evidence. No automatic setting changes.
6. **Benchmarks.** Durations and statistics. 1% lows only from frame-level data. A/B restores settings and will not restart SteamVR or kill a game unless a future explicit authorization path says so. The implementation refuses interruption instead.
7. **High refresh.** 144 and 207 may be requested as headset debug properties. 240 and unlisted rates are not applied.
8. **Overlay.** Secure local pipe with an allow-listed command set. Desktop companion window. In-headset OpenVR dashboard overlay is not loaded: an untested `openvr_api.dll` binding can destabilize SteamVR, and no headset was available to prove it.
9. **Providers.** Detection and cautious config read/write for ALVR and Pyrowave beside the existing Meta Link, Steam Link, and Virtual Desktop classification. Unknown JSON is preserved. Files are not edited while the server is running unless the caller explicitly overrides that refusal. Drivers are not unregistered.
10. **Profiles.** Optional provider preferences. They never launch a streaming stack by themselves.

## Risks

- `setprop` cannot reliably delete an absent property without root. Restoration of an originally unset property stays incomplete until reboot.
- `getprop` returns empty for both absent and empty. Those two states are recorded as one.
- Quest Link will not automatically follow an experimental panel rate.
- GPU level 7 and 144/207 Hz are hardware-verification pending.
- ALVR `session.json` schemas change. Writes are limited to numeric values that already exist at the addressed path.
- Named-pipe overlay IPC must stay current-user / administrator ACL, matching `SessionHelperHost`, and must reject raw ADB or process launch text.

## Verification strategy

Host-side unit tests use fixtures and fakes. They are not headset acceptance. Physical checks are listed in `HARDWARE-ACCEPTANCE.md`. Baseline build and `dotnet test` are recorded in `STAGE-TRACKER.md` when this audit commit is made.
