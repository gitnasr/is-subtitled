using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsSubtitled.Models;

namespace IsSubtitled.ViewModels;

/// <summary>Which panel the results pane shows.</summary>
public enum ScanState
{
    Idle,
    Scanning,
    Results,
    AllGood,
    Error,
}

public enum SortMode
{
    FolderName,
    FileCount,
    Size,
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly Stopwatch _stopwatch = new();
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

    // ---- Configuration -------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyPropertyChangedFor(nameof(FolderDisplay))]
    private string? _selectedFolder;

    public string FolderDisplay => string.IsNullOrWhiteSpace(SelectedFolder)
        ? "No folder selected"
        : SelectedFolder;

    [ObservableProperty]
    private string _newExcluded = string.Empty;

    public ObservableCollection<string> ExcludedDirs { get; } = new();

    public IReadOnlyList<string> VideoFormats => SubtitleScanner.VideoExtensions;
    public IReadOnlyList<string> SubtitleFormats => SubtitleScanner.SubtitleExtensions;

    // ---- State ---------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsScanning), nameof(HasResults),
        nameof(IsAllGood), nameof(IsError), nameof(ShowResultsToolbar), nameof(ShowSummary))]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(SaveCommand))]
    private ScanState _state = ScanState.Idle;

    public bool IsIdle => State == ScanState.Idle;
    public bool IsScanning => State == ScanState.Scanning;
    public bool HasResults => State == ScanState.Results;
    public bool IsAllGood => State == ScanState.AllGood;
    public bool IsError => State == ScanState.Error;

    /// <summary>Filter and sort only make sense when there are rows to act on.</summary>
    public bool ShowResultsToolbar => State == ScanState.Results;

    /// <summary>The header pill describes a scan, so a failed one has nothing to say.</summary>
    public bool ShowSummary => State is ScanState.Scanning or ScanState.Results or ScanState.AllGood;

    [ObservableProperty]
    private string _status = "Pick a folder to scan.";

    [ObservableProperty]
    private string _errorPath = string.Empty;

    // ---- Live scan counters --------------------------------------------

    [ObservableProperty]
    private string _currentDirectory = string.Empty;

    [ObservableProperty]
    private int _filesExamined;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _foldersScanned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private int _missingFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string _elapsedText = string.Empty;

    /// <summary>The header pill: "42 missing | 8 folders | 4.2s elapsed".</summary>
    public string SummaryText => $"{MissingFound} missing  |  {FoldersScanned} folders  |  {ElapsedText} elapsed";

    /// <summary>Folders walked most recently, newest last — the live list during a scan.</summary>
    public ObservableCollection<string> RecentDirectories { get; } = new();

    // ---- Results -------------------------------------------------------

    /// <summary>Every group the scan produced. Source of truth; never filtered in place.</summary>
    private readonly List<DirectoryGroup> _allResults = new();

    /// <summary>What the view binds to — <see cref="_allResults"/> after filter and sort.</summary>
    public ObservableCollection<DirectoryGroup> Results { get; } = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    private SortMode _sortBy = SortMode.FolderName;

    /// <summary>Bound to the sort ComboBox; index order matches <see cref="SortMode"/>.</summary>
    [ObservableProperty]
    private int _sortIndex;

    partial void OnFilterTextChanged(string value) => ApplyFilterAndSort();

    partial void OnSortIndexChanged(int value)
    {
        _sortBy = (SortMode)value;
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        var filter = FilterText?.Trim() ?? string.Empty;

        IEnumerable<DirectoryGroup> groups = _allResults;

        if (filter.Length > 0)
        {
            // Keep a group when its path matches, or when any of its files do —
            // in the latter case only the matching files are shown.
            groups = _allResults
                .Select(g => g.Directory.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    ? g
                    : new DirectoryGroup
                    {
                        Directory = g.Directory,
                        Videos = g.Videos
                            .Where(v => v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            .ToList(),
                        IsExpanded = g.IsExpanded,
                    })
                .Where(g => g.Videos.Count > 0);
        }

        groups = _sortBy switch
        {
            SortMode.FileCount => groups.OrderByDescending(g => g.Count).ThenBy(g => g.Directory, StringComparer.OrdinalIgnoreCase),
            SortMode.Size => groups.OrderByDescending(g => g.TotalSize).ThenBy(g => g.Directory, StringComparer.OrdinalIgnoreCase),
            _ => groups.OrderBy(g => g.Directory, StringComparer.OrdinalIgnoreCase),
        };

        Results.Clear();
        foreach (var g in groups)
            Results.Add(g);
    }

    // ---- Commands ------------------------------------------------------

    private bool CanScan() => !string.IsNullOrWhiteSpace(SelectedFolder) && State != ScanState.Scanning;
    private bool CanSave() => _allResults.Count > 0;

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

    [RelayCommand]
    private void ToggleGroup(DirectoryGroup? group)
    {
        if (group is not null) group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var root = SelectedFolder;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            ErrorPath = root ?? string.Empty;
            Status = "Folder does not exist.";
            State = ScanState.Error;
            return;
        }

        // The scanner deliberately skips unreadable folders so one bad subfolder can't
        // abort a scan — which would make a denied *root* look like a clean result.
        // Probe it up front so that case surfaces as an error instead.
        try
        {
            _ = Directory.EnumerateFileSystemEntries(root).Take(1).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            ErrorPath = root;
            Status = "Access denied.";
            State = ScanState.Error;
            return;
        }
        catch (IOException)
        {
            ErrorPath = root;
            Status = "Folder is unreadable.";
            State = ScanState.Error;
            return;
        }

        State = ScanState.Scanning;
        _allResults.Clear();
        Results.Clear();
        RecentDirectories.Clear();
        FilesExamined = 0;
        FoldersScanned = 0;
        MissingFound = 0;
        CurrentDirectory = root;
        ElapsedText = "0.0s";
        _stopwatch.Restart();

        _cts = new CancellationTokenSource();
        var excluded = new HashSet<string>(ExcludedDirs, StringComparer.OrdinalIgnoreCase);
        var progress = new Progress<ScanProgress>(p =>
        {
            // Reports are posted from the scan thread and can still be draining after the
            // scan finishes; letting a late one land would overwrite the final totals.
            if (State != ScanState.Scanning) return;

            CurrentDirectory = p.CurrentDirectory;
            FilesExamined = p.FilesExamined;
            FoldersScanned = p.FoldersScanned;
            MissingFound = p.MissingFound;
            ElapsedText = $"{_stopwatch.Elapsed.TotalSeconds:0.0}s";

            RecentDirectories.Add(p.CurrentDirectory);
            if (RecentDirectories.Count > 12) RecentDirectories.RemoveAt(0);
        });

        try
        {
            var results = await Task.Run(
                () => SubtitleScanner.Scan(root, excluded, progress, _cts.Token),
                _cts.Token);

            foreach (var r in results)
            {
                _allResults.Add(new DirectoryGroup
                {
                    Directory = r.Directory,
                    Videos = r.Videos.Select(v => new VideoItem { FullPath = v.FullPath, Size = v.Length }).ToList(),
                });
            }

            var total = results.Sum(r => r.Videos.Count);
            ApplyFilterAndSort();

            State = total == 0 ? ScanState.AllGood : ScanState.Results;

            // Once finished the header pill describes the result, not the walk: how many
            // videos are missing subtitles and how many folders they sit in.
            MissingFound = total;
            FoldersScanned = results.Count;
            Status = total == 0
                ? "Every video has a subtitle."
                : $"{total} video(s) without subtitles in {results.Count} folder(s).";
        }
        catch (OperationCanceledException)
        {
            Status = "Scan cancelled.";
            // Keep whatever was already collected rather than throwing it away.
            ApplyFilterAndSort();
            State = _allResults.Count > 0 ? ScanState.Results : ScanState.Idle;
            MissingFound = _allResults.Sum(g => g.Count);
            FoldersScanned = _allResults.Count;
        }
        catch (Exception ex)
        {
            ErrorPath = root;
            Status = ex.Message;
            State = ScanState.Error;
        }
        finally
        {
            _stopwatch.Stop();
            ElapsedText = $"{_stopwatch.Elapsed.TotalSeconds:0.0}s";
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private async Task RetryAsync() => await ScanAsync();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_ui is null || _allResults.Count == 0) return;
        var path = await _ui.PickSaveFileAsync("NoSub.txt");
        if (path is null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Videos without subtitles in: {SelectedFolder}");
        sb.AppendLine();
        var total = 0;
        foreach (var g in _allResults)
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
