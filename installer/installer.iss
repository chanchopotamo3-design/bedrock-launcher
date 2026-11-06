; Instalador Inno Setup 6. No toca archivos de Minecraft. Los datos del usuario (%LOCALAPPDATA%\BedrockUltimateLauncher) se conservan al desinstalar.
[Setup]
AppName=Bedrock Ultimate Launcher
AppVersion=1.0.0
AppPublisher=Bedrock Ultimate Launcher
DefaultDirName={autopf}\Bedrock Ultimate Launcher
DefaultGroupName=Bedrock Ultimate Launcher
OutputDir=..\dist
OutputBaseFilename=BedrockUltimateLauncher-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName=Bedrock Ultimate Launcher
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio / Create desktop shortcut"
[Files]
Source: "..\dist\app\BedrockUltimateLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Bedrock Ultimate Launcher"; Filename: "{app}\BedrockUltimateLauncher.exe"
Name: "{autodesktop}\Bedrock Ultimate Launcher"; Filename: "{app}\BedrockUltimateLauncher.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\BedrockUltimateLauncher.exe"; Description: "Iniciar / Launch"; Flags: nowait postinstall skipifsilent
