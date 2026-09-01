using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace IsSubtitled.Models;

/// <summary>Live counters reported while a scan is running.</summary>
public sealed record ScanProgress(
    string CurrentDirectory,
    int FilesExamined,
    int FoldersScanned,
    int MissingFound);

/// <summary>A video file with no matching subtitle, and its size on disk.</summary>
public sealed record VideoFile(string FullPath, long Length);

/// <summary>
/// Walks a directory tree and finds video files that have no matching
/// subtitle file (same base name) in the same folder.
/// </summary>
public static class SubtitleScanner
{
    public static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".flv", ".avi", ".mov", ".wmv", ".ts" };

    public static readonly string[] SubtitleExtensions =
        { ".srt", ".sub", ".ssa", ".ass" };

    private static readonly HashSet<string> VideoExt =
        new(VideoExtensions, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SubExt =
        new(SubtitleExtensions, StringComparer.OrdinalIgnoreCase);

    public sealed record DirectoryResult(string Directory, IReadOnlyList<VideoFile> Videos);

    /// <param name="excluded">Folders to skip — each entry may be a bare folder name (e.g. "COMP")
    /// or a full path (e.g. "H:\PX\COMP"). A full path also skips everything beneath it. Case-insensitive.</param>
    /// <param name="progress">Reports the current directory and running counts, once per directory.</param>
    public static List<DirectoryResult> Scan(
        string root,
        IReadOnlySet<string> excluded,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        var results = new List<DirectoryResult>();
        var counters = new Counters();
        ScanDir(root, excluded, results, counters, progress, ct);
        return results;
    }

    /// <summary>Mutable running totals, threaded through the recursion.</summary>
    private sealed class Counters
    {
        public int Files;
        public int Folders;
        public int Missing;
    }

    private static bool IsExcluded(string fullPath, IReadOnlySet<string> excluded)
    {
        var name = Path.GetFileName(fullPath);
        var full = fullPath.TrimEnd('\\', '/');
        foreach (var e in excluded)
        {
            if (string.IsNullOrWhiteSpace(e)) continue;
            var entry = e.TrimEnd('\\', '/');

            // Bare name match (e.g. "COMP").
            if (string.Equals(entry, name, StringComparison.OrdinalIgnoreCase)) return true;

            // Exact full-path match (e.g. "H:\PX\COMP").
            if (string.Equals(entry, full, StringComparison.OrdinalIgnoreCase)) return true;

            // Inside an excluded path (e.g. "H:\PX\COMP\S" under "H:\PX\COMP").
            if (full.StartsWith(entry + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(entry + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void ScanDir(
        string dir,
        IReadOnlySet<string> excluded,
        List<DirectoryResult> results,
        Counters counters,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        FileInfo[] files;
        DirectoryInfo[] subDirs;
        try
        {
            // DirectoryInfo rather than Directory.GetFiles: on Windows the size comes back
            // with the directory entry, so file sizes cost no extra syscalls.
            var info = new DirectoryInfo(dir);
            files = info.GetFiles();
            subDirs = info.GetDirectories();
        }
        catch (UnauthorizedAccessException) { return; } // skip folders we can't read
        catch (DirectoryNotFoundException) { return; }
        catch (IOException) { return; }

        counters.Folders++;
        counters.Files += files.Length;

        var subtitleBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            if (SubExt.Contains(f.Extension))
                subtitleBases.Add(Path.GetFileNameWithoutExtension(f.Name));
        }

        var missing = files
            .Where(f => VideoExt.Contains(f.Extension))
            .Where(f => !subtitleBases.Contains(Path.GetFileNameWithoutExtension(f.Name)))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new VideoFile(f.FullName, SafeLength(f)))
            .ToList();

        if (missing.Count > 0)
        {
            counters.Missing += missing.Count;
            results.Add(new DirectoryResult(dir, missing));
        }

        // One report per directory — per-file would flood the UI thread.
        progress?.Report(new ScanProgress(dir, counters.Files, counters.Folders, counters.Missing));

        foreach (var sub in subDirs)
        {
            if (IsExcluded(sub.FullName, excluded)) continue;
            ScanDir(sub.FullName, excluded, results, counters, progress, ct);
        }
    }

    /// <summary>Length can throw if the entry vanished between enumeration and access.</summary>
    private static long SafeLength(FileInfo f)
    {
        try { return f.Length; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }
}
