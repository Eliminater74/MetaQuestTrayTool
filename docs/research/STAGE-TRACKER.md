# Stage tracker

Starting HEAD: `52d961bb5a456f8cd44313a5fbec266cfe789704` (`release: prepare v1.1.37`).

Final verification on 2026-11-24, before the documentation commit:

- Release `dotnet test MetaQuestTrayTool.sln -c Release -r win-x64 --no-restore`: 312 passed, 0 failed, 0 skipped.
- Debug build of the app project: succeeded, 0 warnings.
- Release build: succeeded as part of the test run.
- Self-contained publish smoke to `publish/local-smoke` produced `MetaQuestTrayTool.exe`. That directory is local output and is not part of the commit.
- Installer was not rebuilt. The public version was not changed.
- No physical headset, ALVR server, or Pyrowave install was exercised.

| Stage | Commit | Host verification |
| --- | --- | --- |
| 0 Baseline | `6f08835712b32eb5c77c55a798eb768dff5fbc6d` | 231 passed before new tests. |
| 1 Capability discovery | `379f9654c433299d3e7b0b9158d200558872be53` | 21 capability tests. |
| 2 Property transactions | `474a5c5abdb87e146cc48afbfded710acdebff84` | 7 transaction tests. |
| 3 GPU level 7 | `eea8d175b095c96316f705f5b1384f85202d5a8f` | 20 GPU and override tests. |
| 4 Headset telemetry | `b98317f2f93e432bb0afffefe3449cb5cb18d073` | 8 parser tests. |
| 4 PC and streaming telemetry | `8b78ebf3610af9738f8c0ab213fca609c222466b` | 9 probe tests. |
| 5 Dashboard and analyzer | `e7f42fb92983dd268ba2c29f1d788214b2630ebb` | 15 analyzer and override tests. UI build succeeded. |
| 6 Benchmarks | `4a8cb1b058c7c78c26e6e2ea42be73d9c9595ebc` | 7 benchmark tests. |
| 7 Experimental refresh | `2977d8ae0ad86d1a8b992eadc6486dc1eab7b882` | 19 refresh and override tests. Scaling property is not written. |
| 8 Overlay pipe | `01a1c8c31eff090a00c95dbfd225df6aca220f20` | Release build. OpenVR overlay not loaded. |
| 9–13 Providers, ALVR, Pyrowave, profiles, diagnostics | `40e373e90db3d3658206755388f73ff2ca5e5b6f` | 13 integration tests. |
| 14 Full regression | `866b54ba80cf22a661139b7938b934dbf9a11d07` | 312 passed. That result was not a security review. |
| 15 Documentation | `866b54ba80cf22a661139b7938b934dbf9a11d07` | Version number was not changed. |

## Independent review

Review start: `866b54ba80cf22a661139b7938b934dbf9a11d07`.

| Review stage | Commit | What changed |
| --- | --- | --- |
| B Recovery | `2d560a3966f34cf8a40e67ad3fa49661c2599e6d` | A later apply keeps the first original. A fingerprint change writes nothing. |
| C GPU and refresh | `c130dd498b5478fbbb4bf90dc84e79da433af5c2` | Missing battery temperature refuses GPU level 7. Panel rate is parsed separately from property readback. |
| E Benchmarks | `2e180485f4c3077534685dec5bbfb7ab4608d595` | Timed 30/60/300 second sessions, warm-up, cancel, history, and comparison guards. |
| F Overlay pipe | `cb44381fa7851eeed124516d1ecb9d78099a1a45` | Per-process token and 1024-byte read limit. OpenVR stays unloaded. |
| G ALVR | `75970268cbb5fede1d809eaffe405ede0ee24052` | Generic numeric editor removed. One allowlisted plain number. No running-server override. |
| H Pyrowave | `8ab22c882b2152bbbbb197aee0152c79ecd8a4b7` | Settings are reported. Unverified schema paths are not written. |
| I Profiles | `66c1a5449a58c8ebb152442535cce8072e136b40` | Preferences are evaluated on apply and do not launch a provider. |
| D Dashboard | `c901c2223565c4c391da1324927d5f7cf2da165e` | Source labels, CSV/JSON export, and PC benchmark controls. |
| J Security review | `a5426af7283cf6d18470972155f789e7738b13e1` | Review notes and regression tests. See `SECURITY-REVIEW.md`. |

Final host verification after those commits:

- `dotnet restore MetaQuestTrayTool.sln -r win-x64 --locked-mode`
- Debug build: succeeded, 0 warnings.
- Release build: succeeded, 0 warnings.
- `dotnet test MetaQuestTrayTool.sln -c Release -r win-x64 --no-restore --collect:"XPlat Code Coverage"`: 332 passed, 0 failed, 0 skipped.
- `scripts/Test-Coverage.ps1`: line coverage 15.96%, floor 8%.
- Publish smoke wrote `publish/local-smoke/MetaQuestTrayTool.exe`.
- `scripts/build-installer.ps1` wrote `dist/MetaQuestTrayTool-Setup-1.1.37.exe` (53.3 MB). It was not published and the public version was not changed.
- No physical headset, ALVR server, or Pyrowave install was used.

Hardware acceptance remains outstanding. See `HARDWARE-ACCEPTANCE.md`.
