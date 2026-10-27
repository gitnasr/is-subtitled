using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IsSubtitled.Models;
using IsSubtitled.ViewModels;
using Xunit;

namespace IsSubtitled.Tests;

public class ExclusionTests : IDisposable
{
    private readonly string _tempRoot;

    public ExclusionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "IsSubtitledTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch { }
    }

    private string CreateDir(string relativePath)
    {
        var path = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    private void CreateFile(string relativePath)
    {
        var path = Path.Combine(_tempRoot, relativePath);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, "dummy content");
    }

    [Fact]
    public void BareNameExclusion_ExcludesMatchingFolder()
    {
        // Setup:
        // root/Keep/video1.mp4
        // root/SkipMe/video2.mp4
        CreateFile("Keep/video1.mp4");
        CreateFile("SkipMe/video2.mp4");

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SkipMe" };
        var results = SubtitleScanner.Scan(_tempRoot, excluded, null, CancellationToken.None);

        Assert.Single(results);
        Assert.EndsWith("Keep", results[0].Directory);
    }

    [Fact]
    public void ForwardSlashPathExclusion_ExcludesFolder()
    {
        CreateFile("Keep/video1.mp4");
        var skipDir = CreateDir("SkipMe");
        CreateFile("SkipMe/video2.mp4");

        // User copies path with forward slashes:
        var forwardSlashPath = skipDir.Replace('\\', '/');
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { forwardSlashPath };
        var results = SubtitleScanner.Scan(_tempRoot, excluded, null, CancellationToken.None);

        Assert.Single(results);
        Assert.EndsWith("Keep", results[0].Directory);
    }

    [Fact]
    public void TrailingSlashPathExclusion_ExcludesFolder()
    {
        CreateFile("Keep/video1.mp4");
        var skipDir = CreateDir("SkipMe");
        CreateFile("SkipMe/video2.mp4");

        var trailingSlashPath = skipDir.TrimEnd('\\') + "\\";
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { trailingSlashPath };
        var results = SubtitleScanner.Scan(_tempRoot, excluded, null, CancellationToken.None);

        Assert.Single(results);
        Assert.EndsWith("Keep", results[0].Directory);
    }

    [Fact]
    public void RelativePathExclusion_ExcludesFolder()
    {
        CreateFile("Keep/video1.mp4");
        CreateFile("Category/SkipMe/video2.mp4");

        // User specifies relative path inside library:
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Category/SkipMe" };
        var results = SubtitleScanner.Scan(_tempRoot, excluded, null, CancellationToken.None);

        Assert.Single(results);
        Assert.EndsWith("Keep", results[0].Directory);
    }

    [Fact]
    public void RootFolderExclusion_ExcludesEverything()
    {
        CreateFile("video1.mp4");
        CreateFile("Sub/video2.mp4");

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _tempRoot };
        var results = SubtitleScanner.Scan(_tempRoot, excluded, null, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public void ViewModel_UncommittedNewExcluded_IsIncludedInScan()
    {
        // When user types an exclusion in the TextBox and directly clicks "Scan" without Enter:
        CreateFile("Keep/video1.mp4");
        CreateFile("SkipMe/video2.mp4");

        var vm = new MainWindowViewModel();
        vm.SelectedFolder = _tempRoot;
        vm.NewExcluded = "SkipMe"; // Typed into textbox but user didn't press Enter

        // ScanAsync should automatically commit pending NewExcluded before scanning!
        // We test whether ScanAsync excludes "SkipMe"
        vm.ScanCommand.Execute(null);

        // Wait for scan
        int timeout = 50;
        while (vm.IsScanning && timeout-- > 0)
        {
            Thread.Sleep(50);
        }

        Assert.Single(vm.Results);
        Assert.EndsWith("Keep", vm.Results[0].Directory);
    }
}
