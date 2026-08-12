# Bundled Android Platform-Tools (ADB)

Official Google **platform-tools 37.0.1** for Windows, so Headset debug / `debug.oculus.*` works without Android Studio or SideQuest.

| File | Why |
| --- | --- |
| `adb.exe` | Android Debug Bridge |
| `AdbWinApi.dll` / `AdbWinUsbApi.dll` | Required Windows ADB backends |
| `libwinpthread-1.dll` | Runtime dependency of this ADB build |
| `NOTICE.txt` | Google / Android third-party notices |
| `source.properties` | Package revision |

Source: https://dl.google.com/android/repository/platform-tools-latest-windows.zip

These files are © Google and licensed under the Android SDK terms. We only ship the pieces ADB needs to talk to a Quest.
