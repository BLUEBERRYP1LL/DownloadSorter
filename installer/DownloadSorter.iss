; DownloadSorter Installer Script for Inno Setup
; Download Inno Setup from: https://jrsoftware.org/isinfo.php

#define MyAppName "DownloadSorter"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DownloadSorter"
#define MyAppURL "https://github.com/yourusername/DownloadSorter"
#define MyAppExeName "sorter.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\publish
OutputBaseFilename=DownloadSorter-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "addtopath"; Description: "Add to PATH (recommended for CLI usage)"; GroupDescription: "System Configuration:"; Flags: checkedonce

[Files]
Source: "..\publish\sorter.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName} Dashboard"; Filename: "{app}\{#MyAppExeName}"; Parameters: "dashboard"
Name: "{group}\{#MyAppName} Status"; Filename: "{app}\{#MyAppExeName}"; Parameters: "status"
Name: "{group}\Open Sorted Folder"; Filename: "{userappdata}\..\Local\DownloadSorter"; Flags: foldershortcut
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "dashboard"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "init"; Description: "Run initial setup wizard"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  Path: string;
  NewPath: string;
begin
  if CurStep = ssPostInstall then
  begin
    if IsTaskSelected('addtopath') then
    begin
      RegQueryStringValue(HKEY_LOCAL_MACHINE,
        'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
        'Path', Path);

      if Pos(ExpandConstant('{app}'), Path) = 0 then
      begin
        NewPath := Path + ';' + ExpandConstant('{app}');
        RegWriteStringValue(HKEY_LOCAL_MACHINE,
          'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
          'Path', NewPath);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Path: string;
  NewPath: string;
  P: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
      'Path', Path);

    P := Pos(ExpandConstant('{app}'), Path);
    if P > 0 then
    begin
      Delete(Path, P - 1, Length(ExpandConstant('{app}')) + 1);
      RegWriteStringValue(HKEY_LOCAL_MACHINE,
        'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
        'Path', Path);
    end;
  end;
end;
