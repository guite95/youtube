# YouTube to MP3 Converter Installer Builder
# PowerShell 스크립트

Write-Host "YouTube to MP3 Converter Installer 빌드 중..." -ForegroundColor Green

# NSIS가 설치되어 있는지 확인
try {
    $nsisPath = Get-Command makensis -ErrorAction Stop
    Write-Host "NSIS 발견: $($nsisPath.Source)" -ForegroundColor Green
} catch {
    Write-Host "NSIS가 설치되어 있지 않습니다." -ForegroundColor Red
    Write-Host "https://nsis.sourceforge.io/Download 에서 NSIS를 다운로드하여 설치해주세요." -ForegroundColor Yellow
    Write-Host "또는 Chocolatey를 사용하여 설치할 수 있습니다: choco install nsis" -ForegroundColor Yellow
    Read-Host "계속하려면 아무 키나 누르세요"
    exit 1
}

# build 폴더가 존재하는지 확인
if (-not (Test-Path "build\exe.win-amd64-3.12")) {
    Write-Host "빌드 폴더를 찾을 수 없습니다. 먼저 PyInstaller로 실행 파일을 빌드해주세요." -ForegroundColor Red
    Write-Host "python -m PyInstaller --onefile --windowed youtube_to_mp3.py" -ForegroundColor Yellow
    Read-Host "계속하려면 아무 키나 누르세요"
    exit 1
}

# 설치 프로그램 빌드
Write-Host "NSIS 스크립트 컴파일 중..." -ForegroundColor Yellow
& makensis installer.nsi

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "설치 프로그램이 성공적으로 생성되었습니다!" -ForegroundColor Green
    Write-Host "파일명: YouTube_to_MP3_Converter_Setup.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "이제 이 파일을 GitHub 릴리즈에 업로드할 수 있습니다." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "설치 프로그램 빌드 중 오류가 발생했습니다." -ForegroundColor Red
}

Read-Host "계속하려면 아무 키나 누르세요" 