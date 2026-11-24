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
| 14 Full regression | this documentation commit records it | 312 passed, 0 failed. No separate repair was required. |
| 15 Documentation | this commit | Version number was not changed. Installer was not rebuilt. |

Hardware acceptance remains outstanding. See `HARDWARE-ACCEPTANCE.md`.
