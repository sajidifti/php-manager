#define AppName "PHP Manager"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "Sifti"
#define AppExeName "PhpManager.exe"

[Setup]
AppId={{E02315DC-A815-4D2E-A923-17A39643DF39}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\PHP Manager
DefaultGroupName=PHP Manager
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=PHP-Manager-Setup-{#AppVersion}
SetupIconFile=..\PhpManager\Assets\php.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start PHP Manager when I sign in"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PHP Manager"; Filename: "{app}\{#AppExeName}"
Name: "{userdesktop}\PHP Manager"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PHP Manager"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start PHP Manager"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExeName} /F >nul 2>&1"; Flags: runhidden; RunOnceId: "StopPhpManager"
