#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#ifndef MyAppRuntime
  #define MyAppRuntime "win-x64"
#endif

#ifndef MySourceDir
  #define MySourceDir "publish"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "."
#endif

#define MyAppName "QuickMask"
#define MyAppDisplayName "QuickMask"
#define MyAppExeName "QuickMask.exe"
#define MyRepoRoot "..\\.."

[Setup]
AppId={{7E5968A8-A8D6-4220-96F9-8CE537A9B500}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\{#MyAppName}
PrivilegesRequired=lowest
DefaultGroupName={#MyAppDisplayName}
DisableProgramGroupPage=yes
UsePreviousTasks=no
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir={#MyRepoRoot}\{#MyOutputDir}
OutputBaseFilename={#MyAppName}_{#MyAppVersion}-{#MyAppRuntime}_setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#MyRepoRoot}\QuickMask.UI\Assets\SoftwareIcon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyRepoRoot}\{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
