; WinBox Windows installer (Inno Setup 6).
; Built by scripts/dist.ps1 — portable zip remains a separate artifact.
;
; Defines (optional overrides from ISCC /D):
;   MyAppVersion, SourceDir, OutputDir, MyAppRuntime, SetupIcon

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppRuntime
  #define MyAppRuntime "win-x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\dist"
#endif
#ifndef SetupIcon
  #define SetupIcon "..\src\WinBox.Host\Assets\winbox.ico"
#endif

#define MyAppName "WinBox"
#define MyAppPublisher "WinBox"
#define MyAppExeName "WinBox.Host.exe"
#define MyAppURL "https://github.com/hitzhangjie/winbox"

[Setup]
AppId={{8F3C2A91-6B4E-4D7A-9C1F-2E5B8A0D4F73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=WinBox-{#MyAppVersion}-{#MyAppRuntime}-setup
SetupIconFile={#SetupIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11 (build 22000+) — matches documented product support.
MinVersion=10.0.22000
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
