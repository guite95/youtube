@echo off
echo YouTube to MP3 Converter Installer 빌드 중...

REM NSIS가 설치되어 있는지 확인
where makensis >nul 2>nul
if %errorlevel% neq 0 (
    echo NSIS가 설치되어 있지 않습니다.
    echo https://nsis.sourceforge.io/Download 에서 NSIS를 다운로드하여 설치해주세요.
    pause
    exit /b 1
)

REM 설치 프로그램 빌드
echo NSIS 스크립트 컴파일 중...
makensis installer.nsi

if %errorlevel% equ 0 (
    echo.
    echo 설치 프로그램이 성공적으로 생성되었습니다!
    echo 파일명: YouTube_to_MP3_Converter_Setup.exe
    echo.
    echo 이제 이 파일을 GitHub 릴리즈에 업로드할 수 있습니다.
) else (
    echo.
    echo 설치 프로그램 빌드 중 오류가 발생했습니다.
)

pause 