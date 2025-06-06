import os
import sys
import json
import requests
import subprocess
import tempfile
import shutil
from PyQt6.QtWidgets import QMessageBox, QProgressDialog
from PyQt6.QtCore import Qt, QThread, pyqtSignal

class Updater(QThread):
    progress = pyqtSignal(str)
    finished = pyqtSignal()
    error = pyqtSignal(str)

    def __init__(self, current_version):
        super().__init__()
        self.current_version = current_version
        self.repo_owner = "YOUR_GITHUB_USERNAME"  # GitHub 사용자 이름
        self.repo_name = "YOUR_REPO_NAME"  # 저장소 이름
        self.github_token = "YOUR_GITHUB_TOKEN"  # GitHub 개인 액세스 토큰

    def run(self):
        try:
            # 최신 릴리즈 정보 가져오기
            headers = {'Authorization': f'token {self.github_token}'}
            response = requests.get(
                f'https://api.github.com/repos/{self.repo_owner}/{self.repo_name}/releases/latest',
                headers=headers
            )
            response.raise_for_status()
            latest_release = response.json()
            latest_version = latest_release['tag_name']

            if latest_version > self.current_version:
                self.progress.emit("새로운 버전을 다운로드 중...")
                
                # 실행 파일 다운로드
                for asset in latest_release['assets']:
                    if asset['name'].endswith('.exe'):
                        download_url = asset['browser_download_url']
                        response = requests.get(download_url, headers=headers, stream=True)
                        response.raise_for_status()

                        # 임시 파일로 다운로드
                        with tempfile.NamedTemporaryFile(delete=False, suffix='.exe') as temp_file:
                            total_size = int(response.headers.get('content-length', 0))
                            downloaded = 0
                            
                            for chunk in response.iter_content(chunk_size=8192):
                                if chunk:
                                    temp_file.write(chunk)
                                    downloaded += len(chunk)
                                    progress = int((downloaded / total_size) * 100)
                                    self.progress.emit(f"다운로드 중... {progress}%")

                        # 현재 실행 파일 백업
                        current_exe = sys.executable
                        backup_exe = current_exe + '.backup'
                        shutil.copy2(current_exe, backup_exe)

                        # 새 버전으로 교체
                        try:
                            shutil.move(temp_file.name, current_exe)
                            os.remove(backup_exe)
                            self.finished.emit()
                        except Exception as e:
                            # 실패 시 백업에서 복구
                            shutil.move(backup_exe, current_exe)
                            raise e
            else:
                self.progress.emit("이미 최신 버전입니다.")
                self.finished.emit()

        except Exception as e:
            self.error.emit(str(e))

def check_for_updates(parent_widget, current_version):
    updater = Updater(current_version)
    
    progress_dialog = QProgressDialog("업데이트 확인 중...", "취소", 0, 0, parent_widget)
    progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
    progress_dialog.setWindowTitle("업데이트")
    progress_dialog.setCancelButton(None)
    progress_dialog.show()

    def update_progress(message):
        progress_dialog.setLabelText(message)

    def update_finished():
        progress_dialog.close()
        QMessageBox.information(parent_widget, "업데이트 완료", 
                              "프로그램을 다시 시작하여 업데이트를 적용하세요.")

    def update_error(error_message):
        progress_dialog.close()
        QMessageBox.critical(parent_widget, "업데이트 오류", 
                           f"업데이트 중 오류가 발생했습니다:\n{error_message}")

    updater.progress.connect(update_progress)
    updater.finished.connect(update_finished)
    updater.error.connect(update_error)
    updater.start() 