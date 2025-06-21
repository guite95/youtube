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
!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"

; Welcome page
!insertmacro MUI_PAGE_WELCOME
; License page
!insertmacro MUI_PAGE_LICENSE "LICENSE"
; Components page
!insertmacro MUI_PAGE_COMPONENTS
; Directory page
!insertmacro MUI_PAGE_DIRECTORY
; Instfiles page
!insertmacro MUI_PAGE_INSTFILES
; Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\youtube_to_mp3.exe"
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

Section "메인 프로그램 (필수)" SEC_MAIN
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite ifnewer
  
  ; PyInstaller --onefile 로 빌드된 메인 실행 파일 하나만 복사합니다.
  File "dist\youtube_to_mp3.exe"
  
  ; 시작 메뉴 바로가기 생성
  CreateDirectory "$SMPROGRAMS\YouTube to MP3 Converter"
  CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk" "$INSTDIR\youtube_to_mp3.exe"
  CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\제거.lnk" "$INSTDIR\uninst.exe"
SectionEnd

Section "바탕화면에 바로가기 생성" SEC_DESKTOP_SHORTCUT
  CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\youtube_to_mp3.exe"
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
  ; 메인 실행 파일 및 언인스톨러 삭제
  Delete "$INSTDIR\youtube_to_mp3.exe"
  Delete "$INSTDIR\uninst.exe"

  ; 시작 메뉴 및 바탕화면 바로가기 삭제
  Delete "$SMPROGRAMS\YouTube to MP3 Converter\제거.lnk"
  Delete "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk"
  Delete "$DESKTOP\${PRODUCT_NAME}.lnk"

  RMDir "$SMPROGRAMS\YouTube to MP3 Converter"
  RMDir "$INSTDIR"

  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
  SetAutoClose true
SectionEnd 