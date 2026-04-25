#define MyAppName      "WinScrobb"
#define MyAppVersion   "1.0.2"
#define MyAppPublisher "WinScrobb"
#define MyAppExeName   "WinScrobb.exe"
#define PublishDir     "publish"

[Setup]
AppId={{A3F7C1D2-8B4E-4F9A-BC3D-6E2F1A0D5C8B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=WinScrobb-Setup
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\logo.png";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\logosmall.png";   DestDir: "{app}"; Flags: ignoreversion
Source: "icon.ico";                      DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Registry]
; Run at startup — matches what the app writes so they stay in sync
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "WinScrobb"; ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the running process before uninstall
Filename: "taskkill.exe"; Parameters: "/f /im {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove the startup registry key on uninstall
    RegDeleteValue(HKCU,
      'Software\Microsoft\Windows\CurrentVersion\Run',
      'WinScrobb');
  end;
end;
