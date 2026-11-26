using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace BTController.App.Services;

public sealed record UpdateCheckResult(
    bool IsSuccess,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion = null,
    string? ReleaseNotes = null,
    string? DownloadUrl = null,
    string? AssetFileName = null,
    long AssetSize = 0,
    string? HtmlUrl = null,
    string? ErrorMessage = null);

public sealed class UpdateService
{
    private const string RepoOwner = "pastur253";
    private const string RepoName = "BTController";
    private const string LatestReleaseApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    public UpdateCheckResult? LastResult { get; private set; }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"BTController-Windows/{GetCurrentVersionStatic()}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        return client;
    }

    public string GetCurrentVersion() => GetCurrentVersionStatic();

    private static string GetCurrentVersionStatic()
    {
        try
        {
            var pkgVersion = Package.Current.Id.Version;
            return $"{pkgVersion.Major}.{pkgVersion.Minor}.{pkgVersion.Build}";
        }
        catch
        {
            var asmVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (asmVersion != null)
            {
                return $"{asmVersion.Major}.{asmVersion.Minor}.{Math.Max(0, asmVersion.Build)}";
            }
            return "1.0.0";
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string currentVersion = GetCurrentVersion();

        try
        {
            using var response = await Http.GetAsync(LatestReleaseApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    IsSuccess: false,
                    IsUpdateAvailable: false,
                    CurrentVersion: currentVersion,
                    ErrorMessage: $"Ошибка GitHub API: HTTP {(int)response.StatusCode}");
            }

            string jsonText = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString()?.Trim() ?? ""
                : "";
            string htmlUrl = root.TryGetProperty("html_url", out var htmlProp)
                ? htmlProp.GetString() ?? $"https://github.com/{RepoOwner}/{RepoName}/releases"
                : $"https://github.com/{RepoOwner}/{RepoName}/releases";
            string releaseNotes = root.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString()?.Trim() ?? ""
                : "";

            string? downloadUrl = null;
            string? assetFileName = null;
            long assetSize = 0;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                int bestScore = -1;
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    string lower = name.ToLowerInvariant();
                    string url = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                    long size = asset.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var s) ? s : 0;

                    if (string.IsNullOrWhiteSpace(url)) continue;

                    int score = -1;
                    if (lower == "btcontroller-setup.exe") score = 100;
                    else if (lower.EndsWith(".exe")) score = 50;
                    else if (lower.EndsWith(".msix")) score = 10;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        downloadUrl = url;
                        assetFileName = name;
                        assetSize = size;
                    }
                }
            }

            string cleanRemote = CleanVersion(tagName);
            string cleanCurrent = CleanVersion(currentVersion);

            if (string.IsNullOrWhiteSpace(cleanRemote))
            {
                return new UpdateCheckResult(
                    IsSuccess: false,
                    IsUpdateAvailable: false,
                    CurrentVersion: currentVersion,
                    ErrorMessage: "Не удалось определить версию последнего релиза");
            }

            bool isNewer = CompareVersions(cleanRemote, cleanCurrent) > 0;

            var result = new UpdateCheckResult(
                IsSuccess: true,
                IsUpdateAvailable: isNewer,
                CurrentVersion: currentVersion,
                LatestVersion: cleanRemote,
                ReleaseNotes: releaseNotes,
                DownloadUrl: downloadUrl,
                AssetFileName: assetFileName,
                AssetSize: assetSize,
                HtmlUrl: htmlUrl);
            LastResult = result;
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                IsSuccess: false,
                IsUpdateAvailable: false,
                CurrentVersion: currentVersion,
                ErrorMessage: $"Не удалось проверить обновления: {ex.Message}");
        }
    }

    public async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string fileName,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string updateDir = Path.Combine(Path.GetTempPath(), "BTController-Update");
        Directory.CreateDirectory(updateDir);

        string safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "BTController-Setup.exe" : fileName);
        string destinationPath = Path.Combine(updateDir, safeFileName);
        string tempPath = destinationPath + ".download";

        using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    progress?.Report((double)totalRead / totalBytes.Value * 100.0);
                }
            }
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }
        File.Move(tempPath, destinationPath);
        progress?.Report(100.0);

        return destinationPath;
    }

    public void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });
    }

    private static string CleanVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return string.Empty;
        string trimmed = version.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }
        int dashIdx = trimmed.IndexOf('-');
        if (dashIdx >= 0)
        {
            trimmed = trimmed[..dashIdx];
        }
        return trimmed.Trim();
    }

    private static int CompareVersions(string left, string right)
    {
        var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int maxLen = Math.Max(leftParts.Length, rightParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int l = i < leftParts.Length && int.TryParse(leftParts[i], out var lv) ? lv : 0;
            int r = i < rightParts.Length && int.TryParse(rightParts[i], out var rv) ? rv : 0;
            if (l != r) return l.CompareTo(r);
        }

        return 0;
    }
}
