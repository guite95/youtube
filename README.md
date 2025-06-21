# YouTube to MP3 Converter

YouTube 동영상을 MP3 오디오로 변환하는 데스크톱 애플리케이션입니다.

## 주요 기능

- YouTube URL을 입력하여 MP3로 변환
- 사용자 지정 파일 이름 지원
- 저장 경로 선택
- 자동 업데이트 기능
- 직관적인 GUI 인터페이스

## 설치 방법

### 방법 1: 설치 프로그램 사용 (권장)
1. [릴리즈 페이지](https://github.com/guite95/youtube/releases)에서 최신 버전의 `YouTube_to_MP3_Converter_Setup.exe`를 다운로드하세요
2. 설치 프로그램을 실행하세요
3. 설치 완료 후 시작 메뉴에서 프로그램을 실행하세요

### 방법 2: 소스 코드에서 실행
```bash
# 저장소 클론
git clone https://github.com/guite95/youtube.git
cd youtube

# 의존성 설치
pip install -r requirements.txt

# 프로그램 실행
python youtube_to_mp3.py
```

## 사용법

1. **YouTube URL 입력**: 변환하고 싶은 YouTube 동영상의 URL을 입력하세요
2. **파일 이름 지정** (선택사항): 원하는 파일 이름을 입력하세요. 비워두면 동영상 제목을 사용합니다
3. **저장 경로 선택**: MP3 파일을 저장할 폴더를 선택하세요
4. **다운로드**: 다운로드 버튼을 클릭하여 변환을 시작하세요

## 시스템 요구사항

- Windows 10 이상
- 인터넷 연결
- FFmpeg (자동 설치됨)

## 개발

### 로컬에서 설치 프로그램 빌드

1. [NSIS](https://nsis.sourceforge.io/Download)를 설치하세요
2. 다음 명령을 실행하세요:
```bash
build_installer.bat
```

### GitHub Actions를 통한 자동 빌드

태그를 푸시하면 자동으로 설치 프로그램이 빌드됩니다:
```bash
git tag v1.1.0
git push origin v1.1.0
```

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

## 기여

버그 리포트나 기능 제안은 [Issues](https://github.com/guite95/youtube/issues) 페이지를 이용해주세요.

## 업데이트

프로그램 내에서 "도움말 > 업데이트 확인"을 통해 최신 버전을 확인할 수 있습니다.
