#define MyAppName "Yukari"
#define MyAppPublisher "TXG0Fk3"
#define MyAppURL "https://github.com/Yukari-App/Yukari"
#define MyAppExeName "Yukari.exe"
#define MyArch "x64"

#define AppPublishPath "..\Yukari\bin\" + MyArch + "\Release\net10.0-windows10.0.22621.0\win-" + MyArch + "\publish\"
#define FullExePath AppPublishPath + MyAppExeName

#ifExist FullExePath
  #define MyAppVersion GetStringFileInfo(FullExePath, "ProductVersion")
  #if MyAppVersion == ""
    #define MyAppVersion GetFileVersion(FullExePath)
  #endif
#else
  #define MyAppVersion "0.0.0-BuildError"
#endif

[Setup]
AppId={{FC56388E-9A45-4F15-BB8F-204C3C2BA2CB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=.
OutputBaseFilename={#MyAppName}.Setup.{#MyArch}
SetupIconFile=..\Yukari.ico

Compression=lzma
SolidCompression=yes
WizardStyle=modern dynamic windows11

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppPublishPath}*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent