using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace ytdown;

internal static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/guite95/youtube/releases/latest";
    private const string PackageAssetName = "ytdown-win-x64.zip";
    private const string ChecksumAssetName = "ytdown-win-x64.zip.sha256";
    private const string ManifestFileName = "release.manifest";

    private static readonly HttpClient Http = CreateHttpClient();

    public static async Task CheckForUpdatesAsync(Window owner)
    {
        if (!TryReadInstalledVersion(AppContext.BaseDirectory, out string currentVersionText, out Version currentVersion))
            return;

        GitHubRelease? release;
        try
        {
            using var response = await Http.GetAsync(LatestReleaseUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return;

            string json = await response.Content.ReadAsStringAsync();
            release = JsonSerializer.Deserialize<GitHubRelease>(json);
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (release == null || release.Draft || release.Prerelease)
            return;

        if (!TryParseVersion(release.TagName, out Version latestVersion) || latestVersion <= currentVersion)
            return;

        GitHubAsset? packageAsset = release.Assets?.FirstOrDefault(a =>
            string.Equals(a.Name, PackageAssetName, StringComparison.OrdinalIgnoreCase));
        GitHubAsset? checksumAsset = release.Assets?.FirstOrDefault(a =>
            string.Equals(a.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase));

        if (packageAsset == null || checksumAsset == null)
            return;

        MessageBoxResult answer = MessageBox.Show(
            owner,
            $"새 버전 {release.TagName}이 있습니다.\n\n현재 버전: {currentVersionText}\n새 버전: {release.TagName}\n\n지금 업데이트하면 새 버전을 자동으로 다운로드한 뒤 프로그램이 재시작됩니다.",
            "업데이트 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            await DownloadAndApplyAsync(release.TagName, packageAsset.BrowserDownloadUrl, checksumAsset.BrowserDownloadUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                $"업데이트를 적용하지 못했습니다. 기존 프로그램은 그대로 유지됩니다.\n\n{ex.Message}",
                "업데이트 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task DownloadAndApplyAsync(string tagName, string packageUrl, string checksumUrl)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string updateRoot = Path.Combine(localAppData, "ytdown", "updates");
        Directory.CreateDirectory(updateRoot);

        string updateDirectory = Path.Combine(
            updateRoot,
            $"{SanitizeForPath(tagName)}-{Guid.NewGuid():N}");
        string stagingDirectory = Path.Combine(updateDirectory, "staging");
        string zipPath = Path.Combine(updateDirectory, PackageAssetName);

        Directory.CreateDirectory(updateDirectory);
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            await DownloadFileAsync(packageUrl, zipPath);

            string checksumText = await Http.GetStringAsync(checksumUrl);
            string expectedHash = checksumText
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?
                .Trim()
                .ToLowerInvariant()
                ?? throw new InvalidDataException("업데이트 체크섬 파일이 비어 있습니다.");

            await using (var zipStream = File.OpenRead(zipPath))
            {
                byte[] hash = await SHA256.HashDataAsync(zipStream);
                string actualHash = Convert.ToHexString(hash).ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("업데이트 파일의 SHA-256 검증에 실패했습니다.");
            }

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, stagingDirectory, overwriteFiles: true));

            string stagedExe = Path.Combine(stagingDirectory, "ytdown.exe");
            if (!File.Exists(stagedExe))
                throw new InvalidDataException("업데이트 패키지에 ytdown.exe가 없습니다.");

            if (!TryReadInstalledVersion(stagingDirectory, out string stagedVersionText, out Version stagedVersion) ||
                !TryParseVersion(tagName, out Version expectedVersion) ||
                stagedVersion != expectedVersion)
            {
                throw new InvalidDataException(
                    $"업데이트 패키지 버전이 일치하지 않습니다. ({stagedVersionText} / {tagName})");
            }

            LaunchUpdater(stagingDirectory);
        }
        catch
        {
            TryDeleteDirectory(updateDirectory);
            throw;
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 128,
            useAsync: true);

        await input.CopyToAsync(output);
    }

    private static void LaunchUpdater(string stagingDirectory)
    {
        string targetDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        string? parentDirectory = Path.GetDirectoryName(targetDirectory);
        if (string.IsNullOrWhiteSpace(parentDirectory))
            throw new InvalidOperationException("설치 경로를 확인할 수 없습니다.");

        string scriptPath = Path.Combine(Path.GetTempPath(), $"ytdown-update-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, UpdaterPowerShell, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        bool requireElevation = !CanWriteDirectory(parentDirectory);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = requireElevation,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (requireElevation)
            psi.Verb = "runas";
        else
            psi.CreateNoWindow = true;

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-ParentPid");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add("-StagingDir");
        psi.ArgumentList.Add(stagingDirectory);
        psi.ArgumentList.Add("-TargetDir");
        psi.ArgumentList.Add(targetDirectory);

        Process.Start(psi) ?? throw new InvalidOperationException("업데이트 프로세스를 시작하지 못했습니다.");
        Application.Current.Shutdown();
    }

    private static bool TryReadInstalledVersion(string directory, out string versionText, out Version version)
    {
        versionText = string.Empty;
        version = new Version(0, 0);

        string manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
            return false;

        string? versionLine = File.ReadLines(manifestPath)
            .FirstOrDefault(line => line.StartsWith("version=", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(versionLine))
            return false;

        versionText = versionLine[(versionLine.IndexOf('=') + 1)..].Trim();
        return TryParseVersion(versionText, out version);
    }

    private static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string normalized = text.Trim().TrimStart('v', 'V');
        int suffixIndex = normalized.IndexOf('-');
        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];

        if (!Version.TryParse(normalized, out Version? parsed) || parsed == null)
            return false;

        version = parsed;
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ytdown-updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static bool CanWriteDirectory(string directory)
    {
        string probePath = Path.Combine(directory, $".ytdown-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            try { File.Delete(probePath); } catch { }
            return false;
        }
    }

    private static string SanitizeForPath(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }

    private const string UpdaterPowerShell = """
param(
    [Parameter(Mandatory = $true)][int]$ParentPid,
    [Parameter(Mandatory = $true)][string]$StagingDir,
    [Parameter(Mandatory = $true)][string]$TargetDir
)

$ErrorActionPreference = 'Stop'
$logRoot = Join-Path $env:LOCALAPPDATA 'ytdown'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$logPath = Join-Path $logRoot 'update.log'
$backupDir = "$TargetDir.__old_$([Guid]::NewGuid().ToString('N'))"
$newProcess = $null

try {
    Wait-Process -Id $ParentPid -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    $stagedExe = Join-Path $StagingDir 'ytdown.exe'
    if (-not (Test-Path -LiteralPath $stagedExe)) {
        throw 'Staged ytdown.exe was not found.'
    }

    if (Test-Path -LiteralPath $backupDir) {
        Remove-Item -LiteralPath $backupDir -Recurse -Force
    }

    # 기존 설치 폴더 자체를 백업 이름으로 이동한 뒤 새 폴더를 만듭니다.
    # 이렇게 하면 이전 버전의 DLL/tools 파일이 새 설치 폴더에 남을 수 없습니다.
    Move-Item -LiteralPath $TargetDir -Destination $backupDir
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

    Get-ChildItem -LiteralPath $StagingDir -Force |
        Copy-Item -Destination $TargetDir -Recurse -Force

    # 사용자 데이터만 명시적으로 보존합니다.
    foreach ($name in @('cookies.txt', 'settings.json')) {
        $oldData = Join-Path $backupDir $name
        if (Test-Path -LiteralPath $oldData) {
            Copy-Item -LiteralPath $oldData -Destination (Join-Path $TargetDir $name) -Force
        }
    }

    $newExe = Join-Path $TargetDir 'ytdown.exe'
    if (-not (Test-Path -LiteralPath $newExe)) {
        throw 'Updated ytdown.exe was not found.'
    }

    $newProcess = Start-Process -FilePath $newExe -WorkingDirectory $TargetDir -PassThru
    Start-Sleep -Seconds 2

    if ($newProcess.HasExited) {
        throw "Updated application exited immediately. ExitCode=$($newProcess.ExitCode)"
    }

    # 새 버전이 정상 기동된 뒤 백업과 staging을 삭제합니다.
    Remove-Item -LiteralPath $backupDir -Recurse -Force
    Remove-Item -LiteralPath $StagingDir -Recurse -Force
    "$(Get-Date -Format o) update succeeded" | Set-Content -LiteralPath $logPath -Encoding UTF8
}
catch {
    ($_ | Out-String) | Set-Content -LiteralPath $logPath -Encoding UTF8

    try {
        if ($null -ne $newProcess -and -not $newProcess.HasExited) {
            Stop-Process -Id $newProcess.Id -Force
        }
    } catch {}

    # 실패 시 새 폴더를 제거하고 기존 버전을 원상복구합니다.
    try {
        if (Test-Path -LiteralPath $TargetDir) {
            Remove-Item -LiteralPath $TargetDir -Recurse -Force
        }
    } catch {}

    try {
        if (Test-Path -LiteralPath $backupDir) {
            Move-Item -LiteralPath $backupDir -Destination $TargetDir
        }
    } catch {}

    try {
        $oldExe = Join-Path $TargetDir 'ytdown.exe'
        if (Test-Path -LiteralPath $oldExe) {
            Start-Process -FilePath $oldExe -WorkingDirectory $TargetDir
        }
    } catch {}
}
finally {
    try { Remove-Item -LiteralPath $PSCommandPath -Force } catch {}
}
""";

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
