import os
import sys
import yt_dlp
import json
import requests
import subprocess
import re
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                            QHBoxLayout, QLineEdit, QPushButton, QProgressBar, 
                            QLabel, QFileDialog, QMessageBox, QStyle, QMenuBar, QMenu)
from PyQt6.QtCore import Qt, QThread, pyqtSignal
from PyQt6.QtGui import QIcon, QFont, QAction
from updater import check_for_updates
import tempfile
import shutil

# GitHub 저장소 정보
REPO_OWNER = "guite95"  # GitHub 사용자 이름으로 변경
REPO_NAME = "youtube"  # GitHub 저장소 이름으로 변경
CURRENT_VERSION = "1.1.0"  # 현재 버전

def sanitize_filename(filename):
    """파일 이름에서 사용할 수 없는 특수문자를 제거합니다."""
    # Windows에서 사용할 수 없는 문자들 제거
    invalid_chars = r'[<>:"/\\|?*]'
    filename = re.sub(invalid_chars, '', filename)
    
    # 앞뒤 공백 제거
    filename = filename.strip()
    
    # 빈 문자열이거나 점으로만 구성된 경우 기본값 반환
    if not filename or filename == '.' or filename == '..':
        return None
        
    return filename

class UpdateChecker:
    def __init__(self):
        self.latest_version = None
        self.download_url = None
        
    def check_for_updates(self):
        try:
            # GitHub API를 통해 최신 릴리즈 정보 가져오기
            api_url = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest"
            response = requests.get(api_url)
            if response.status_code == 200:
                release_info = response.json()
                self.latest_version = release_info['tag_name'].replace('v', '')
                self.download_url = release_info['assets'][0]['browser_download_url']
                return self.latest_version > CURRENT_VERSION
        except Exception as e:
            print(f"업데이트 확인 중 오류 발생: {str(e)}")
        return False
        
    def download_update(self, progress_callback=None):
        try:
            # 임시 파일로 다운로드
            response = requests.get(self.download_url, stream=True)
            total_size = int(response.headers.get('content-length', 0))
            
            temp_dir = tempfile.mkdtemp()
            temp_file = os.path.join(temp_dir, "update.zip")
            
            downloaded = 0
            with open(temp_file, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if progress_callback and total_size:
                            progress = int((downloaded / total_size) * 100)
                            progress_callback(progress)
            
            return temp_file
        except Exception as e:
            print(f"업데이트 다운로드 중 오류 발생: {str(e)}")
            return None

class DownloadWorker(QThread):
    progress = pyqtSignal(str)
    finished = pyqtSignal()
    error = pyqtSignal(str)

    def __init__(self, url, save_path, filename=None):
        super().__init__()
        self.url = url
        self.save_path = save_path
        self.filename = filename

    def run(self):
        if self.filename:
            # 사용자가 지정한 파일 이름 사용
            output_template = os.path.join(self.save_path, f'{self.filename}.%(ext)s')
        else:
            # 기본 동영상 제목 사용
            output_template = os.path.join(self.save_path, '%(title)s.%(ext)s')
            
        ydl_opts = {
            'format': 'bestaudio/best',
            'postprocessors': [{
                'key': 'FFmpegExtractAudio',
                'preferredcodec': 'mp3',
                'preferredquality': '192',
            }],
            'outtmpl': output_template,
            'progress_hooks': [self._progress_hook],
        }
        
        try:
            with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                ydl.download([self.url])
            self.finished.emit()
        except Exception as e:
            self.error.emit(str(e))

    def _progress_hook(self, d):
        if d['status'] == 'downloading':
            self.progress.emit(f"다운로드 중: {d.get('_percent_str', '0%')}")
        elif d['status'] == 'finished':
            self.progress.emit("변환 중...")

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle(f"YouTube to MP3 Converter v{CURRENT_VERSION}")
        self.setMinimumSize(600, 400)
        self.setup_ui()
        self.setup_menu()
        
        # 업데이트 체커 초기화
        self.update_checker = UpdateChecker()

    def setup_ui(self):
        # 메인 위젯 설정
        main_widget = QWidget()
        self.setCentralWidget(main_widget)
        layout = QVBoxLayout(main_widget)
        layout.setSpacing(20)
        layout.setContentsMargins(20, 20, 20, 20)

        # 버전 표시
        version_label = QLabel(f"버전: {CURRENT_VERSION}")
        version_label.setAlignment(Qt.AlignmentFlag.AlignRight)
        layout.addWidget(version_label)

        # URL 입력 영역
        url_layout = QHBoxLayout()
        self.url_input = QLineEdit()
        self.url_input.setPlaceholderText("YouTube URL을 입력하세요")
        self.url_input.setMinimumHeight(40)
        url_layout.addWidget(self.url_input)
        
        # 다운로드 버튼
        self.download_btn = QPushButton("다운로드")
        self.download_btn.setMinimumHeight(40)
        self.download_btn.clicked.connect(self.start_download)
        url_layout.addWidget(self.download_btn)
        layout.addLayout(url_layout)

        # 파일 이름 입력 영역
        filename_layout = QHBoxLayout()
        filename_label = QLabel("파일 이름:")
        filename_label.setMinimumHeight(40)
        filename_label.setMinimumWidth(80)
        filename_layout.addWidget(filename_label)
        
        self.filename_input = QLineEdit()
        self.filename_input.setPlaceholderText("파일 이름을 입력하세요 (선택사항, 비워두면 동영상 제목 사용)")
        self.filename_input.setMinimumHeight(40)
        filename_layout.addWidget(self.filename_input)
        layout.addLayout(filename_layout)

        # 저장 경로 선택 영역
        path_layout = QHBoxLayout()
        self.path_label = QLabel("저장 경로가 선택되지 않음")
        self.path_label.setMinimumHeight(40)
        path_layout.addWidget(self.path_label)
        
        self.browse_btn = QPushButton("경로 선택")
        self.browse_btn.setMinimumHeight(40)
        self.browse_btn.clicked.connect(self.select_save_path)
        path_layout.addWidget(self.browse_btn)
        layout.addLayout(path_layout)

        # 진행 상태 표시
        self.progress_bar = QProgressBar()
        self.progress_bar.setMinimumHeight(30)
        self.progress_bar.setTextVisible(True)
        self.progress_bar.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.progress_bar)

        # 상태 메시지
        self.status_label = QLabel("")
        self.status_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.status_label)

        # 스타일 설정
        self.setStyleSheet("""
            QMainWindow {
                background-color: #f0f0f0;
            }
            QLineEdit {
                padding: 5px;
                border: 2px solid #ccc;
                border-radius: 5px;
                font-size: 14px;
            }
            QPushButton {
                background-color: #2196F3;
                color: white;
                border: none;
                border-radius: 5px;
                padding: 5px 15px;
                font-size: 14px;
            }
            QPushButton:hover {
                background-color: #1976D2;
            }
            QProgressBar {
                border: 2px solid #ccc;
                border-radius: 5px;
                text-align: center;
            }
            QProgressBar::chunk {
                background-color: #2196F3;
            }
            QLabel {
                font-size: 14px;
            }
        """)

    def setup_menu(self):
        menubar = self.menuBar()
        
        # 도움말 메뉴
        help_menu = menubar.addMenu("도움말")
        
        # 업데이트 확인 액션
        update_action = QAction("업데이트 확인", self)
        update_action.triggered.connect(self.check_updates)
        help_menu.addAction(update_action)
        
        # 버전 정보 액션
        version_action = QAction(f"버전 정보 (v{CURRENT_VERSION})", self)
        version_action.setEnabled(False)
        help_menu.addAction(version_action)

    def check_updates(self):
        if self.update_checker.check_for_updates():
            reply = QMessageBox.question(
                self,
                "업데이트 가능",
                f"새로운 버전({self.update_checker.latest_version})이 있습니다. 지금 업데이트하시겠습니까?",
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
            )
            
            if reply == QMessageBox.StandardButton.Yes:
                self.start_update()

    def start_update(self):
        self.status_label.setText("업데이트 다운로드 중...")
        self.progress_bar.setValue(0)
        
        def update_progress(progress):
            self.progress_bar.setValue(progress)
        
        temp_file = self.update_checker.download_update(update_progress)
        if temp_file:
            try:
                # 현재 실행 파일의 경로
                current_exe = sys.executable
                
                # 업데이트 스크립트 생성
                update_script = os.path.join(os.path.dirname(temp_file), "update.bat")
                with open(update_script, "w") as f:
                    f.write(f"""@echo off
timeout /t 2 /nobreak
del "{current_exe}"
powershell Expand-Archive -Path "{temp_file}" -DestinationPath "{os.path.dirname(current_exe)}" -Force
start "" "{current_exe}"
del "%~f0"
""")
                
                # 업데이트 스크립트 실행
                subprocess.Popen([update_script], shell=True)
                QApplication.quit()
            except Exception as e:
                QMessageBox.critical(self, "업데이트 실패", f"업데이트 중 오류가 발생했습니다: {str(e)}")
        else:
            QMessageBox.critical(self, "업데이트 실패", "업데이트 파일을 다운로드할 수 없습니다.")

    def select_save_path(self):
        folder = QFileDialog.getExistingDirectory(self, "저장할 폴더 선택")
        if folder:
            self.save_path = folder
            self.path_label.setText(f"저장 경로: {folder}")

    def start_download(self):
        if not hasattr(self, 'save_path'):
            QMessageBox.warning(self, "경고", "저장 경로를 선택해주세요.")
            return

        url = self.url_input.text().strip()
        if not url:
            QMessageBox.warning(self, "경고", "YouTube URL을 입력해주세요.")
            return

        # 파일 이름 가져오기 (빈 문자열이면 None으로 설정)
        filename = self.filename_input.text().strip()
        if filename:
            filename = sanitize_filename(filename)
            if not filename:
                QMessageBox.warning(self, "경고", "유효하지 않은 파일 이름입니다. 특수문자를 제거해주세요.")
                return
        else:
            filename = None

        self.download_btn.setEnabled(False)
        self.progress_bar.setValue(0)
        self.status_label.setText("다운로드 준비 중...")

        self.worker = DownloadWorker(url, self.save_path, filename)
        self.worker.progress.connect(self.update_progress)
        self.worker.finished.connect(self.download_finished)
        self.worker.error.connect(self.download_error)
        self.worker.start()

    def update_progress(self, message):
        self.status_label.setText(message)
        if "%" in message:
            try:
                percent = int(message.split("%")[0].strip())
                self.progress_bar.setValue(percent)
            except:
                pass

    def download_finished(self):
        self.download_btn.setEnabled(True)
        self.progress_bar.setValue(100)
        
        # 사용자가 지정한 파일 이름이 있으면 표시
        if hasattr(self.worker, 'filename') and self.worker.filename:
            self.status_label.setText(f"다운로드 완료! 파일명: {self.worker.filename}.mp3")
            QMessageBox.information(self, "완료", f"오디오가 성공적으로 저장되었습니다.\n파일명: {self.worker.filename}.mp3")
        else:
            self.status_label.setText("다운로드 완료!")
            QMessageBox.information(self, "완료", "오디오가 성공적으로 저장되었습니다.")

    def download_error(self, error_message):
        self.download_btn.setEnabled(True)
        self.status_label.setText("오류 발생!")
        QMessageBox.critical(self, "오류", f"다운로드 중 오류가 발생했습니다:\n{error_message}")

def main():
    """메인 애플리케이션 실행 함수"""
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
