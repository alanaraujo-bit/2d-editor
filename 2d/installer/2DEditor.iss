; Instalador do 2D Editor (2D Tecnologia) - Inno Setup 7
; Compilar:  tools\InnoSetup\ISCC.exe /DAppVersion=1.0.0 installer\2DEditor.iss
; Pré-requisito: scripts\package.ps1 (gera dist\2D Editor)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName      "2D Editor"
#define AppPublisher "2D Tecnologia"
#define AppURL       "https://2dtecnologia.com/"
#define AppExe       "2DEditor.exe"
#define Src          "..\dist\2D Editor"
#define Brand        "..\brand"

[Setup]
AppId={{6F1D2A3C-2D7E-4C1B-9E2D-2D7EC0FFEE01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL=https://wa.me/5531997788973
AppUpdatesURL={#AppURL}
AppCopyright=(C) 2026 2D Tecnologia
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Instalador do {#AppName}
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Instala sem administrador para o usuário atual; oferece "todos os usuários" (com admin)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile={#Src}\LICENSE
SetupIconFile={#Brand}\2d.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
DisableWelcomePage=no
WizardImageFile={#Brand}\wizard-large-1x.bmp,{#Brand}\wizard-large-2x.bmp
WizardSmallImageFile={#Brand}\wizard-small-1x.bmp,{#Brand}\wizard-small-2x.bmp
OutputDir=..\dist
OutputBaseFilename=2D-Editor-Setup
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
ptbr.ContextMenu=Adicionar "Editar com 2D Editor" ao menu do botão direito
ptbr.EditWith=Editar com 2D Editor
ptbr.Integration=Integração com o Windows:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "contextmenu"; Description: "{cm:ContextMenu}"; GroupDescription: "{cm:Integration}"

[Files]
; Instalado: configurações em %APPDATA%\2D Editor (sem doLocalConf.xml = não portátil)
Source: "{#Src}\*"; DestDir: "{app}"; Excludes: "doLocalConf.xml"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Editor de texto e código - 2D Tecnologia"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; "Abrir com" do Windows
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""
; Menu de contexto "Editar com 2D Editor" para qualquer arquivo
Root: HKA; Subkey: "Software\Classes\*\shell\2DEditor"; ValueType: string; ValueData: "{cm:EditWith}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\*\shell\2DEditor"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExe}"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\*\shell\2DEditor\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: contextmenu

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
