using System.Collections.Generic;
using System.IO;

namespace IsSubtitled.ViewModels;

public sealed class VideoItem
{
    public required string FullPath { get; init; }
    public string Name => Path.GetFileName(FullPath);
}

public sealed class DirectoryGroup
{
    public required string Directory { get; init; }
    public required IReadOnlyList<VideoItem> Videos { get; init; }
}
