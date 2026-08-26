using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ytdown
{
    public partial class MainWindow : Window
    {
        private string _fullSavePath = "";

        // URL 메타 디바운스/취소
        private CancellationTokenSource? _metaCts;
        private Process? _runningMetaProcess;

        // 다운로드/변환 취소
        private CancellationTokenSource? _downloadCts;
        private Process? _runningProcess; // 다운로드/변환 공용

        // 경로
        private readonly string _baseDir = AppContext.BaseDirectory;
        private string ToolsDir => Path.Combine(_baseDir, "tools");

        private string YtDlpPath => Path.Combine(ToolsDir, "yt-dlp.exe");
        private string FfmpegPath => Path.Combine(ToolsDir, "ffmpeg.exe");

        private string CookiesPath => Path.Combine(_baseDir, "cookies.txt");

        public MainWindow()
        {
            InitializeComponent();
            UrlTextBox.TextChanged += UrlTextBox_TextChanged;
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog { Filter = "MP3 Audio (*.mp3)|*.mp3" };
            if (saveFileDialog.ShowDialog() == true)
            {
                _fullSavePath = saveFileDialog.FileName;
                SavePathTextBox.Text = Path.GetFileName(_fullSavePath);
            }
        }

        // 크롬이 사용 중이어도 강제로 복사해오는 안전한 복사 메서드
        private void SafeCopy(string sourcePath, string destPath)
        {
            if (!File.Exists(sourcePath)) return;
            
            // FileShare.ReadWrite 덕분에 크롬이 켜져 있어도 에러 없이 복사 가능!
            using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                fs.CopyTo(dest);
            }
        }

        // 섀도우 카피용 임시 폴더 생성
        private string? GetTempChromeProfile()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string chromeUserData = Path.Combine(localAppData, @"Google\Chrome\User Data");
                
                // 매번 고유한 임시 폴더 생성 (충돌 방지)
                string tempProfile = Path.Combine(Path.GetTempPath(), $"yt_dlp_chrome_{Guid.NewGuid()}");
                string tempDefault = Path.Combine(tempProfile, "Default");
                string tempNetwork = Path.Combine(tempDefault, "Network");

                Directory.CreateDirectory(tempNetwork);

                string localStateSrc = Path.Combine(chromeUserData, "Local State");
                SafeCopy(localStateSrc, Path.Combine(tempProfile, "Local State"));

                string cookieNewSrc = Path.Combine(chromeUserData, @"Default\Network\Cookies");
                string cookieOldSrc = Path.Combine(chromeUserData, @"Default\Cookies");

                if (File.Exists(cookieNewSrc))
                    SafeCopy(cookieNewSrc, Path.Combine(tempNetwork, "Cookies"));
                else if (File.Exists(cookieOldSrc))
                    SafeCopy(cookieOldSrc, Path.Combine(tempDefault, "Cookies"));

                return tempProfile;
            }
            catch
            {
                return null;
            }
        }

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(_fullSavePath))
            {
                MessageBox.Show("주소와 저장 경로를 확인해주세요!");
                return;
            }

            // tools 필수 파일 체크
            if (!File.Exists(YtDlpPath))
            {
                MessageBox.Show("tools\\yt-dlp.exe가 없습니다.");
                return;
            }
            if (!File.Exists(FfmpegPath))
            {
                MessageBox.Show("tools\\ffmpeg.exe가 없습니다.");
                return;
            }

            DownloadBtn.IsEnabled = false;
            CancelBtn.Visibility = Visibility.Visible;
            OpenFolderBtn.Visibility = Visibility.Collapsed;
            DownloadProgress.Value = 0;
            StatusLabel.Text = "다운로드 준비 중...";

            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();

            string? tempChromePath = null;

            try
            {
                string outputBase = _fullSavePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    ? _fullSavePath.Substring(0, _fullSavePath.Length - 4)
                    : _fullSavePath;

                // 임시 크롬 프로필 복사 (시간 거의 안 걸림)
                tempChromePath = GetTempChromeProfile();

                var downloadPsi = BuildYtDlpDownloadPsi(url, outputBase, tempChromePath);

                var result = await RunProcessWithLinesAsync(
                    psi: downloadPsi,
                    token: _downloadCts.Token,
                    onStdoutLine: line =>
                    {
                        // 다운로드 진행률 (0~100%)
                        if (TryParsePercent(line, out double p))
                        {
                            SetOverallProgress(p, $"다운로드 중... {p:0.0}%");
                        }
                        // yt-dlp가 다운로드 끝내고 mp3 변환 시작할 때 뜨는 로그 감지
                        else if (line.Contains("[ExtractAudio]"))
                        {
                            SetOverallProgress(100, "오디오 변환 중... (잠시만 기다려주세요)");
                        }
                    },
                    onStderrLine: line => { /* 에러 로그 무시 */ });

                if (result.WasCanceled)
                {
                    StatusLabel.Text = "취소됨";
                    return;
                }

                if (result.ExitCode != 0 || !File.Exists(outputBase + ".mp3"))
                {
                    StatusLabel.Text = "실패";
                    MessageBox.Show($"다운로드 또는 변환에 실패했습니다.\n\n{TrimForDialog(result.Error)}");
                    return;
                }

                DownloadProgress.Value = 100;
                StatusLabel.Text = "완료!";
                OpenFolderBtn.Visibility = Visibility.Visible;
                MessageBox.Show("성공적으로 저장되었습니다!");
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "취소됨";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "오류";
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
            finally
            {
                // 🌟 핵심: 작업이 끝나면 생성했던 임시 크롬 프로필 폴더 삭제
                if (!string.IsNullOrEmpty(tempChromePath) && Directory.Exists(tempChromePath))
                {
                    try { Directory.Delete(tempChromePath, true); } catch { }
                }

                _runningProcess = null;
                DownloadBtn.IsEnabled = true;
                CancelBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _downloadCts?.Cancel();

                if (_runningProcess != null && !_runningProcess.HasExited)
                    _runningProcess.Kill(entireProcessTree: true);
            }
            catch { }
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_fullSavePath);
                if (!string.IsNullOrEmpty(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { }
        }

        // ====== URL 메타(제목/썸네일): 기존 로직 유지 + 디바운스/취소 ======
        private async void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (url.Length < 10) return;

            _metaCts?.Cancel();
            _metaCts = new CancellationTokenSource();
            var token = _metaCts.Token;

            try
            {
                await Task.Delay(700, token);

                try
                {
                    if (_runningMetaProcess != null && !_runningMetaProcess.HasExited)
                        _runningMetaProcess.Kill(entireProcessTree: true);
                }
                catch { }

                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;

                    // tools\yt-dlp.exe 사용 (인코딩은 건드리지 않음)
                    var proc = new ProcessStartInfo
                    {
                        FileName = YtDlpPath,
                        WorkingDirectory = ToolsDir,
                        Arguments = $"--get-thumbnail --get-title \"{url}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                    };

                    using var p = Process.Start(proc);
                    if (p == null) return;

                    _runningMetaProcess = p;

                    string? title = p.StandardOutput.ReadLine();
                    string? thumbUrl = p.StandardOutput.ReadLine();

                    if (token.IsCancellationRequested) return;

                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(title))
                            TitleLabel.Text = title;

                        if (!string.IsNullOrEmpty(thumbUrl))
                        {
                            ThumbnailBrush.ImageSource = new BitmapImage(new Uri(thumbUrl));
                            Placeholder.Visibility = Visibility.Collapsed;
                        }
                    });
                }, token);
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ====== Process 빌더 ======

        private ProcessStartInfo BuildYtDlpDownloadPsi(string url, string outputBase, string? tempChromePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                WorkingDirectory = ToolsDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // 1. 임시 복사한 크롬 프로필 먹여주기
            if (!string.IsNullOrEmpty(tempChromePath) && Directory.Exists(tempChromePath))
            {
                psi.ArgumentList.Add("--cookies-from-browser");
                psi.ArgumentList.Add($"chrome:{tempChromePath}");
            }
            else if (File.Exists(CookiesPath))
            {
                // 폴백: 임시 폴더 생성이 실패한 경우 기존 cookies.txt 적용
                psi.ArgumentList.Add("--cookies");
                psi.ArgumentList.Add(CookiesPath);
            }

            psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add("--newline");
            psi.ArgumentList.Add("--force-overwrites");

            // 2. yt-dlp에게 오디오 추출과 mp3 변환 완벽 위임!
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("bestaudio/best");
            psi.ArgumentList.Add("--extract-audio");
            psi.ArgumentList.Add("--audio-format");
            psi.ArgumentList.Add("mp3");
            psi.ArgumentList.Add("--audio-quality");
            psi.ArgumentList.Add("192K");

            // 3. 내장된 ffmpeg 경로 알려주기
            psi.ArgumentList.Add("--ffmpeg-location");
            psi.ArgumentList.Add(ToolsDir);

            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add($"{outputBase}.%(ext)s");
            psi.ArgumentList.Add(url);

            return psi;
        }

        // ====== Process 실행(라인 콜백 포함) ======

        private async Task<ProcessResult> RunProcessWithLinesAsync(
            ProcessStartInfo psi,
            CancellationToken token,
            Action<string>? onStdoutLine,
            Action<string>? onStderrLine)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _runningProcess = process;

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            process.Exited += (_, __) => tcs.TrySetResult(process.ExitCode);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                output.AppendLine(e.Data);
                onStdoutLine?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                error.AppendLine(e.Data);
                onStderrLine?.Invoke(e.Data);
            };

            if (!process.Start())
                return new ProcessResult { ExitCode = -1, Output = "", Error = "Process start failed." };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }
            }))
            {
                int exitCode = await tcs.Task;

                try
                {
                    // 이벤트로 못 받은 잔여 버퍼 회수
                    string restOut = process.StandardOutput.ReadToEnd();
                    string restErr = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(restOut)) output.AppendLine(restOut);
                    if (!string.IsNullOrWhiteSpace(restErr)) error.AppendLine(restErr);
                }
                catch { }

                // 이벤트 리더가 마무리될 시간을 주기 위해 WaitForExit 호출
                try { process.WaitForExit(); } catch { }

                return new ProcessResult
                {
                    ExitCode = exitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    WasCanceled = token.IsCancellationRequested
                };
            }
        }

        // ====== 유틸 ======

        private void SetOverallProgress(double overallPercent, string statusText)
        {
            overallPercent = Clamp(overallPercent, 0, 100);

            Dispatcher.BeginInvoke(() =>
            {
                DownloadProgress.Value = overallPercent;
                StatusLabel.Text = statusText;
            });
        }

        private static bool TryParsePercent(string line, out double percent)
        {
            percent = 0;

            var match = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
            if (!match.Success) return false;

            return double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out percent);
        }



        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);

        private static string TrimForDialog(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(오류 로그 없음)";
            s = s.Trim();
            return s.Length > 2000 ? s.Substring(0, 2000) + "\n...(생략)" : s;
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
            public bool WasCanceled { get; set; }
        }
    }

    // ProgressBar Indicator 폭 계산용 Converter (XAML Resources에 등록되어 있어야 함)
    public sealed class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3) return 0.0;

            double value = SafeToDouble(values[0]);
            double max = SafeToDouble(values[1]);
            double trackWidth = SafeToDouble(values[2]);

            if (max <= 0 || trackWidth <= 0) return 0.0;

            double ratio = value / max;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            return trackWidth * ratio;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static double SafeToDouble(object o)
        {
            try { return System.Convert.ToDouble(o, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }
    }
}
