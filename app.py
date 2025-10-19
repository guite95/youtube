import os
import yt_dlp
import urllib.parse
from flask import (Flask, render_template, request, send_file, 
                   jsonify, session, redirect, url_for)

# Flask 애플리케이션을 생성합니다.
app = Flask(__name__)

# --- 환경 변수에서 시크릿 키와 비밀번호를 불러옵니다 ---
# 이 값들은 서버 실행 시 터미널에서 직접 주입하거나 (로컬 테스트)
# AWS Elastic Beanstalk의 환경 속성에서 설정합니다 (배포 환경).
app.secret_key = os.environ.get('SECRET_KEY')
PASSWORD = os.environ.get('PASSWORD')

# MP3 파일을 임시로 저장할 폴더를 설정합니다.
TEMP_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'temp')
if not os.path.exists(TEMP_DIR):
    os.makedirs(TEMP_DIR)


@app.route('/')
def index():
    """
    메인 페이지입니다. 사용자가 로그인 상태인지 확인하고,
    로그인하지 않았다면 로그인 페이지로 리디렉션합니다.
    """
    if not session.get('logged_in'):
        return redirect(url_for('login'))
    return render_template('index.html')


@app.route('/login', methods=['GET', 'POST'])
def login():
    """
    GET 요청 시 로그인 페이지를 보여주고,
    POST 요청 시 제출된 비밀번호를 확인합니다.
    """
    error = None
    if request.method == 'POST':
        if request.form['password'] == PASSWORD:
            # 비밀번호가 맞으면 세션에 로그인 상태를 기록합니다.
            session['logged_in'] = True
            return redirect(url_for('index'))
        else:
            error = '비밀번호가 올바르지 않습니다.'
    return render_template('login.html', error=error)


@app.route('/logout')
def logout():
    """
    세션에서 로그인 정보를 제거하여 사용자를 로그아웃시킵니다.
    """
    session.pop('logged_in', None)
    return redirect(url_for('login'))


@app.route('/download', methods=['POST'])
def download():
    """
    유튜브 URL을 받아 오디오를 MP3로 변환하고 사용자에게 전송합니다.
    이 기능은 로그인한 사용자만 접근할 수 있습니다.
    """
    if not session.get('logged_in'):
        return jsonify({'error': '인증되지 않은 접근입니다.'}), 401

    data = request.get_json()
    url = data.get('url')

    if not url:
        return jsonify({'error': 'URL이 제공되지 않았습니다.'}), 400

    try:
        ydl_opts = {
            'format': 'bestaudio/best',
            'postprocessors': [{
                'key': 'FFmpegExtractAudio',
                'preferredcodec': 'mp3',
                'preferredquality': '192',
            }],
            'outtmpl': os.path.join(TEMP_DIR, '%(title)s.%(ext)s'),
            'restrictfilenames': True,
        }

        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            info_dict = ydl.extract_info(url, download=True)
            original_filepath = ydl.prepare_filename(info_dict)
            base, _ = os.path.splitext(original_filepath)
            mp3_filepath = base + '.mp3'

        if os.path.exists(mp3_filepath):
            filename = os.path.basename(mp3_filepath)
            encoded_filename = urllib.parse.quote(filename.encode('utf-8'))
            
            response = send_file(
                mp3_filepath,
                as_attachment=True,
                download_name=filename
            )
            
            response.headers["Content-Disposition"] = f"attachment; filename*=UTF-8''{encoded_filename}"
            
            # 파일 전송이 완료된 후 서버에서 파일을 삭제하는 함수를 등록합니다.
            @response.call_on_close
            def cleanup():
                try:
                    os.remove(mp3_filepath)
                except OSError as e:
                    print(f"Error removing file: {e.strerror}")

            return response
        else:
            return jsonify({'error': 'MP3 파일을 생성하지 못했습니다.'}), 500

    except Exception as e:
        return jsonify({'error': f'변환 중 오류가 발생했습니다: {str(e)}'}), 500


# 이 스크립트가 직접 실행될 때만 Flask 개발 서버를 실행합니다.
if __name__ == '__main__':
    app.run(debug=True)