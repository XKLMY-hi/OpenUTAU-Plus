; OpenUTAU Plus — Inno Setup 6 安装器
; 构建：ISCC.exe OpenUtauPlus.iss /DMyAppVersion=<版本>
; 深色向导（DarkMode=force）· 中英双语 · 与原版 OpenUTAU 完全共存

#ifndef MyAppVersion
  #define MyAppVersion "0.1.568.2"
#endif
#define MyAppName "OpenUTAU Plus"
#define MyAppURL "https://github.com/XKLMY-hi/OpenUTAU-Plus"

[Setup]
AppId={{7B1D9F3A-2C4E-4F8B-9A6D-5E0C8B2A1D44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
AppPublisher={#MyAppName}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
; 与原版隔离的安装目录——卸载/升级互不影响
DefaultDirName={autopf}\OpenUTAU Plus
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=..\OpenUtau\Assets\open-utau.ico
UninstallDisplayIcon={app}\OpenUtau.exe
UninstallDisplayName={#MyAppName} {#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
; 深色向导——与系统深浅设置无关，恒为深色（WizardStyle 深色取值需 Inno 6.6+）
WizardStyle=modern dark
OutputDir=dist
OutputBaseFilename=OpenUTAU-Plus-win-x64-{#MyAppVersion}-setup
; 基于文件占用检测自动关闭运行中的 Plus（按路径，绝不误关原版 OpenUtau.exe）
CloseApplications=yes
RestartApplications=yes
SetupLogging=yes
ShowLanguageDialog=no

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,createdump.exe,Microsoft.DiaSymReader.Native.amd64.dll,onnxruntime.lib,*.dylib"
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
; Name 不带 .lnk——Inno 自动附加；写了会双后缀（.lnk.lnk）
Name: "{autoprograms}\OpenUTAU Plus"; Filename: "{app}\OpenUtau.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\OpenUTAU Plus"; Filename: "{app}\OpenUtau.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; 仅注册 .ustxp（Plus 专属格式，原版无此扩展——注册/卸载零冲突）。
; 刻意不注册 .ustx：避免安装覆盖原版关联、卸载删除原版键的连带破坏。
Root: HKCR; Subkey: ".ustxp"; ValueType: string; ValueData: "OpenUtauPlusFile"; Flags: uninsdeletekey
Root: HKCR; Subkey: "OpenUtauPlusFile"; ValueType: string; ValueData: "OpenUTAU Plus 工程文件"; Flags: uninsdeletekey
Root: HKCR; Subkey: "OpenUtauPlusFile\DefaultIcon"; ValueType: string; ValueData: "{app}\OpenUtau.exe,0"; Flags: uninsdeletekey
Root: HKCR; Subkey: "OpenUtauPlusFile\shell\open\command"; ValueType: string; ValueData: """{app}\OpenUtau.exe"" ""%1"""; Flags: uninsdeletekey
; 卸载键补 InstallLocation——帮助 Geek 等第三方卸载器识别主目录（数据目录在
; Documents，名称启发式扫描无法根治，但主目录信息完整可减少误判）
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletevalue

[Run]
; VC++ 运行库：检测缺失才静默安装（上游 OpenUTAU 官方做法）
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "正在安装 Visual C++ 运行库…"; Check: NotVC2015PlusInstalled
Filename: "{app}\OpenUtau.exe"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: postinstall nowait skipifsilent

[Code]
const
  VC_KEY = 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';

{ VC++ 2015-2022 统一注册键（14.0 为共享版本号）：Installed DWORD=1 即已装 }
function IsVC2015PlusInstalled(): Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, VC_KEY, 'Installed', Installed) and (Installed = 1);
end;

function NotVC2015PlusInstalled(): Boolean;
begin
  Result := not IsVC2015PlusInstalled();
end;
