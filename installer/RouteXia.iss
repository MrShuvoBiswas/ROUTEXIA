; ══════════════════════════════════════════════════════════════════════════════
; RouteXia - Production Inno Setup Installer Script
; Packages RouteXia x64 WPF client, WinDivert kernel drivers, and firewall rules
; ══════════════════════════════════════════════════════════════════════════════

#define MyAppName "RouteXia"
#ifndef MyAppVersion
#define MyAppVersion "1.0.7"
#endif
#define MyAppPublisher "RouteXia Inc."
#define MyAppURL "https://routexia.com"
#define MyAppExeName "RouteXia.exe"

[Setup]
; Unique application GUID
AppId={{8B5826A8-B60E-47BC-9BC9-A5EFDF3541D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/support
AppUpdatesURL={#MyAppURL}/updates

; Installation Directory (Asks user where to install)
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
UsePreviousAppDir=yes
DirExistsWarning=no

; Administrative elevation required for WinDivert kernel driver & firewall rules
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Modern Wizard UI Pages
WizardStyle=modern
WizardResizable=no
WizardSizePercent=100,100
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableFinishedPage=no

; Output settings
OutputDir=..\artifacts\installer
OutputBaseFilename=SetupRouteXia-v{#MyAppVersion}
SetupIconFile=..\client\RouteXia.App\Resources\Icons\RouteXia-AppIcon.ico
UninstallDisplayName={#MyAppName} Gaming Network Optimizer
UninstallDisplayIcon={app}\{#MyAppExeName},0

UsedUserAreasWarning=no

; Process closing
CloseApplications=yes
CloseApplicationsFilter=*RouteXia*.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Desktop Shortcut is CHECKED by default
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; Start with Windows is optional (unchecked by default)
Name: "startwithwindows"; Description: "Start RouteXia automatically on Windows startup"; GroupDescription: "Preferences:"; Flags: unchecked

[Files]
; Main application binaries (published self-contained x64)
Source: "..\client\RouteXia.App\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WinDivert native kernel driver and user-mode library
Source: "..\client\RouteXia.App\Native\WinDivert.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert64.sys"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert.dll"; DestDir: "{app}\Native"; Flags: ignoreversion
Source: "..\client\RouteXia.App\Native\WinDivert64.sys"; DestDir: "{app}\Native"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional Windows startup registration
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startwithwindows; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startwithwindows; Flags: uninsdeletevalue

; Windows App Paths (Allows running 'routexia' from Run dialog or command prompt)
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
; Clean any stale firewall rules first, then register new ones
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""RouteXia Gaming Optimizer"""; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""RouteXia Gaming Optimizer"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes"; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""RouteXia Gaming Optimizer"" dir=out action=allow program=""{app}\{#MyAppExeName}"" enable=yes"; Flags: runhidden

; Post-install launch option (Checked by default on Finish screen)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: runascurrentuser nowait postinstall skipifsilent

[UninstallRun]
; Clean up firewall rules on uninstall
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""RouteXia Gaming Optimizer"""; Flags: runhidden; RunOnceId: "DelFwRule"
; Stop and delete WinDivert driver service so files are completely unlocked
Filename: "sc.exe"; Parameters: "stop WinDivert"; Flags: runhidden; RunOnceId: "StopWinDivert"
Filename: "sc.exe"; Parameters: "delete WinDivert"; Flags: runhidden; RunOnceId: "DelWinDivert"
Filename: "sc.exe"; Parameters: "stop WinDivert14"; Flags: runhidden; RunOnceId: "StopWinDivert14"
Filename: "sc.exe"; Parameters: "delete WinDivert14"; Flags: runhidden; RunOnceId: "DelWinDivert14"

[UninstallDelete]
; Completely wipe application directory and any runtime-generated files (logs, databases, cache)
Type: filesandordirs; Name: "{app}"

[Code]
// Helper function to safely execute system commands synchronously
procedure RunCommandSilent(const FileName, Params: string);
var
  ResultCode: Integer;
begin
  Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Pre-installation cleanup: Close running RouteXia instances and release WinDivert service
function InitializeSetup(): Boolean;
begin
  // Terminate any running RouteXia processes silently
  RunCommandSilent('taskkill.exe', '/F /IM RouteXia.exe /T');
  // Stop WinDivert kernel driver service to ensure driver files are unlocked for replacement
  RunCommandSilent('sc.exe', 'stop WinDivert');
  RunCommandSilent('sc.exe', 'stop WinDivert14');
  Result := True;
end;

// Pre-uninstallation cleanup: Terminate processes before uninstall begins
function InitializeUninstall(): Boolean;
begin
  // Terminate any active RouteXia processes before removing files
  RunCommandSilent('taskkill.exe', '/F /IM RouteXia.exe /T');
  // Stop and remove WinDivert kernel driver service
  RunCommandSilent('sc.exe', 'stop WinDivert');
  RunCommandSilent('sc.exe', 'delete WinDivert');
  RunCommandSilent('sc.exe', 'stop WinDivert14');
  RunCommandSilent('sc.exe', 'delete WinDivert14');
  Result := True;
end;

// Post-uninstallation cleanup: Purge any user-level residual directories
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  LocalAppDataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    LocalAppDataDir := ExpandConstant('{localappdata}\RouteXia');
    if DirExists(LocalAppDataDir) then
    begin
      DelTree(LocalAppDataDir, True, True, True);
    end;
  end;
end;
