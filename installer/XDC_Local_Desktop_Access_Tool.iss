#define MyAppName "XDC Local Desktop Access Tool"
#define MyAppVersion "0.9.0-beta"
#define MyAppPublisher "David McDougall"
#define MyAppExeName "XdcLocalDesktopAccessTool.App.exe"
#define MyAppSourcePath "C:\Users\DITES\Documents\XDC_Local_Desktop_Access_Tool\src\XdcLocalDesktopAccessTool.App\bin\Release\net8.0-windows\win-x64\publish"
#define MyAppOutputPath "C:\Users\DITES\Documents\XDC_Local_Desktop_Access_Tool\release"
#define MyInstallerIcon "C:\Users\DITES\Documents\XDC_Local_Desktop_Access_Tool\src\XdcLocalDesktopAccessTool.App\Assets\Images\XDC_Icon_X.ico"

[Setup]
AppId={{D6B8E18B-8A0A-4F6A-9A91-5A61C9B54C21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

; Install to Program Files (admin required)
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

DisableProgramGroupPage=yes
OutputDir={#MyAppOutputPath}
OutputBaseFilename=XDC_Local_Desktop_Access_Tool_v0.9.0-beta_setup_X

Compression=lzma
SolidCompression=yes
WizardStyle=modern

ArchitecturesInstallIn64BitMode=x64compatible

; FIX (Admin)
PrivilegesRequired=admin

; Icons
SetupIconFile={#MyInstallerIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Optional polish (safe to include)
AppPublisherURL=https://github.com/DITES01/xdc-local-desktop-access-tool
AppSupportURL=https://github.com/DITES01/xdc-local-desktop-access-tool
AppUpdatesURL=https://github.com/DITES01/xdc-local-desktop-access-tool

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyAppSourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

; Desktop (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent