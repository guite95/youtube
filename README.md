# 🎵 YouTube to MP3 Converter

<div align="center">

![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

**YouTube 동영상을 MP3 오디오로 변환하는 데스크톱 애플리케이션**

[📥 다운로드](https://github.com/guite95/youtube/releases) | [🐛 이슈 리포트](https://github.com/guite95/youtube/issues) | [⭐ 스타](https://github.com/guite95/youtube/stargazers)

</div>

---

## ✨ 주요 기능

- 🎯 **간편한 변환**: YouTube URL만 입력하면 MP3로 변환
- 📝 **파일 이름 지정**: 원하는 파일 이름으로 저장 가능
- 📁 **저장 경로 선택**: 원하는 폴더에 저장
- 🔄 **자동 업데이트**: 프로그램 내에서 최신 버전 확인
- 🖥️ **직관적인 GUI**: 사용하기 쉬운 인터페이스
- ⚡ **빠른 변환**: 최적화된 다운로드 및 변환

## 🚀 빠른 시작

### 방법 1: 설치 프로그램 사용 (권장)

1. **[릴리즈 페이지](https://github.com/guite95/youtube/releases)**에서 최신 버전의 `YouTube_to_MP3_Converter_Setup.exe` 다운로드
2. 설치 프로그램 실행
3. 설치 완료 후 시작 메뉴에서 프로그램 실행

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

## 📖 사용법

<div align="center">

![사용법](https://via.placeholder.com/600x400/2196F3/FFFFFF?text=YouTube+to+MP3+Converter+사용법)

</div>

1. **YouTube URL 입력**: 변환하고 싶은 YouTube 동영상의 URL을 입력하세요
2. **파일 이름 지정** (선택사항): 원하는 파일 이름을 입력하세요. 비워두면 동영상 제목을 사용합니다
3. **저장 경로 선택**: MP3 파일을 저장할 폴더를 선택하세요
4. **다운로드**: 다운로드 버튼을 클릭하여 변환을 시작하세요

## 💻 시스템 요구사항

- **운영체제**: Windows 10 이상
- **인터넷**: 안정적인 인터넷 연결
- **저장공간**: 충분한 디스크 공간
- **기타**: FFmpeg (자동 설치됨)

## 🔧 개발자 정보

### 로컬에서 설치 프로그램 빌드

1. [NSIS](https://nsis.sourceforge.io/Download) 설치
2. 다음 명령 실행:
```bash
build_all.bat
```

### GitHub Actions를 통한 자동 빌드

태그를 푸시하면 자동으로 설치 프로그램이 빌드됩니다:
```bash
git tag v1.1.0
git push origin v1.1.0
```

## 📝 라이선스

이 프로젝트는 **MIT 라이선스** 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

## 🤝 기여하기

버그 리포트나 기능 제안은 [Issues](https://github.com/guite95/youtube/issues) 페이지를 이용해주세요.

## 🔄 업데이트

프로그램 내에서 **"도움말 > 업데이트 확인"**을 통해 최신 버전을 확인할 수 있습니다.

---

<div align="center">

**⭐ 이 프로젝트가 도움이 되었다면 스타를 눌러주세요!**

[GitHub에서 보기](https://github.com/guite95/youtube)

</div>
