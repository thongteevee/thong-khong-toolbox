[Setup]
AppId={{8E75B3C2-11DB-4AB1-9D06-1BFCC9D1E407}
AppName=Thong Khong Toolbox
AppVersion={#ToolboxVersion}
AppPublisher=Thong Khong Toolbox
AppPublisherURL=https://github.com/thongteevee/thong-khong-toolbox
AppSupportURL=https://github.com/thongteevee/thong-khong-toolbox/issues
AppUpdatesURL=https://github.com/thongteevee/thong-khong-toolbox/releases/latest
DefaultDirName={localappdata}\Programs\Thong Khong Toolbox
DefaultGroupName=Thong Khong Toolbox
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
OutputBaseFilename=ThongKhongToolboxSetup
WizardStyle=modern
SetupIconFile=..\assets\toolbox.ico
LicenseFile=UserAgreement.txt
InfoBeforeFile=PrivacyPolicy.txt
UninstallDisplayIcon={app}\ThongKhongToolbox.exe

[Messages]
WizardInfoBefore=Privacy Policy
InfoBeforeLabel=Review how Toolbox handles your data before installing.
InfoBeforeClickLabel=Click Next to continue.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#ToolboxExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "UserAgreement.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "PrivacyPolicy.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Thong Khong Toolbox"; Filename: "{app}\ThongKhongToolbox.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Thong Khong Toolbox"; Filename: "{app}\ThongKhongToolbox.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ThongKhongToolbox.exe"; Description: "Launch Thong Khong Toolbox"; Flags: postinstall nowait skipifsilent unchecked

[Code]
function InitializeSetup(): Boolean;
var
  FrameworkRelease: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', FrameworkRelease) and (FrameworkRelease >= 528040);
  if not Result then
    MsgBox('Thong Khong Toolbox requires .NET Framework 4.8 or later. Install it from Microsoft and run Setup again.', mbError, MB_OK);
end;
