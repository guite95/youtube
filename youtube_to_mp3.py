import os
import yt_dlp
from tkinter import Tk, filedialog

def download_youtube_audio():
    # 유튜브 링크 입력받기
    video_url = input("YouTube 링크를 입력하세요: ")
    
    # 사용자에게 저장할 경로 선택하도록 요청
    root = Tk()
    root.withdraw()  # Tkinter 창 숨기기
    save_path = filedialog.askdirectory(title="오디오 파일을 저장할 폴더를 선택하세요")
    
    if not save_path:
        print("저장 경로가 선택되지 않았습니다. 작업을 종료합니다.")
        return
    
    # 저장 경로와 파일 이름 설정
    output_template = os.path.join(save_path, '%(title)s.%(ext)s')
    
    ydl_opts = {
        'format': 'bestaudio/best',
        'postprocessors': [{
            'key': 'FFmpegExtractAudio',
            'preferredcodec': 'mp3',
            'preferredquality': '192',
        }],
        'outtmpl': output_template,
    }
    
    try:
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            ydl.download([video_url])
        print("오디오가 성공적으로 저장되었습니다.")
    except Exception as e:
        print(f"오류가 발생했습니다: {e}")

if __name__ == "__main__":
    download_youtube_audio()
