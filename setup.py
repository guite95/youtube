from setuptools import setup, find_packages

setup(
    name="youtube-to-mp3",
    version="1.1.0",
    description="YouTube to MP3 Converter",
    author="guite95",
    packages=find_packages(),
    install_requires=[
        "yt-dlp",
        "PyQt6",
        "requests"
    ],
    entry_points={
        "gui_scripts": [
            "youtube-to-mp3=youtube_to_mp3:main",
        ],
    },
)
