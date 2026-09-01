using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace IsSubtitled.Models;

/// <summary>
/// Walks a directory tree and finds video files that have no matching
/// subtitle file (same base name) in the same folder.
/// </summary>
public static class SubtitleScanner
{
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".flv", ".avi", ".mov", ".wmv", ".ts" };

    private static readonly HashSet<string> SubExt = new(StringComparer.OrdinalIgnoreCase)
        { ".srt", ".sub", ".ssa", ".ass" };

    public sealed record DirectoryResult(string Directory, IReadOnlyList<string> Videos);

    /// <param name="excluded">Folders to skip — each entry may be a bare folder name (e.g. "COMP")
    /// or a full path (e.g. "H:\PX\COMP"). A full path also skips everything beneath it. Case-insensitive.</param>
    /// <param name="progress">Reports the directory currently being scanned.</param>
    public static List<DirectoryResult> Scan(
        string root,
        IReadOnlySet<string> excluded,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var results = new List<DirectoryResult>();
        ScanDir(root, excluded, results, progress, ct);
        return results;
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
        IProgress<string>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(dir);

        string[] files;
        string[] subDirs;
        try
        {
            files = Directory.GetFiles(dir);
            subDirs = Directory.GetDirectories(dir);
        }
        catch (UnauthorizedAccessException) { return; } // skip folders we can't read
        catch (DirectoryNotFoundException) { return; }
        catch (IOException) { return; }

        var subtitleBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            if (SubExt.Contains(Path.GetExtension(f)))
                subtitleBases.Add(Path.GetFileNameWithoutExtension(f));
        }

        var missing = files
            .Where(f => VideoExt.Contains(Path.GetExtension(f)))
            .Where(f => !subtitleBases.Contains(Path.GetFileNameWithoutExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
            results.Add(new DirectoryResult(dir, missing));

        foreach (var sub in subDirs)
        {
            if (IsExcluded(sub, excluded)) continue;
            ScanDir(sub, excluded, results, progress, ct);
        }
    }
}
