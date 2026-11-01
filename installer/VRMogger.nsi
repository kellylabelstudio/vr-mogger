Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!define ROOT "${__FILEDIR__}\.."
!define VERSION "1.1.0-beta.1"
Name "VR Mogger ${VERSION}"
OutFile "${ROOT}\dist\VRMogger-${VERSION}-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\VRMogger"
InstallDirRegKey HKCU "Software\VRMogger" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "1.1.0.0"
VIAddVersionKey /LANG=1033 "ProductName" "VR Mogger"
VIAddVersionKey /LANG=1033 "CompanyName" "Kelly Label Studio x MK2XX2"
VIAddVersionKey /LANG=1033 "FileDescription" "VR Mogger beta setup"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) 2026 Kelly Label Studio x MK2XX2"
!define MUI_ABORTWARNING
!define MUI_ICON "${ROOT}\bin\App.ico"
!define MUI_UNICON "${ROOT}\bin\App.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${ROOT}\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\VRMogger.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

!macro CheckAppClosed
check_app:
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "VRMogger_SingleInstance") p.r0'
  StrCmp $0 0 app_closed
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_RETRYCANCEL|MB_ICONINFORMATION "Please exit VR Mogger from its tray menu first so it can restore your display. Then click Retry." IDRETRY check_app
  Abort
app_closed:
!macroend

Function .onInit
  !insertmacro CheckAppClosed
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 528040 runtime_ok runtime_missing runtime_ok
runtime_missing:
  MessageBox MB_OK|MB_ICONSTOP "VR Mogger requires .NET Framework 4.8 or newer, which is included with Windows 11. On an older Windows installation, get it from https://dotnet.microsoft.com/download/dotnet-framework/net48 before continuing."
  Abort
runtime_ok:
  SetRegView 32
FunctionEnd

Section "VR Mogger"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File "${ROOT}\bin\VRMogger.exe"
  File "${ROOT}\LICENSE"
  File "${ROOT}\README.md"
  File "${ROOT}\START-HERE.txt"
  File "${ROOT}\CHANGELOG.md"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\VR Mogger"
  CreateShortCut "$SMPROGRAMS\VR Mogger\VR Mogger.lnk" "$INSTDIR\VRMogger.exe"
  CreateShortCut "$SMPROGRAMS\VR Mogger\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\VRMogger" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "DisplayName" "VR Mogger beta"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "Publisher" "Kelly Label Studio x MK2XX2"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "DisplayIcon" "$INSTDIR\VRMogger.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger" "NoRepair" 1
SectionEnd

Function un.onInit
  !insertmacro CheckAppClosed
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "VRMogger"
  StrCmp $0 '"$INSTDIR\VRMogger.exe" --minimized' remove_startup
  StrCmp $0 '"$INSTDIR\VRMogger.exe" /minimized' remove_startup keep_startup
remove_startup:
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "VRMogger"
keep_startup:
  SetRegView 32
  Delete "$INSTDIR\VRMogger.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\START-HERE.txt"
  Delete "$INSTDIR\CHANGELOG.md"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$SMPROGRAMS\VR Mogger\VR Mogger.lnk"
  Delete "$SMPROGRAMS\VR Mogger\Uninstall.lnk"
  RMDir "$SMPROGRAMS\VR Mogger"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VRMogger"
  DeleteRegKey HKCU "Software\VRMogger"
  ; Keep settings and recovery records in AppData. Never remove an unfinished restoration record.
SectionEnd
