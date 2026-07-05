; Meta Search and Control Center - Inno Setup Script
; Mit Inno Setup 6 kompilieren: https://jrsoftware.org/isinfo.php
; iscc setup.iss

#define AppName "Meta Search and Control Center"
#define AppExe "MSCC.exe"
#define AppVersion "1.0.0"
#define AppPublisher "Dennis Michael Heine"
#define AppURL "https://github.com/decipher2k/Meta-Search-and-Control-Center"
#define SourceDir "bin\Debug\net10.0-windows"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\MSCC
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputBaseFilename=MSCC-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\app-icon.ico
SetupIconFile=app-icon.ico
LicenseFile=LICENSE.txt

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"; GroupDescription: "Weitere Verknuepfungen:"

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion signonce
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\cs\*"; DestDir: "{app}\cs"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\de\*"; DestDir: "{app}\de"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\es\*"; DestDir: "{app}\es"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\fr\*"; DestDir: "{app}\fr"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\it\*"; DestDir: "{app}\it"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\ko\*"; DestDir: "{app}\ko"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\pl\*"; DestDir: "{app}\pl"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\ru\*"; DestDir: "{app}\ru"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\tr\*"; DestDir: "{app}\tr"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\zh-Hans\*"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\zh-Hant\*"; DestDir: "{app}\zh-Hant"; Flags: ignoreversion recursesubdirs
Source: "{#SourceDir}\MSCC.exe.WebView2\*"; DestDir: "{app}\MSCC.exe.WebView2"; Flags: ignoreversion recursesubdirs
; Alle weiteren Ordner und Dateien einschliessen
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\app-icon.ico"
Name: "{group}\{#AppName} deinstallieren"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\app-icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  Result := True;
end;
