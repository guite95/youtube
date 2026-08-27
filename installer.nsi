Unicode true

!ifndef PRODUCT_VERSION
  !define PRODUCT_VERSION "0.0.0"
!endif

!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "publish"
!endif

!define PRODUCT_NAME "ytdown"
!define PRODUCT_PUBLISHER "guite95"
!define PRODUCT_WEB_SITE "https://github.com/guite95/youtube"
!define PRODUCT_EXE "ytdown.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ytdown"
!define PRODUCT_APP_PATH_KEY "Software\Microsoft\Windows\CurrentVersion\App Paths\ytdown.exe"

RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 64

!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${PRODUCT_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "ytdown 실행"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Korean"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "ytdown-setup.exe"
InstallDir "$LOCALAPPDATA\Programs\ytdown"
InstallDirRegKey HKCU "${PRODUCT_APP_PATH_KEY}" ""
ShowInstDetails show
ShowUnInstDetails show

Var BackupDir

Function .onInit
  SetShellVarContext current
FunctionEnd

Function un.onInit
  SetShellVarContext current
FunctionEnd

Section "ytdown" SEC_MAIN
  SectionIn RO

  ; 설치/업데이트 중 실행 중인 기존 프로세스의 파일 잠금을 해제합니다.
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /IM "${PRODUCT_EXE}" /T /F'
  Pop $0
  Pop $1

  ; 사용자 데이터만 임시 보관합니다. 나머지 설치 파일은 전부 교체됩니다.
  StrCpy $BackupDir "$TEMP\ytdown-install-backup"
  RMDir /r "$BackupDir"
  CreateDirectory "$BackupDir"

  IfFileExists "$INSTDIR\cookies.txt" 0 +2
    CopyFiles /SILENT "$INSTDIR\cookies.txt" "$BackupDir\cookies.txt"

  IfFileExists "$INSTDIR\settings.json" 0 +2
    CopyFiles /SILENT "$INSTDIR\settings.json" "$BackupDir\settings.json"

  ; 구버전 DLL/tools/실행 파일이 남지 않도록 설치 폴더를 통째로 지웁니다.
  RMDir /r "$INSTDIR"
  CreateDirectory "$INSTDIR"
  SetOutPath "$INSTDIR"

  ; publish 디렉터리 전체를 새 설치 세트로 배치합니다.
  File /r "${PUBLISH_DIR}\*.*"

  ; 보존 대상으로 지정한 사용자 데이터만 복원합니다.
  IfFileExists "$BackupDir\cookies.txt" 0 +2
    CopyFiles /SILENT "$BackupDir\cookies.txt" "$INSTDIR\cookies.txt"

  IfFileExists "$BackupDir\settings.json" 0 +2
    CopyFiles /SILENT "$BackupDir\settings.json" "$INSTDIR\settings.json"

  RMDir /r "$BackupDir"

  WriteUninstaller "$INSTDIR\uninstall.exe"

  CreateDirectory "$SMPROGRAMS\ytdown"
  CreateShortCut "$SMPROGRAMS\ytdown\ytdown.lnk" "$INSTDIR\${PRODUCT_EXE}" "" "$INSTDIR\${PRODUCT_EXE}"
  CreateShortCut "$SMPROGRAMS\ytdown\ytdown 제거.lnk" "$INSTDIR\uninstall.exe"
  CreateShortCut "$DESKTOP\ytdown.lnk" "$INSTDIR\${PRODUCT_EXE}" "" "$INSTDIR\${PRODUCT_EXE}"

  WriteRegStr HKCU "${PRODUCT_APP_PATH_KEY}" "" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr HKCU "${PRODUCT_APP_PATH_KEY}" "Path" "$INSTDIR"

  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\${PRODUCT_EXE}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${PRODUCT_UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKCU "${PRODUCT_UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${PRODUCT_UNINST_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /IM "${PRODUCT_EXE}" /T /F'
  Pop $0
  Pop $1

  Delete "$DESKTOP\ytdown.lnk"
  Delete "$SMPROGRAMS\ytdown\ytdown.lnk"
  Delete "$SMPROGRAMS\ytdown\ytdown 제거.lnk"
  RMDir "$SMPROGRAMS\ytdown"

  DeleteRegKey HKCU "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKCU "${PRODUCT_APP_PATH_KEY}"

  ; 제거 시에는 설치 폴더 전체를 삭제합니다.
  RMDir /r "$INSTDIR"
SectionEnd
