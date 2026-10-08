#define SourceRoot "..\dist\publish-0.1.21"
[Setup]
AppId={{37B63537-46B0-4D51-A9C0-619BE83FE608}
AppName=Neo Snap
AppVersion=0.1.21
AppPublisher=jairlinethai
DefaultDirName={localappdata}\Programs\SnapCraft
DefaultGroupName=Neo Snap
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Neo-Snap-Windows-Setup-0.1.21
SetupIconFile=..\src\SnapCraft\Assets\icons\app.ico
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "shortcuts"; Description: "Create Desktop and Start menu shortcuts"

[Icons]
Name: "{autoprograms}\Neo Snap"; Filename: "{app}\SnapCraft.exe"; IconFilename: "{app}\Assets\icons\NeoSnap-0.1.21.ico"; Tasks: shortcuts
Name: "{autodesktop}\Neo Snap"; Filename: "{app}\SnapCraft.exe"; IconFilename: "{app}\Assets\icons\NeoSnap-0.1.21.ico"; Tasks: shortcuts

[Run]
Filename: "{app}\SnapCraft.exe"; Description: "Open Neo Snap"; Flags: nowait postinstall skipifsilent

[Code]
procedure SHChangeNotify(EventId: Integer; Flags: Cardinal; Item1, Item2: Integer);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    SHChangeNotify($08000000, 0, 0, 0);
end;
