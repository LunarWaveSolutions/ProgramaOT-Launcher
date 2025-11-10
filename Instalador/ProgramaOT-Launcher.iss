[Setup]
AppId={{9E3D6D6E-9C3C-45D2-9A21-7C5B5EE6C1F5}}
AppName=ProgramaOT Launcher
AppVerName=ProgramaOT Launcher 1.0.0
AppVersion=1.0.0
AppPublisher=ProgramaOT
AppPublisherURL=https://programaot.shop
DefaultDirName={userappdata}\ProgramaOT
DefaultGroupName=ProgramaOT
OutputDir=j:\Projeto\Ot\ProgramaOT-Launcher\Instalador
OutputBaseFilename=ProgramaOT-Installer
SetupIconFile=j:\Projeto\Ot\ProgramaOT-Launcher\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=none
DisableProgramGroupPage=yes
DisableWelcomePage=no

; Telas e textos
InfoBeforeFile=j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\textoInicial.md
LicenseFile=j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\textoTermo.md
InfoAfterFile=j:\Projeto\Ot\ProgramaOT-Launcher\Instalador\textoFinalInstalacao.md

UninstallDisplayIcon={app}\ProgramaOT-Launcher.exe

[Languages]
Name: "br"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "j:\Projeto\Ot\ProgramaOT-Launcher\bin\Debug\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "Tibia\*"

[Icons]
Name: "{group}\ProgramaOT Launcher"; Filename: "{app}\ProgramaOT-Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ProgramaOT-Launcher.exe"
Name: "{commondesktop}\ProgramaOT Launcher"; Filename: "{app}\ProgramaOT-Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ProgramaOT-Launcher.exe"

[Run]
Filename: "{app}\ProgramaOT-Launcher.exe"; Description: "Iniciar ProgramaOT Launcher"; Flags: nowait postinstall skipifsilent
