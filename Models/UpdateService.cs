using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace IsSubtitled.Models;

public enum UpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Error,
}

public sealed record UpdateInfo(
    string Version,
    string Tag,
    string Title,
    string ReleaseNotes,
    string HtmlUrl,
    string DownloadUrl,
    string AssetName,
    long AssetSize,
    DateTime PublishedAt);

public static class AppVersion
{
    private static string? _currentVersion;

    public static string Current
    {
        get
        {
            if (_currentVersion is not null) return _currentVersion;

            var asm = typeof(AppVersion).Assembly;
            var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoAttr?.InformationalVersion is { } info && !string.IsNullOrWhiteSpace(info))
            {
                _currentVersion = info.Split('+')[0].Trim();
            }
            else
            {
                var ver = asm.GetName().Version;
                _currentVersion = ver is not null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
            }
            return _currentVersion;
        }
        internal set => _currentVersion = value;
    }

    /// <summary>Returns 1 if a > b, -1 if a &lt; b, 0 if equal.</summary>
    public static int CompareSemVer(string a, string b)
    {
        var (aMaj, aMin, aPatch) = Parse(a);
        var (bMaj, bMin, bPatch) = Parse(b);

        if (aMaj != bMaj) return aMaj.CompareTo(bMaj);
        if (aMin != bMin) return aMin.CompareTo(bMin);
        return aPatch.CompareTo(bPatch);
    }

    public static bool IsNewer(string remoteVersion, string localVersion) =>
        CompareSemVer(remoteVersion, localVersion) > 0;

    private static (int Major, int Minor, int Patch) Parse(string ver)
    {
        if (string.IsNullOrWhiteSpace(ver)) return (0, 0, 0);
        var clean = ver.Trim().TrimStart('v', 'V').Split('-')[0];
        var parts = clean.Split('.');
        int maj = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        int min = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return (maj, min, patch);
    }
}

public class UpdateService
{
    public const string DefaultRepo = "gitnasr/is-subtitled";
    private readonly HttpClient _http;
    private readonly string _repo;

    public UpdateService(HttpClient? httpClient = null, string repo = DefaultRepo)
    {
        _http = httpClient ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "IsSubtitled-AutoUpdater");
        }
        _repo = repo;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(string? currentVersionOverride = null, CancellationToken ct = default)
    {
        var currentVersion = currentVersionOverride ?? AppVersion.Current;
        var url = $"https://api.github.com/repos/{_repo}/releases/latest";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github.v3+json");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseReleaseJson(json, currentVersion);
    }

    public UpdateInfo? ParseReleaseJson(string json, string currentVersion)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var remoteVersion = tagName.TrimStart('v', 'V');
        if (string.IsNullOrWhiteSpace(remoteVersion)) return null;

        if (!AppVersion.IsNewer(remoteVersion, currentVersion))
            return null;

        var title = root.TryGetProperty("name", out var n) ? n.GetString() ?? tagName : tagName;
        var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        var htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        var publishedAt = root.TryGetProperty("published_at", out var p) && p.TryGetDateTime(out var dt)
            ? dt
            : DateTime.UtcNow;

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        var asset = PickBestAsset(assets);
        if (asset is null) return null;

        return new UpdateInfo(
            Version: remoteVersion,
            Tag: tagName,
            Title: title,
            ReleaseNotes: body,
            HtmlUrl: htmlUrl,
            DownloadUrl: asset.Value.Url,
            AssetName: asset.Value.Name,
            AssetSize: asset.Value.Size,
            PublishedAt: publishedAt);
    }

    public static (string Name, string Url, long Size)? PickBestAsset(JsonElement assets)
    {
        var isWindows = OperatingSystem.IsWindows();
        var isMac = OperatingSystem.IsMacOS();
        var isArm = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        (string Name, string Url, long Size)? candidate = null;

        foreach (var item in assets.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var url = item.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            var size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

            if (string.IsNullOrWhiteSpace(url)) continue;

            if (isWindows)
            {
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return (name, url, size);
            }
            else if (isMac)
            {
                if (isArm && name.Contains("osx-arm64", StringComparison.OrdinalIgnoreCase))
                    return (name, url, size);
                if (!isArm && name.Contains("osx-x64", StringComparison.OrdinalIgnoreCase))
                    return (name, url, size);
            }
            else // Linux
            {
                if (name.Contains("linux", StringComparison.OrdinalIgnoreCase))
                    return (name, url, size);
            }

            candidate ??= (name, url, size);
        }

        return candidate;
    }

    public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "IsSubtitled_Updates", update.Version);
        Directory.CreateDirectory(tempDir);
        var targetFile = Path.Combine(tempDir, update.AssetName);

        // If already fully downloaded, reuse
        if (File.Exists(targetFile) && new FileInfo(targetFile).Length == update.AssetSize && update.AssetSize > 0)
        {
            progress?.Report(100.0);
            return targetFile;
        }

        using var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.AssetSize;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            totalRead += read;
            if (totalBytes > 0)
            {
                double pct = (double)totalRead / totalBytes * 100.0;
                progress?.Report(Math.Min(100.0, pct));
            }
        }

        progress?.Report(100.0);
        return targetFile;
    }

    public void ApplyUpdateAndRestart(string downloadedFilePath)
    {
        var currentProcess = Process.GetCurrentProcess();
        var currentExePath = Environment.ProcessPath ?? currentProcess.MainModule?.FileName;

        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            throw new InvalidOperationException("Could not locate running executable path.");

        if (OperatingSystem.IsWindows())
        {
            // Windows updater: Use a hidden cmd script to wait for process exit, overwrite exe, and relaunch
            var pid = currentProcess.Id;
            var script = $"@echo off\r\n" +
                         $":waitloop\r\n" +
                         $"tasklist /FI \"PID eq {pid}\" 2>NUL | find /I \"{pid}\" >NUL\r\n" +
                         $"if not errorlevel 1 (\r\n" +
                         $"    timeout /T 1 /NOBREAK >NUL\r\n" +
                         $"    goto waitloop\r\n" +
                         $")\r\n" +
                         $"copy /Y \"{downloadedFilePath}\" \"{currentExePath}\" >NUL\r\n" +
                         $"start \"\" \"{currentExePath}\"\r\n" +
                         $"del \"%~f0\"\r\n";

            var scriptPath = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.bat");
            File.WriteAllText(scriptPath, script);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(psi);
            Environment.Exit(0);
        }
        else
        {
            // Unix/macOS: replace binary and restart
            try
            {
                File.Copy(downloadedFilePath, currentExePath, true);
                Process.Start(currentExePath);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply update: {ex.Message}", ex);
            }
        }
    }
}
