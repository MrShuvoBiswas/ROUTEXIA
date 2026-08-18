; ══════════════════════════════════════════════════════════════════════════════
; RouteXia - Production Inno Setup Installer Script
; Packages RouteXia x64 WPF client, WinDivert kernel drivers, and firewall rules
; ══════════════════════════════════════════════════════════════════════════════

#define MyAppName "RouteXia"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "RouteXia Inc."
#define MyAppURL "https://routexia.com"
#define MyAppExeName "RouteXia.exe"

[Setup]
; Unique application GUID
AppId={{8B5826A8-B60E-47BC-9BC9-A5EFDF3541D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; WinDivert kernel drivers require administrative elevation during execution & install
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
OutputDir=..\artifacts\installer
OutputBaseFilename=RouteXia-Setup-v{#MyAppVersion}
SetupIconFile=..\client\RouteXia.App\Resources\Icons\RouteXia-AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startwithwindows"; Description: "Start RouteXia automatically on Windows startup"; GroupDescription: "Preferences:"; Flags: unchecked

[Files]
; Main application binaries (published self-contained / framework-dependent)
Source: "..\client\RouteXia.App\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WinDivert native kernel driver and user-mode library
Source: "..\client\RouteXia.App\Native\WinDivert.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert64.sys"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert.dll"; DestDir: "{app}\Native"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert64.sys"; DestDir: "{app}\Native"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional Windows startup registration
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startwithwindows; Flags: uninsdeletevalue

[Run]
; Register Windows Firewall rules for kernel packet diversion
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""RouteXia Gaming Optimizer"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes"; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""RouteXia Gaming Optimizer"" dir=out action=allow program=""{app}\{#MyAppExeName}"" enable=yes"; Flags: runhidden

; Post-install launch option
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Clean up firewall rules on uninstall
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""RouteXia Gaming Optimizer"""; Flags: runhidden

[Code]
// Custom Inno Setup Pascal code for safety checks
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
