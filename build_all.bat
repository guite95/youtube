@echo off
echo YouTube to MP3 Converter 전체 빌드 프로세스 시작...
echo.

REM Python이 설치되어 있는지 확인
python --version >nul 2>nul
if %errorlevel% neq 0 (
    echo Python이 설치되어 있지 않습니다.
    echo https://python.org 에서 Python을 다운로드하여 설치해주세요.
    pause
    exit /b 1
)

REM 의존성 설치
echo 의존성 설치 중...
pip install -r requirements.txt
pip install pyinstaller

REM 기존 빌드 폴더 정리
if exist "build" (
    echo 기존 빌드 폴더 정리 중...
    rmdir /s /q build
)

if exist "dist" (
    echo 기존 dist 폴더 정리 중...
    rmdir /s /q dist
)

REM PyInstaller로 실행 파일 빌드
echo PyInstaller로 실행 파일 빌드 중...
pyinstaller --onefile --noconsole --name youtube_to_mp3 --icon="icon.ico" --hidden-import=PyQt6.sip youtube_to_mp3.py

if %errorlevel% neq 0 (
    echo PyInstaller 빌드 중 오류가 발생했습니다.
    pause
    exit /b 1
)

REM NSIS가 설치되어 있는지 확인
where makensis >nul 2>nul
if %errorlevel% neq 0 (
    echo.
    echo NSIS가 설치되어 있지 않습니다.
    echo https://nsis.sourceforge.io/Download 에서 NSIS를 다운로드하여 설치해주세요.
    echo 또는 Chocolatey를 사용하여 설치할 수 있습니다: choco install nsis
    echo.
    echo 실행 파일은 dist\youtube_to_mp3.exe 폴더에 생성되었습니다.
    pause
    exit /b 1
)

REM 설치 프로그램 빌드
echo.
echo NSIS로 설치 프로그램 빌드 중...
makensis installer.nsi

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo 빌드 완료!
    echo ========================================
    echo.
    echo 생성된 파일들:
    echo - 실행 파일: dist\youtube_to_mp3.exe
    echo - 설치 프로그램: YouTube_to_MP3_Converter_Setup.exe
    echo.
    echo 이제 YouTube_to_MP3_Converter_Setup.exe를 GitHub 릴리즈에 업로드할 수 있습니다.
) else (
    echo.
    echo 설치 프로그램 빌드 중 오류가 발생했습니다.
)

echo.
pause 