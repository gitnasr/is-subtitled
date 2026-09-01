using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IsSubtitled.ViewModels;

public sealed class VideoItem
{
    public required string FullPath { get; init; }
    public required long Size { get; init; }

    public string Name => Path.GetFileName(FullPath);
    public string SizeText => FormatSize(Size);

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value >= 100 || unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.0} {units[unit]}";
    }
}

public sealed partial class DirectoryGroup : ObservableObject
{
    public required string Directory { get; init; }
    public required IReadOnlyList<VideoItem> Videos { get; init; }

    /// <summary>Collapsed groups keep their header and badge but hide their rows.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    public int Count => Videos.Count;
    public long TotalSize => Videos.Sum(v => v.Size);
    public string TotalSizeText => VideoItem.FormatSize(TotalSize);

    /// <summary>Leaf folder name, shown before the full path in the group header.</summary>
    public string DisplayName
    {
        get
        {
            var name = Path.GetFileName(Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? Directory : name;
        }
    }
}
