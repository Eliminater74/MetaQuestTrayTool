; Meta Quest Tray Tool — Inno Setup script (OTT-style Setup.exe)
; Built by scripts/build-installer.ps1 after a self-contained publish.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\publish\win-x64"
#endif

#define MyAppName "Meta Quest Tray Tool"
#define MyAppPublisher "Eliminater74"
#define MyAppExeName "MetaQuestTrayTool.exe"
#define MyAppUrl "https://github.com/Eliminater74/MetaQuestTrayTool"
#define MyAppId "{{A7C3E91F-4B2D-4F8A-9E15-6D0C2B8A1F44}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\installer\LICENSE.txt
; Generated from CHANGELOG.md by scripts/build-installer.ps1 (see what's new before install).
InfoBeforeFile=..\installer\WHATSNEW.txt
OutputDir=..\dist
OutputBaseFilename=MetaQuestTrayTool-Setup-{#MyAppVersion}
SetupIconFile=..\src\MetaQuestTrayTool\Resources\Icons\App.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
CloseApplications=yes
CloseApplicationsFilter=MetaQuestTrayTool.exe
RestartApplications=no
AllowNoIcons=yes
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCopyright=Copyright (C) Eliminater74

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Self-contained publish folder (includes .NET runtime + bundled ADB platform-tools)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "Quest Link / SteamVR tray settings"; Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leave %AppData%\MetaQuestTrayTool settings/profiles alone on uninstall.
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Stop the tray before replacing installed files. Shared ADB sessions are not touched.
  Exec('taskkill.exe', '/IM MetaQuestTrayTool.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MetaQuestTrayTool');
    Exec('schtasks.exe', '/Delete /TN "MetaQuestTrayTool" /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/IM MetaQuestTrayTool.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
