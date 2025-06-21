; YouTube to MP3 Converter Installer
; NSIS 스크립트

!define PRODUCT_NAME "YouTube to MP3 Converter"
!define PRODUCT_VERSION "1.1.0"
!define PRODUCT_PUBLISHER "guite95"
!define PRODUCT_WEB_SITE "https://github.com/guite95/youtube"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\youtube_to_mp3.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

SetCompressor lzma

; MUI 1.67 compatible ------
!include "MUI.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\modern-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\modern-uninstall.ico"

; Welcome page
!insertmacro MUI_PAGE_WELCOME
; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE"
; Directory page
!insertmacro MUI_PAGE_DIRECTORY
; Instfiles page
!insertmacro MUI_PAGE_INSTFILES
; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\youtube_to_mp3.exe"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES

; Language files
!insertmacro MUI_LANGUAGE "Korean"

; MUI end ------

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "YouTube_to_MP3_Converter_Setup.exe"
InstallDir "$PROGRAMFILES\YouTube to MP3 Converter"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

Section "MainSection" SEC01
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer
  
  ; 메인 실행 파일
  File "build\exe.win-amd64-3.12\youtube_to_mp3.exe"
  
  ; Python DLL
  File "build\exe.win-amd64-3.12\python312.dll"
  
  ; 라이선스 파일
  File "build\exe.win-amd64-3.12\frozen_application_license.txt"
  
  ; lib 폴더 복사
  SetOutPath "$INSTDIR\lib"
  File /r "build\exe.win-amd64-3.12\lib\*.*"
  
  ; share 폴더 복사
  SetOutPath "$INSTDIR\share"
  File /r "build\exe.win-amd64-3.12\share\*.*"
  
  ; README 파일 생성
  FileOpen $0 "$INSTDIR\README.txt" w
  FileWrite $0 "YouTube to MP3 Converter v${PRODUCT_VERSION}$\r$\n"
  FileWrite $0 "$\r$\n"
  FileWrite $0 "YouTube 동영상을 MP3 오디오로 변환하는 프로그램입니다.$\r$\n"
  FileWrite $0 "$\r$\n"
  FileWrite $0 "사용법:$\r$\n"
  FileWrite $0 "1. YouTube URL을 입력하세요$\r$\n"
  FileWrite $0 "2. (선택사항) 파일 이름을 입력하세요$\r$\n"
  FileWrite $0 "3. 저장 경로를 선택하세요$\r$\n"
  FileWrite $0 "4. 다운로드 버튼을 클릭하세요$\r$\n"
  FileWrite $0 "$\r$\n"
  FileWrite $0 "GitHub: ${PRODUCT_WEB_SITE}$\r$\n"
  FileClose $0
SectionEnd

Section -AdditionalIcons
  WriteIniStr "$INSTDIR\${PRODUCT_NAME}.url" "InternetShortcut" "URL" "${PRODUCT_WEB_SITE}"
  CreateDirectory "$SMPROGRAMS\YouTube to MP3 Converter"
  CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk" "$INSTDIR\youtube_to_mp3.exe"
  CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\웹사이트.lnk" "$INSTDIR\${PRODUCT_NAME}.url"
  CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\제거.lnk" "$INSTDIR\uninst.exe"
SectionEnd

Section -Post
  WriteUninstaller "$INSTDIR\uninst.exe"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\youtube_to_mp3.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\youtube_to_mp3.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd

; Uninstaller Section
Section Uninstall
  Delete "$INSTDIR\${PRODUCT_NAME}.url"
  Delete "$INSTDIR\uninst.exe"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\frozen_application_license.txt"
  Delete "$INSTDIR\python312.dll"
  Delete "$INSTDIR\youtube_to_mp3.exe"

  Delete "$SMPROGRAMS\YouTube to MP3 Converter\제거.lnk"
  Delete "$SMPROGRAMS\YouTube to MP3 Converter\웹사이트.lnk"
  Delete "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk"

  RMDir "$SMPROGRAMS\YouTube to MP3 Converter"
  RMDir /r "$INSTDIR\share"
  RMDir /r "$INSTDIR\lib"
  RMDir "$INSTDIR"

  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  SetAutoClose true
SectionEnd 