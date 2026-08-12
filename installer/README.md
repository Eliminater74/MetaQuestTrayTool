# Installer (OTT-style Setup.exe)

Users double-click **`MetaQuestTrayTool-Setup-x.y.z.exe`** and get a normal Windows install (Program Files, Start Menu, optional Desktop shortcut, Uninstall entry) — same idea as classic Oculus Tray Tool.

## What the installer includes

- Self-contained **win-x64** build (no separate .NET install for end users)
- Bundled Google **platform-tools** ADB
- App icon, Start Menu shortcut, optional Desktop icon
- Stops a running tray instance before upgrade/uninstall
- Leaves `%AppData%\MetaQuestTrayTool` settings/profiles alone on uninstall

## Build the Setup.exe (maintainer)

### 1. Install Inno Setup 6 (once)

```powershell
winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
# or: choco install innosetup -y
```

### 2. Run the build script

From the repo root:

```powershell
.\scripts\build-installer.ps1
```

Output:

- Published app: `publish\win-x64\`
- Installer: `dist\MetaQuestTrayTool-Setup-<version>.exe`

Optional flags:

```powershell
.\scripts\build-installer.ps1 -SkipPublish   # reuse last publish\
.\scripts\build-installer.ps1 -Version 1.0.1 # override Directory.Build.props version
```

### Manual steps (same as the script)

```powershell
dotnet publish .\src\MetaQuestTrayTool\MetaQuestTrayTool.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=true `
  -o .\publish\win-x64

& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" `
  /DMyAppVersion=1.0.0 `
  /DPublishDir=..\publish\win-x64 `
  .\installer\MetaQuestTrayTool.iss
```

## Files

| File | Purpose |
| --- | --- |
| `MetaQuestTrayTool.iss` | Inno Setup script |
| `LICENSE.txt` | Shown on the license page |
| `WELCOME.txt` | Info page before install |
| `../scripts/build-installer.ps1` | One-shot publish + compile |
