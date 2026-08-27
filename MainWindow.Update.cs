using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace ytdown
{
    public partial class MainWindow
    {
        private static readonly HttpClient UpdateCheckHttp = CreateUpdateCheckHttpClient();
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/guite95/youtube/releases/latest";
        private const string ReleaseManifestName = "release.manifest";

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            VersionLabel.Text = $"현재 버전 {GetInstalledVersionDisplay()}";
        }

        private async void UpdateCheckBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateCheckBtn.IsEnabled = false;
            string originalText = UpdateCheckBtn.Content?.ToString() ?? "업데이트 확인";
            UpdateCheckBtn.Content = "확인 중...";

            try
            {
                ManualUpdateCheckResult result = await CheckLatestReleaseAsync();

                if (!result.Success)
                {
                    MessageBox.Show(
                        this,
                        result.ErrorMessage ?? "업데이트 정보를 확인하지 못했습니다.",
                        "업데이트 확인",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (result.UpdateAvailable)
                {
                    // 실제 다운로드/검증/교체는 기존 UpdateService에 맡깁니다.
                    await UpdateService.CheckForUpdatesAsync(this);
                }
                else
                {
                    MessageBox.Show(
                        this,
                        $"현재 최신 버전을 사용하고 있습니다.\n\n현재 버전: {result.CurrentVersionText}",
                        "업데이트 확인",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"업데이트 확인 중 오류가 발생했습니다.\n\n{ex.Message}",
                    "업데이트 확인",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                UpdateCheckBtn.Content = originalText;
                UpdateCheckBtn.IsEnabled = true;
            }
        }

        private static async Task<ManualUpdateCheckResult> CheckLatestReleaseAsync()
        {
            if (!TryReadInstalledVersion(out string currentText, out Version currentVersion))
            {
                return ManualUpdateCheckResult.Fail(
                    "현재 설치 버전을 확인할 수 없습니다. release.manifest 파일이 있는지 확인해주세요.");
            }

            try
            {
                using HttpResponseMessage response = await UpdateCheckHttp.GetAsync(LatestReleaseApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return ManualUpdateCheckResult.Fail(
                        $"GitHub에서 업데이트 정보를 가져오지 못했습니다. (HTTP {(int)response.StatusCode})");
                }

                string json = await response.Content.ReadAsStringAsync();
                GitHubLatestRelease? release = JsonSerializer.Deserialize<GitHubLatestRelease>(json);
                if (release == null || release.Draft || release.Prerelease ||
                    !TryParseReleaseVersion(release.TagName, out Version latestVersion))
                {
                    return ManualUpdateCheckResult.Fail("최신 버전 정보를 해석하지 못했습니다.");
                }

                return new ManualUpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = latestVersion > currentVersion,
                    CurrentVersionText = $"v{currentText.TrimStart('v', 'V')}",
                    LatestVersionText = release.TagName,
                };
            }
            catch (HttpRequestException)
            {
                return ManualUpdateCheckResult.Fail(
                    "인터넷 연결 또는 GitHub 접속 상태를 확인해주세요.");
            }
            catch (TaskCanceledException)
            {
                return ManualUpdateCheckResult.Fail(
                    "업데이트 확인 시간이 초과되었습니다. 잠시 후 다시 시도해주세요.");
            }
        }

        private static string GetInstalledVersionDisplay()
        {
            return TryReadInstalledVersion(out string text, out _)
                ? $"v{text.TrimStart('v', 'V')}"
                : "확인 불가";
        }

        private static bool TryReadInstalledVersion(out string text, out Version version)
        {
            text = string.Empty;
            version = new Version(0, 0);

            string manifestPath = Path.Combine(AppContext.BaseDirectory, ReleaseManifestName);
            if (!File.Exists(manifestPath))
                return false;

            string? line = File.ReadLines(manifestPath)
                .FirstOrDefault(x => x.StartsWith("version=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(line))
                return false;

            text = line[(line.IndexOf('=') + 1)..].Trim();
            return TryParseReleaseVersion(text, out version);
        }

        private static bool TryParseReleaseVersion(string? value, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim().TrimStart('v', 'V');
            int suffixIndex = normalized.IndexOf('-');
            if (suffixIndex >= 0)
                normalized = normalized[..suffixIndex];

            if (!Version.TryParse(normalized, out Version? parsed) || parsed == null)
                return false;

            version = parsed;
            return true;
        }

        private static HttpClient CreateUpdateCheckHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ytdown-update-check/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private sealed class GitHubLatestRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }
        }

        private sealed class ManualUpdateCheckResult
        {
            public bool Success { get; set; }
            public bool UpdateAvailable { get; set; }
            public string CurrentVersionText { get; set; } = string.Empty;
            public string LatestVersionText { get; set; } = string.Empty;
            public string? ErrorMessage { get; set; }

            public static ManualUpdateCheckResult Fail(string message)
                => new() { Success = false, ErrorMessage = message };
        }
    }
}
