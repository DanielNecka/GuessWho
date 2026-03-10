; Inno Setup Script for GuessWho
; Download Inno Setup from: https://jrsoftware.org/isdl.php
;
; Compile for each architecture:
;   iscc /DArch=x64 setup.iss
;   iscc /DArch=x86 setup.iss
;   iscc /DArch=arm64 setup.iss

#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
AppId={{E3F7A1B2-5C4D-4E6F-8A9B-0D1E2F3A4B5C}
AppName=GuessWho
AppVersion=1.0
AppVerName=GuessWho 1.0
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Daniel Necka
VersionInfoDescription=GuessWho - gra sieciowa
VersionInfoCopyright=© 2026 Daniel Necka
AppPublisher=Daniel Necka
AppPublisherURL=https://github.com/DanielNecka/GuessWho
AppSupportURL=https://github.com/DanielNecka/GuessWho/issues
AppUpdatesURL=https://github.com/DanielNecka/GuessWho/releases
DefaultDirName={autopf}\GuessWho
DefaultGroupName=GuessWho
OutputDir=installer
OutputBaseFilename=GuessWho_Setup_{#Arch}
SetupIconFile=assets\ico.ico
UninstallDisplayIcon={app}\GuessWho.exe
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
#if Arch == "x64"
ArchitecturesInstallIn64BitMode=x64compatible
#elif Arch == "arm64"
ArchitecturesInstallIn64BitMode=arm64 x64compatible
#endif

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
; Main application (published single-file exe)
Source: "bin\publish\win-{#Arch}\GuessWho.exe"; DestDir: "{app}"; Flags: ignoreversion
; Config file
Source: "bin\publish\win-{#Arch}\config.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut
Name: "{group}\GuessWho"; Filename: "{app}\GuessWho.exe"; IconFilename: "{app}\GuessWho.exe"
; Desktop shortcut
Name: "{autodesktop}\GuessWho"; Filename: "{app}\GuessWho.exe"; IconFilename: "{app}\GuessWho.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; GroupDescription: "Dodatkowe ikony:"

[Run]
Filename: "{app}\GuessWho.exe"; Description: "Uruchom GuessWho"; Flags: nowait postinstall skipifsilent
