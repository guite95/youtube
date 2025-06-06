import sys
from cx_Freeze import setup, Executable

# 의존성 패키지
build_exe_options = {
    "packages": ["os", "sys", "yt_dlp", "PyQt6", "requests", "json", "tempfile", "shutil", "subprocess"],
    "excludes": [],
    "include_files": [
        "README.md",
        "LICENSE",
        "requirements.txt"
    ]
}

# 기본 타겟
base = None
if sys.platform == "win32":
    base = "Win32GUI"

setup(
    name="YouTube to MP3 Converter",
    version="1.0.1",
    description="YouTube 동영상을 MP3로 변환하는 프로그램",
    options={"build_exe": build_exe_options},
    executables=[
        Executable(
            "youtube_to_mp3.py",
            base=base,
            target_name="youtube_to_mp3.exe",
            icon="icon.ico"  # 아이콘 파일이 있다면 추가
        )
    ]
)
