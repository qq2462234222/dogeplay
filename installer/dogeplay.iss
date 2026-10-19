#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef ReleaseVersion
  #error ReleaseVersion is required
#endif

[Setup]
AppId={{4EEAC7E7-BCE1-47FD-A207-DCC542F565B1}
AppName=dogeplay
AppVersion={#ReleaseVersion}
AppVerName=dogeplay {#ReleaseVersion}
AppPublisher=doge
AppComments=游戏演奏与悬浮学习谱 · 免费使用 · 仅供学习交流
VersionInfoCompany=doge
VersionInfoProductName=dogeplay
VersionInfoVersion={#ReleaseVersion}
VersionInfoDescription=dogeplay 安装程序
DefaultDirName={code:DefaultInstallDir}
DefaultGroupName=dogeplay
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AppMutex=Local\LumaKeys.Desktop
CloseApplications=yes
RestartApplications=no
SetupIconFile={#PayloadDir}\assets\dogeplay.ico
UninstallDisplayIcon={app}\dogeplay.exe
LicenseFile={#SetupLicense}
OutputDir={#SetupOutput}
OutputBaseFilename=dogeplay-{#ReleaseVersion}-setup-win-x64
WizardStyle=modern
WizardResizable=yes
Compression=lzma2/fast
SolidCompression=yes
SetupLogging=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "data\*,*.log,*.pdb"

[Dirs]
Name: "{app}\data"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\dogeplay"; Filename: "{app}\dogeplay.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\dogeplay"; Filename: "{app}\dogeplay.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\dogeplay.exe"; Description: "启动 dogeplay"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
function DefaultInstallDir(Param: String): String;
begin
  if DirExists('D:\') then Result := 'D:\dogeplay'
  else Result := ExpandConstant('{sd}\dogeplay');
end;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('dogeplay 需要 .NET Framework 4.8。请在 Windows 更新中安装该组件，然后重新运行安装程序。', mbError, MB_OK);
end;
