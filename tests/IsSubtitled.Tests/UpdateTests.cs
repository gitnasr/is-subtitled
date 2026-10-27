using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IsSubtitled.Models;
using Xunit;

namespace IsSubtitled.Tests;

public class UpdateTests
{
    [Theory]
    [InlineData("2.2.0", "2.1.1", true)]
    [InlineData("2.1.2", "2.1.1", true)]
    [InlineData("3.0.0", "2.9.9", true)]
    [InlineData("2.10.0", "2.9.0", true)]
    [InlineData("2.1.1", "2.1.1", false)]
    [InlineData("2.1.0", "2.1.1", false)]
    [InlineData("1.9.9", "2.0.0", false)]
    [InlineData("v2.2.0", "2.1.1", true)]
    [InlineData("2.2.0", "v2.1.1", true)]
    public void SemVerComparison_CorrectlyIdentifiesNewerVersions(string remote, string local, bool expected)
    {
        Assert.Equal(expected, AppVersion.IsNewer(remote, local));
    }

    [Fact]
    public void ParseReleaseJson_ExtractsUpdateInfoWhenNewer()
    {
        var sampleJson = """
        {
          "tag_name": "v2.2.0",
          "name": "Release v2.2.0",
          "body": "### Highlights\n* Auto-updater introduced",
          "html_url": "https://github.com/gitnasr/is-subtitled/releases/tag/v2.2.0",
          "published_at": "2026-10-27T14:26:19Z",
          "assets": [
            {
              "name": "IsSubtitled.exe",
              "size": 103615037,
              "browser_download_url": "https://github.com/gitnasr/is-subtitled/releases/download/v2.2.0/IsSubtitled.exe"
            },
            {
              "name": "IsSubtitled-linux-x64.tar.gz",
              "size": 36060490,
              "browser_download_url": "https://github.com/gitnasr/is-subtitled/releases/download/v2.2.0/IsSubtitled-linux-x64.tar.gz"
            }
          ]
        }
        """;

        var service = new UpdateService();
        var update = service.ParseReleaseJson(sampleJson, "2.1.1");

        Assert.NotNull(update);
        Assert.Equal("2.2.0", update.Version);
        Assert.Equal("v2.2.0", update.Tag);
        Assert.Equal("Release v2.2.0", update.Title);
        Assert.Contains("Auto-updater introduced", update.ReleaseNotes);
        Assert.NotEmpty(update.AssetName);
        Assert.True(update.AssetSize > 0);
        Assert.StartsWith("https://github.com/gitnasr/is-subtitled/releases/download/v2.2.0/", update.DownloadUrl);
    }

    [Fact]
    public void PickBestAsset_SelectsCorrectPlatformAsset()
    {
        var assetsJson = """
        [
          {
            "name": "IsSubtitled.exe",
            "size": 100,
            "browser_download_url": "https://example.com/IsSubtitled.exe"
          },
          {
            "name": "IsSubtitled-linux-x64.tar.gz",
            "size": 200,
            "browser_download_url": "https://example.com/IsSubtitled-linux-x64.tar.gz"
          },
          {
            "name": "IsSubtitled-osx-arm64.tar.gz",
            "size": 300,
            "browser_download_url": "https://example.com/IsSubtitled-osx-arm64.tar.gz"
          }
        ]
        """;

        using var doc = JsonDocument.Parse(assetsJson);
        var winAsset = UpdateService.PickBestAsset(doc.RootElement, isWindowsOverride: true, isMacOverride: false, isArmOverride: false);
        var linuxAsset = UpdateService.PickBestAsset(doc.RootElement, isWindowsOverride: false, isMacOverride: false, isArmOverride: false);
        var macArmAsset = UpdateService.PickBestAsset(doc.RootElement, isWindowsOverride: false, isMacOverride: true, isArmOverride: true);

        Assert.Equal("IsSubtitled.exe", winAsset?.Name);
        Assert.Equal("IsSubtitled-linux-x64.tar.gz", linuxAsset?.Name);
        Assert.Equal("IsSubtitled-osx-arm64.tar.gz", macArmAsset?.Name);
    }

    [Fact]
    public void ParseReleaseJson_ReturnsNullWhenCurrentOrOlder()
    {
        var sampleJson = """
        {
          "tag_name": "v2.1.1",
          "name": "Release v2.1.1",
          "body": "No changes",
          "assets": [
            {
              "name": "IsSubtitled.exe",
              "size": 1000,
              "browser_download_url": "https://example.com/IsSubtitled.exe"
            }
          ]
        }
        """;

        var service = new UpdateService();
        var update = service.ParseReleaseJson(sampleJson, "2.1.1");

        Assert.Null(update);
    }

    [Fact]
    public async Task CheckForUpdateAsync_DetectsNewerReleaseViaHttpClient()
    {
        var sampleJson = """
        {
          "tag_name": "v2.4.0",
          "name": "Release v2.4.0",
          "body": "### Bug Fixes\n* Restored header version pill",
          "html_url": "https://github.com/gitnasr/is-subtitled/releases/tag/v2.4.0",
          "published_at": "2026-10-27T15:00:00Z",
          "assets": [
            {
              "name": "IsSubtitled.exe",
              "size": 50000,
              "browser_download_url": "https://github.com/gitnasr/is-subtitled/releases/download/v2.4.0/IsSubtitled.exe"
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(sampleJson));
        var service = new UpdateService(httpClient);
        var update = await service.CheckForUpdateAsync("2.3.1");

        Assert.NotNull(update);
        Assert.Equal("2.4.0", update.Version);
        Assert.Equal("v2.4.0", update.Tag);
        Assert.Equal(50000, update.AssetSize);
    }

    [Fact]
    public void MainWindowAxaml_BindsVersionPillUpdateBannerAndAddExcludedButton()
    {
        var axamlPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Views", "MainWindow.axaml"));
        Assert.True(File.Exists(axamlPath), $"MainWindow.axaml not found at {axamlPath}");

        var axaml = File.ReadAllText(axamlPath);

        // 1. Header version pill & check for updates command
        Assert.Contains("CurrentVersionDisplay", axaml);
        Assert.Contains("CheckForUpdatesCommand", axaml);

        // 2. Inline update notification banner, progress bar, and actions
        Assert.Contains("ShowUpdateBanner", axaml);
        Assert.Contains("UpdateStatusText", axaml);
        Assert.Contains("UpdateProgress", axaml);
        Assert.Contains("DownloadUpdateCommand", axaml);
        Assert.Contains("ApplyUpdateCommand", axaml);
        Assert.Contains("DismissUpdateCommand", axaml);
        Assert.Contains("ViewReleaseNotesCommand", axaml);

        // 3. Explicit "Add" button for folder exclusions
        Assert.Contains("Content=\"Add\"", axaml);

        // 4. Invariant 3: 100% 7-bit ASCII in MainWindow.axaml (except optional UTF-8 BOM at index 0)
        for (int i = 0; i < axaml.Length; i++)
        {
            char c = axaml[i];
            if (i == 0 && c == '\uFEFF') continue;
            Assert.True(c <= 127, $"Non-ASCII character U+{(int)c:X4} found in MainWindow.axaml at index {i}");
        }
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
