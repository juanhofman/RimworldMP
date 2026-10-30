; RimworldMP single-file Windows installer (Inno Setup 6)
; Payload is prepared by packaging/build-setup.ps1 into packaging/stage/

#ifndef MyAppVersion
  #define MyAppVersion "0.7.0"
#endif
#ifndef MyStageDir
  #define MyStageDir "stage"
#endif
#ifndef MyDistDir
  #define MyDistDir "dist"
#endif

#define MyAppName "RimworldMP"
#define MyAppPublisher "RimworldMP"
#define MyAppURL "https://github.com/juanhofman/RimworldMP"
#define MyAppExeName "RimworldMP.Launcher.exe"

[Setup]
AppId={{A8C3E2F1-9B47-4D2E-8F61-0C1D5A7B3E90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\RimworldMP
DefaultGroupName=RimworldMP
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#MyDistDir}
OutputBaseFilename=RimworldMP-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=
DisableWelcomePage=no
InfoBeforeFile=
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "installmods"; Description: "Install RimworldMP + Harmony into RimWorld Mods folder"; GroupDescription: "RimWorld mods:"; Flags: checkedonce

[Files]
; Launcher (self-contained publish)
Source: "{#MyStageDir}\Launcher\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Bundled mod payloads (copied to RimWorld Mods when task selected)
Source: "{#MyStageDir}\Mods\RimworldMP\*"; DestDir: "{code:GetRimWorldModsDir}\RimworldMP"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: installmods; Check: HasRimWorldModsDir
Source: "{#MyStageDir}\Mods\Harmony\*"; DestDir: "{code:GetRimWorldModsDir}\Harmony"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: installmods; Check: HasRimWorldModsDir
; Always keep a local copy under the app so the launcher can "Fix my mods" later
Source: "{#MyStageDir}\Mods\RimworldMP\*"; DestDir: "{app}\BundledMods\RimworldMP"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyStageDir}\Mods\Harmony\*"; DestDir: "{app}\BundledMods\Harmony"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RimworldMP Launcher"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\RimworldMP Launcher"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch RimworldMP"; Flags: nowait postinstall skipifsilent unchecked

[Code]
var
  CachedModsDir: String;

function TrySteamInstallPath(var SteamPath: String): Boolean;
begin
  Result := RegQueryStringValue(HKLM64, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', SteamPath)
         or RegQueryStringValue(HKLM32, 'SOFTWARE\Valve\Steam', 'InstallPath', SteamPath)
         or RegQueryStringValue(HKCU, 'SOFTWARE\Valve\Steam', 'SteamPath', SteamPath);
  if Result then
    StringChangeEx(SteamPath, '/', '\', True);
end;

function DetectRimWorldModsDir: String;
var
  SteamPath, Candidate: String;
begin
  Result := '';
  if TrySteamInstallPath(SteamPath) then
  begin
    Candidate := SteamPath + '\steamapps\common\RimWorld\Mods';
    if DirExists(SteamPath + '\steamapps\common\RimWorld') then
    begin
      Result := Candidate;
      Exit;
    end;
  end;

  Candidate := ExpandConstant('{commonpf32}\Steam\steamapps\common\RimWorld\Mods');
  if DirExists(ExpandConstant('{commonpf32}\Steam\steamapps\common\RimWorld')) then
  begin
    Result := Candidate;
    Exit;
  end;

  Candidate := ExpandConstant('{localappdata}\RimWorld\Mods');
  if DirExists(ExpandConstant('{localappdata}\RimWorld')) then
    Result := Candidate;
end;

function GetRimWorldModsDir(Param: String): String;
begin
  if CachedModsDir = '' then
    CachedModsDir := DetectRimWorldModsDir;
  if CachedModsDir = '' then
    CachedModsDir := ExpandConstant('{app}\BundledMods');
  Result := CachedModsDir;
end;

function HasRimWorldModsDir: Boolean;
begin
  Result := DetectRimWorldModsDir <> '';
end;

procedure InitializeWizard;
begin
  CachedModsDir := DetectRimWorldModsDir;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Msg: String;
begin
  if CurStep = ssPostInstall then
  begin
    if (not HasRimWorldModsDir) and WizardIsTaskSelected('installmods') then
    begin
      Msg := 'RimWorld Mods folder was not found automatically.' + #13#10
        + 'Mods were saved under:' + #13#10 + ExpandConstant('{app}\BundledMods') + #13#10
        + 'Copy Harmony + RimworldMP into your RimWorld Mods folder, or use Fix my mods in the launcher.';
      MsgBox(Msg, mbInformation, MB_OK);
    end;
  end;
end;
