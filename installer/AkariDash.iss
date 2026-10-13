; Inno Setup script for Akari Dash. Build it with installer\Build-Installer.ps1, which publishes the
; Release build to artifacts\publish first and passes the version in:
;   ISCC.exe /DAppVersion=1.0.0 installer\AkariDash.iss
;
; Uninstalling leaves C:\ProgramData\Akari-Dash alone on purpose: it holds the Original Values that
; Undo needs, so a reinstall can still put back what an earlier install changed.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "Akari Dash"
#define AppExe "Akari Dash.exe"
#define PublishDir "..\artifacts\publish"

[Setup]
; Never change the AppId: Windows uses it to recognise an install as the same app on upgrade.
AppId={{DBBF442B-1318-4A6E-B16E-DBC6B2B68720}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=isleap9
AppPublisherURL=https://github.com/isleap9/Akari-Dash
AppSupportURL=https://github.com/isleap9/Akari-Dash/issues
AppUpdatesURL=https://github.com/isleap9/Akari-Dash/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
; Akari Dash runs as administrator anyway, so it installs for all users under Program Files.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=AkariDash-Setup-{#AppVersion}
SetupIconFile=..\src\AkariDash.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
