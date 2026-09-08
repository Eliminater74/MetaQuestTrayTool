# Issue #4: Quest Link registry interoperability investigation

Date: 2026-09-08. Related report: [GitHub issue #4](https://github.com/Eliminater74/MetaQuestTrayTool/issues/4).

## Outcome and root causes

The tester's claim that bitrate remains unchanged after both ODT reopen and OVRService restart **was not reproduced on this installation**. The existing bitrate path and DWORD mapping worked in both directions. There is no evidence here to justify replacing Link registry handling with CLI commands, changing hives, or claiming a runtime-cache defect.

Three concrete synchronization defects were established:

1. **Sharpening conversion was wrong.** ODT writes Disabled = 1, Normal = 2, Quality = 3. The tray wrote 0, 1, 3, so requesting Normal selected Disabled in ODT. It also read DWORD 2 as Quality. Both directions now use 1/2/3. An absent override now reads Default rather than Disabled.
2. **A stale width alias hid ODT changes.** ODT wrote `EncodeWidth=2912`. With `EncodeResolutionWidth=3664` also present, ODT displayed 2912 while the old tray reader selected 3664. The tray now prefers an explicit `EncodeWidth`, including zero, and falls back to the legacy name only without a valid ODT DWORD. Dual writes/deletes remain for compatibility; this investigation does not establish runtime ownership of the legacy name.
3. **Read live did not refresh the controls.** The handler only updated a status line. It now loads the registry snapshot into the fields under the existing loading guard, clears the saved preset selection, and preserves numeric values outside the preset catalog (such as 450 Mbps). Reading does not save or apply anything; the next user edit applies the loaded fields. Reopening the page still loads saved settings, as before.

The apply path also had a verification defect: it read back values but always reported success after successful registry calls, without checking equality. It now checks each intended write/deletion, including both width values and DWORD type, through a fresh read handle. A mismatch reports the requested and observed values, clears `LastApplied`, and returns failure. An access failure cannot trigger another unhandled read from the catch block. Partial writes are possible and reported; no speculative rollback overwrites concurrent ODT changes.

## Installed software and method

Installation discovery followed `OculusRuntimeService.DetectInstallPath`: query the configured `Base`/`InitialInstallDir` in the two existing Oculus installation registry locations, then use `Support/oculus-diagnostics`. No machine-specific installation path was added to the application.

The tested `OculusDebugTool.exe` SHA-256 was:

```text
8F335C120E5F4C775752899203FD27790FC4DDB4D1054FB5147182DA195EEF0A
```

Its Windows FileVersion/ProductVersion fields were empty; the digest identifies the tested binary without inventing a Meta release number. OVRService was running, and ODT was not running before the investigation. The tested Link value names were initially absent in HKCU, and the HKLM RemoteHeadset key was absent in both registry views.

ODT was opened only for diagnostic observation and individual control changes. Before/after registry reads were compared with reopened ODT, and the rebuilt application's actual `LinkSettingsService` was then instantiated from the Release assembly for apply/read checks. No GUI automation was added to the product. No new CLI command or service property was inferred from binary strings.

## Setting audit

All observed values below are DWORDs in `HKCU\Software\Oculus\RemoteHeadset`. External-write checks establish what reopened ODT displays; they do not measure the encoder or headset.

| Setting | Observed evidence | Remaining scope |
| --- | --- | --- |
| Encode Bitrate | ODT 450 wrote `BitrateMbps=450`; external 500 appeared in reopened ODT. Rebuilt service Apply(500) also appeared as 500. ODT then changed it to 450 and rebuilt service ReadCurrent returned 450. | Tester-specific failure, service-restart survival, and actual stream bitrate not established. |
| Encode Width | ODT 2912 wrote `EncodeWidth=2912`, leaving the legacy alias absent. With legacy alias 3664 added, ODT still showed 2912. Rebuilt service also read 2912 and its dual write appeared correctly in reopened ODT. | Runtime use of `EncodeResolutionWidth` is unverified. |
| Dynamic Bitrate | External `DBR=1` displayed Enabled; deletion displayed Default. | Disabled=0 retained and unit tested, not independently selected in ODT during this run. |
| Dynamic Bitrate Max | External `DBRMax=350` displayed 350; deletion displayed 0. | Stream ceiling/restart requirements unverified. |
| Dynamic Bitrate Offset | External `DBROffsetMbps=25` displayed 25; deletion displayed 0. | Runtime effect and negative-offset behavior not audited. |
| HEVC | External `HEVC=1` displayed H.265; deletion displayed Default. | Other explicit codecs and actual codec negotiation not tested; existing boolean preference retained. |
| Sliced Encoding | External `NumSlices=1` displayed Off; deletion displayed Default. | No stream/latency test; lowercase spelling is the same registry value. |
| Link Sharpening | External 3 displayed Quality. Selecting Disabled in ODT wrote 1; selecting Normal wrote 2. Rebuilt service Normal wrote 2 and reopened ODT displayed Normal. | Absent value displayed Normal on this installation; tray reports absence as Default because it represents overrides. |
| Distortion Curvature | External `DistortionCurve=0` displayed Low; deletion displayed Default. | High=1 retained and unit tested; visual distortion untested. |
| Mobile ASW | Existing `MobileASWMode` conversion retained; mapping/deletion exercised through isolated tests. | Not exposed in this ODT UI; no observed runtime or GUI effect. |

The initial external checks used PowerShell writes of only these owned values. The rebuilt-service check subsequently applied 500 Mbps, width 2912, Normal sharpening, and defaults for the other controls; ODT showed the expected values after reopening. The reverse bitrate check used the same rebuilt service to read ODT's change to 450.

## Persistence and limitations

- Closing/reopening ODT preserved and displayed the tested values without restarting OVRService.
- `Restart-Service OVRService` was attempted, but Windows denied access to the service in this non-elevated session. The service was not restarted. Service restart, Link reconnect, and reboot persistence are not claimed as verified.
- No active headset stream was measured. Registry verification is explicitly labeled separately from runtime/ODT verification in application results and UI status.
- Original local overrides were restored after testing: all eleven owned registry value names were absent again in both HKCU views. Unrelated Meta values and pairing subkeys were not modified. The diagnostic ODT process was closed.
- Saved JSON settings and enum representations were not migrated or changed. No Meta-installation requirement was added to ordinary tests; missing keys return default overrides and access failures are reported.
- Existing `VrSessionCapabilities`/`LinkConnection` guards remain at the page and application owners, including profile, global/default, startup, and direct applies. Steam Link and Virtual Desktop still block both Meta live pipelines. Game Settings CLI behavior, Exit hotkey behavior, and administrator startup mechanics are unchanged.
- The secondary tray wording change says **Start with Windows as Administrator (hands-free)**; **Restart as Administrator...** remains the current-session command.
- No commit claims to close issue #4. To investigate the remaining bitrate report, capture exact before/after values and types on the affected machine with [Get-LinkRegistrySnapshot.ps1](../../scripts/Get-LinkRegistrySnapshot.ps1), confirm the account running each tool, record its ODT version/hash, and preserve the tray's apply/skip/error status. Account differences, capability skips, and Meta-version differences are possibilities, not established causes.

## Files and validation

Product changes are confined to `LinkSettingsService`, the `LinkSettingsRegistry` platform seam and write verifier, `LinkApplyResult`, Quest Link page refresh/status, the legacy Link window status, application result logging, and the secondary tray label. Documentation includes this report, the corrected registry map, index links, and a read-only snapshot script.

`LinkSettingsTests.cs` adds 21 cases: the observed sharpening mapping, stale-width conflict and explicit zero, legacy-width fallback, missing defaults, every exposed setting's mapping, writes/read-back, delete/default semantics, case-insensitive slice naming, concurrent overwrite/wrong type/failed deletion/alias mismatch, access failures, and both Meta/non-Meta capability decisions. Ordinary tests use an in-memory registry implementation, not Meta software or production registry values. Existing tests remain intact.

Validation commands:

```powershell
dotnet restore .\MetaQuestTrayTool.sln -r win-x64 --locked-mode
dotnet build .\MetaQuestTrayTool.sln -c Release --no-restore
dotnet test .\MetaQuestTrayTool.sln -c Release --no-build --no-restore
git diff --check
```

The complete solution built with **0 warnings / 0 errors**. The full suite passed **75/75**, with no skips. The snapshot script produced four hive/view records with eleven whitelisted names each; both HKCU views showed 450 during the reverse check, and both HKLM views reported the key absent. After restoration, both HKCU views showed all owned names absent.

The tray page's loading guard and event routing were reviewed in source; the full tray-page interaction was not driven on a physical headset. The ODT interoperability evidence above comes from the actual rebuilt service and installed ODT. No installer, release, version bump, tag, push, or issue comment was performed.
