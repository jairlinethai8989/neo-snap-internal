#define SourceRoot "..\dist\publish"
[Setup]
AppId={{37B63537-46B0-4D51-A9C0-619BE83FE608}
AppName=SnapCraft Capture
AppVersion=0.1.1
AppPublisher=SnapCraft
DefaultDirName={localappdata}\Programs\SnapCraft
DefaultGroupName=SnapCraft
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=SnapCraft-Windows-Setup-0.1.1
SetupIconFile=..\src\SnapCraft\Assets\icons\app.ico
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SnapCraft"; Filename: "{app}\SnapCraft.exe"
Name: "{autodesktop}\SnapCraft"; Filename: "{app}\SnapCraft.exe"

[Run]
Filename: "{app}\SnapCraft.exe"; Description: "Open SnapCraft"; Flags: nowait postinstall skipifsilent
