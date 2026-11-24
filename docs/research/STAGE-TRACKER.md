# Stage tracker

Starting HEAD: `52d961bb5a456f8cd44313a5fbec266cfe789704`.

| Stage | Status | Commit | Host verification |
| --- | --- | --- | --- |
| 0 Baseline audit | Complete | `6f08835712b32eb5c77c55a798eb768dff5fbc6d` | Release build, 231 passed. No headset. |
| 1 Capability discovery | Complete | `379f9654c433299d3e7b0b9158d200558872be53` | `HeadsetCapabilityDiscoveryTests`: 21 passed. |
| 2 ADB property transactions | Complete | `474a5c5abdb87e146cc48afbfded710acdebff84` | `AdbPropertyTransactionTests`: 7 passed. |
| 3 Experimental GPU level 7 | Complete | `eea8d175b095c96316f705f5b1384f85202d5a8f` | Filtered GPU and override tests: 20 passed. |
| 4a Headset telemetry | Complete in this commit | this commit | Headset telemetry and existing performance parser tests: 8 passed. |
| 4b PC telemetry | Pending | | |
| 4c Streaming telemetry | Pending | | |
| 5 Dashboard and analyzer | Pending | | |
| 6 Benchmarks | Pending | | |
| 7 Experimental refresh | Pending | | |
| 8 Overlay IPC | Pending | | |
| 9 Provider model | Pending | | |
| 10 ALVR | Pending | | |
| 11 Pyrowave | Pending | | |
| 12 Profiles | Pending | | |
| 13 Diagnostics | Pending | | |
| 14 Hardening | Pending | | |
| 15 Final review | Pending | | |

Physical headset acceptance is outstanding for every experimental control. See `HARDWARE-ACCEPTANCE.md`.
