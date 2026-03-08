; Inno Setup script for HouseVictoria desktop app
; Requires Inno Setup 6 or later (https://jrsoftware.org/isinfo.php)

[Setup]
AppId={{C1F9C0C3-3D2A-4B3D-9A0E-2E5F4D9F8B10}}
AppName=HouseVictoria
AppVersion=1.0.0
AppPublisher=HouseVictoria
AppPublisherURL=https://example.com
DefaultDirName={autopf}\HouseVictoria
DefaultGroupName=HouseVictoria
DisableDirPage=no
DisableProgramGroupPage=no
OutputDir=.
OutputBaseFilename=HouseVictoriaSetup
Compression=lzma
SolidCompression=yes
SetupLogging=yes
UninstallDisplayIcon={app}\HouseVictoria.App.exe
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
DefaultTasks=createDesktopIcon

; Update this path if you change your publish output
; This assumes:
;   dotnet publish HouseVictoria.App -c Release -r win-x64 -o .\publish\HouseVictoria --self-contained false
SourceDir=..\publish\HouseVictoria

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "createDesktopIcon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; Copy all published files into the application directory
Source: "*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
; Start Menu shortcut
Name: "{group}\HouseVictoria"; Filename: "{app}\HouseVictoria.App.exe"; WorkingDir: "{app}"
; Desktop shortcut (controlled by task)
Name: "{commondesktop}\HouseVictoria"; Filename: "{app}\HouseVictoria.App.exe"; Tasks: createDesktopIcon; WorkingDir: "{app}"

[Run]
; Automatically run the app after install (optional)
Filename: "{app}\HouseVictoria.App.exe"; Description: "Launch HouseVictoria"; Flags: nowait postinstall skipifsilent

