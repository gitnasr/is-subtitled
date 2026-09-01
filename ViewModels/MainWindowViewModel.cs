using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsSubtitled.Models;

namespace IsSubtitled.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppConfig _config = AppConfig.Load();
    private IUiServices? _ui;
    private CancellationTokenSource? _cts;

    public MainWindowViewModel()
    {
        SelectedFolder = _config.LastPath;
        foreach (var d in _config.ExcludedDirs)
            ExcludedDirs.Add(d);
    }

    /// <summary>Set by the View once it's constructed.</summary>
    public void AttachUi(IUiServices ui) => _ui = ui;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private string? _selectedFolder;

    [ObservableProperty]
    private string _newExcluded = string.Empty;

    [ObservableProperty]
    private string _status = "Pick a folder to scan.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool _isScanning;

    public bool IsIdle => !IsScanning;

    public ObservableCollection<string> ExcludedDirs { get; } = new();
    public ObservableCollection<DirectoryGroup> Results { get; } = new();

    private bool CanScan() => !string.IsNullOrWhiteSpace(SelectedFolder) && !IsScanning;

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        if (_ui is null) return;
        var picked = await _ui.PickFolderAsync(SelectedFolder);
        if (picked is null) return;
        SelectedFolder = picked;
        _config.LastPath = picked;
        _config.Save();
    }

    [RelayCommand]
    private void AddExcluded()
    {
        foreach (var part in NewExcluded.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ExcludedDirs.Contains(part, StringComparer.OrdinalIgnoreCase))
                ExcludedDirs.Add(part);
        }
        NewExcluded = string.Empty;
        PersistExcluded();
    }

    [RelayCommand]
    private void RemoveExcluded(string? dir)
    {
        if (dir is null) return;
        ExcludedDirs.Remove(dir);
        PersistExcluded();
    }

    private void PersistExcluded()
    {
        _config.ExcludedDirs = ExcludedDirs.ToList();
        _config.Save();
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var root = SelectedFolder;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            Status = "Folder does not exist.";
            return;
        }

        IsScanning = true;
        Results.Clear();
        _cts = new CancellationTokenSource();
        var excluded = new HashSet<string>(ExcludedDirs, StringComparer.OrdinalIgnoreCase);
        var progress = new Progress<string>(p => Status = $"Scanning {p}…");

        try
        {
            var results = await Task.Run(
                () => SubtitleScanner.Scan(root, excluded, progress, _cts.Token),
                _cts.Token);

            foreach (var r in results)
            {
                Results.Add(new DirectoryGroup
                {
                    Directory = r.Directory,
                    Videos = r.Videos.Select(v => new VideoItem { FullPath = v }).ToList(),
                });
            }

            var total = results.Sum(r => r.Videos.Count);
            Status = total == 0
                ? "No videos without subtitles found. 🎉"
                : $"Done — {total} video(s) without subtitles in {results.Count} folder(s).";
        }
        catch (OperationCanceledException)
        {
            Status = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_ui is null || Results.Count == 0) return;
        var path = await _ui.PickSaveFileAsync("NoSub.txt");
        if (path is null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Videos without subtitles in: {SelectedFolder}");
        sb.AppendLine();
        var total = 0;
        foreach (var g in Results)
        {
            sb.AppendLine($"----------------- {g.Directory} ------------------");
            foreach (var v in g.Videos)
            {
                sb.AppendLine(v.FullPath);
                total++;
            }
            sb.AppendLine();
        }
        sb.AppendLine($"Total: {total}");

        try
        {
            await File.WriteAllTextAsync(path, sb.ToString());
            Status = $"Saved to {path}";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenInExplorer(VideoItem? item)
    {
        if (item is not null) Platform.RevealInFileManager(item.FullPath);
    }

    [RelayCommand]
    private void OpenFolder(string? dir)
    {
        if (!string.IsNullOrEmpty(dir)) Platform.OpenFolder(dir);
    }

    [RelayCommand]
    private async Task CopyPathAsync(string? path)
    {
        if (_ui is null || string.IsNullOrEmpty(path)) return;
        await _ui.SetClipboardAsync(path);
        Status = $"Copied: {path}";
    }
}
