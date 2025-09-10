!include "MUI2.nsh"

Name "YouTube to MP3 Converter"
OutFile "YouTube_to_MP3_Converter_Setup.exe"
InstallDir "$PROGRAMFILES\YouTube to MP3 Converter"
InstallDirRegKey HKCU "Software\YouTubeToMP3" "InstallDir"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "Korean"

Section "Install"
    SetOutPath "$INSTDIR"
    File "dist\youtube_to_mp3.exe"
    CreateShortCut "$DESKTOP\YouTube to MP3 Converter.lnk" "$INSTDIR\youtube_to_mp3.exe"
    CreateDirectory "$SMPROGRAMS\YouTube to MP3 Converter"
    CreateShortCut "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk" "$INSTDIR\youtube_to_mp3.exe"
    WriteRegStr HKCU "Software\YouTubeToMP3" "InstallDir" "$INSTDIR"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
    Delete "$INSTDIR\youtube_to_mp3.exe"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    Delete "$DESKTOP\YouTube to MP3 Converter.lnk"
    Delete "$SMPROGRAMS\YouTube to MP3 Converter\YouTube to MP3 Converter.lnk"
    RMDir "$SMPROGRAMS\YouTube to MP3 Converter"
    DeleteRegKey HKCU "Software\YouTubeToMP3"
SectionEnd
