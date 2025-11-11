[Setup]
AppId={{9E3D6D6E-9C3C-45D2-9A21-7C5B5EE6C1F5}}
AppName=ProgramaOT Launcher
AppVerName=ProgramaOT Launcher 1.0.0
AppVersion=1.0.0
AppPublisher=ProgramaOT
AppPublisherURL=https://programaot.shop
; Instalação por usuário em um local padrão para apps (menos propenso a falsos positivos)
DefaultDirName={localappdata}\Programs\ProgramaOT
DefaultGroupName=ProgramaOT
OutputDir=j:\Projeto\Ot\ProgramaOT-Launcher\Instalador
OutputBaseFilename=ProgramaOT-Installer
SetupIconFile=j:\Projeto\Ot\ProgramaOT-Launcher\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Não requer privilégios de administrador; instala por usuário
PrivilegesRequired=lowest
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
; Empacotar binários de Release (otimizado)
Source: "j:\Projeto\Ot\ProgramaOT-Launcher\bin\Release\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "Tibia\*"

[Icons]
Name: "{group}\ProgramaOT Launcher"; Filename: "{app}\ProgramaOT-Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ProgramaOT-Launcher.exe"
Name: "{commondesktop}\ProgramaOT Launcher"; Filename: "{app}\ProgramaOT-Launcher.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ProgramaOT-Launcher.exe"

[Run]
Filename: "{app}\ProgramaOT-Launcher.exe"; Description: "Iniciar ProgramaOT Launcher"; Flags: nowait postinstall skipifsilent
