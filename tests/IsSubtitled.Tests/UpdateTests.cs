using System;
using System.Text.Json;
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
        Assert.Equal("IsSubtitled.exe", update.AssetName);
        Assert.Equal(103615037, update.AssetSize);
        Assert.Equal("https://github.com/gitnasr/is-subtitled/releases/download/v2.2.0/IsSubtitled.exe", update.DownloadUrl);
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
    public async Task LiveCheck_DetectsNewerReleaseAgainstRealGitHubRepo()
    {
        var service = new UpdateService();
        // Compare against a simulated older version "1.0.0"
        var update = await service.CheckForUpdateAsync("1.0.0");

        Assert.NotNull(update);
        Assert.NotEmpty(update.Version);
        Assert.NotEmpty(update.DownloadUrl);
        Assert.True(update.AssetSize > 0);
    }

}
