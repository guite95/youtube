using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
        if (!TryReadInstalledVersion(AppContext.BaseDirectory, out string currentText, out Version current))
            return;

        GitHubRelease? release;
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(LatestReleaseUrl, HttpCompletionOption.ResponseHeadersRead);
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

        if (!TryParseVersion(release.TagName, out Version latest) || latest <= current)
            return;

        GitHubAsset? package = release.Assets?.FirstOrDefault(x =>
            string.Equals(x.Name, PackageAssetName, StringComparison.OrdinalIgnoreCase));
        GitHubAsset? checksum = release.Assets?.FirstOrDefault(x =>
            string.Equals(x.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase));

        if (package == null || checksum == null)
            return;

        MessageBoxResult answer = MessageBox.Show(
            owner,
            $"새 버전 {release.TagName}이 있습니다.\n\n현재 버전: {currentText}\n새 버전: {release.TagName}\n\n지금 업데이트하면 새 버전을 다운로드한 뒤 프로그램이 재시작됩니다.",
            "업데이트 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            await DownloadAndApplyAsync(release.TagName, package.BrowserDownloadUrl, checksum.BrowserDownloadUrl);
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
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ytdown",
            "updates");
        string updateDir = Path.Combine(root, $"{Sanitize(tagName)}-{Guid.NewGuid():N}");
        string stagingDir = Path.Combine(updateDir, "staging");
        string zipPath = Path.Combine(updateDir, PackageAssetName);

        Directory.CreateDirectory(stagingDir);

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

            string actualHash;
            using (var sha = SHA256.Create())
            await using (var stream = File.OpenRead(zipPath))
            {
                byte[] hash = sha.ComputeHash(stream);
                actualHash = Convert.ToHexString(hash).ToLowerInvariant();
            }

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("업데이트 파일의 SHA-256 검증에 실패했습니다.");

            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, stagingDir, overwriteFiles: true));

            if (!File.Exists(Path.Combine(stagingDir, "ytdown.exe")))
                throw new InvalidDataException("업데이트 패키지에 ytdown.exe가 없습니다.");

            if (!TryReadInstalledVersion(stagingDir, out string stagedText, out Version staged) ||
                !TryParseVersion(tagName, out Version expected) || staged != expected)
            {
                throw new InvalidDataException($"업데이트 패키지 버전이 일치하지 않습니다. ({stagedText} / {tagName})");
            }

            LaunchUpdater(stagingDir, updateDir);
        }
        catch
        {
            TryDeleteDirectory(updateDir);
            throw;
        }
    }

    private static async Task DownloadFileAsync(string url, string destination)
    {
        using HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using var output = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            useAsync: true);

        await input.CopyToAsync(output);
    }

    private static void LaunchUpdater(string stagingDir, string updateDir)
    {
        string targetDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        string? parentDir = Path.GetDirectoryName(targetDir);
        if (string.IsNullOrWhiteSpace(parentDir))
            throw new InvalidOperationException("설치 경로를 확인할 수 없습니다.");

        string scriptPath = Path.Combine(Path.GetTempPath(), $"ytdown-update-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, UpdaterPowerShell, new UTF8Encoding(false));

        bool elevate = !CanWriteDirectory(parentDir);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = elevate,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        if (elevate)
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
        psi.ArgumentList.Add(stagingDir);
        psi.ArgumentList.Add("-UpdateDir");
        psi.ArgumentList.Add(updateDir);
        psi.ArgumentList.Add("-TargetDir");
        psi.ArgumentList.Add(targetDir);

        Process? updater = Process.Start(psi);
        if (updater == null)
            throw new InvalidOperationException("업데이트 프로세스를 시작하지 못했습니다.");

        Application.Current.Shutdown();
    }

    private static bool TryReadInstalledVersion(string directory, out string text, out Version version)
    {
        text = string.Empty;
        version = new Version(0, 0);

        string manifest = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifest))
            return false;

        string? line = File.ReadLines(manifest)
            .FirstOrDefault(x => x.StartsWith("version=", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(line))
            return false;

        text = line[(line.IndexOf('=') + 1)..].Trim();
        return TryParseVersion(text, out version);
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().TrimStart('v', 'V');
        int suffix = normalized.IndexOf('-');
        if (suffix >= 0)
            normalized = normalized[..suffix];

        if (!Version.TryParse(normalized, out Version? parsed) || parsed == null)
            return false;

        version = parsed;
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ytdown-updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static bool CanWriteDirectory(string directory)
    {
        string probe = Path.Combine(directory, $".ytdown-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            try { File.Delete(probe); } catch { }
            return false;
        }
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
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
    [Parameter(Mandatory = $true)][string]$UpdateDir,
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

    Move-Item -LiteralPath $TargetDir -Destination $backupDir
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

    # 새 릴리스의 파일 목록만 새 설치 폴더에 복사합니다.
    # 기존 폴더를 통째로 옮겼기 때문에 폐기된 DLL/tools 파일은 남지 않습니다.
    Get-ChildItem -LiteralPath $StagingDir -Force |
        Copy-Item -Destination $TargetDir -Recurse -Force

    # 명시적으로 허용한 사용자 데이터만 이전 버전에서 복원합니다.
    foreach ($name in @('cookies.txt', 'settings.json')) {
        $source = Join-Path $backupDir $name
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $TargetDir $name) -Force
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

    Remove-Item -LiteralPath $backupDir -Recurse -Force
    Remove-Item -LiteralPath $UpdateDir -Recurse -Force
    "$(Get-Date -Format o) update succeeded" | Set-Content -LiteralPath $logPath -Encoding UTF8
}
catch {
    ($_ | Out-String) | Set-Content -LiteralPath $logPath -Encoding UTF8

    try {
        if ($null -ne $newProcess -and -not $newProcess.HasExited) {
            Stop-Process -Id $newProcess.Id -Force
        }
    } catch {}

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
