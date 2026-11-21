; Inno Setup Script for BT Controller Windows 11
#define MyAppName "Bluetooth Controller"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BTController"
#define MyAppURL "https://github.com/pastur253/BTController"
#define MyAppExeName "BTController.App.exe"

[Setup]
AppId={{B7E209E3-4028-4822-8356-9A0191B95B11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\BTController
DisableProgramGroupPage=yes
OutputDir=artifacts\inno_setup
OutputBaseFilename=BTController-InnoSetup
SetupIconFile=BTController.Installer\installer.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\btcontroller"; ValueType: string; ValueData: "URL:Bluetooth Controller Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\btcontroller"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\btcontroller\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
