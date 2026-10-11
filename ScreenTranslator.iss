#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "ScreenSense AI"
#define AppExeName "ScreenTranslator.exe"
#define AppPublisher "Your Name"
#define PublishDir "publish"
#define FarsiIsl AddBackslash(CompilerPath) + "Languages\Farsi.isl"
#define LicensePath "LICENSE"

[Setup]
AppId={{D18E82CB-7949-4285-ABFA-0F814F6CC51B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
OutputDir=dist
OutputBaseFilename=ScreenTranslator-Setup-{#AppVersion}
SetupIconFile=app.ico
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
AppMutex=ScreenTranslator.SingleInstance
CloseApplications=yes
RestartApplications=no
#if FileExists(LicensePath)
LicenseFile={#LicensePath}
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#if FileExists(FarsiIsl)
Name: "farsi"; MessagesFile: "{#FarsiIsl}"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ScreenTranslator"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\ScreenTranslator');

    if (not UninstallSilent) and DirExists(DataDir) then
    begin
      if MsgBox('Also delete your settings, saved API keys and downloaded OCR language data?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
