from cx_Freeze import setup, Executable

# 실행 파일로 만들 스크립트 지정
target = Executable(
    script="youtube_to_mp3.py",  # 스크립트 파일 이름
    target_name="youtube_to_mp3.exe"  # 생성할 exe 파일 이름
)

setup(
    name="YouTube to MP3 Converter",
    version="1.0",
    description="Extract audio from YouTube video and save as MP3.",
    executables=[target]
)
