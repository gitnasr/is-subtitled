using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace IsSubtitled.Models;

/// <summary>Native file-manager integration (the reason this is a desktop app).</summary>
public static class Platform
{
    /// <summary>Open the given folder in the OS file manager, reusing an existing window if one already shows it.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (TryReuseExplorerWindow(path, null)) return;
                LaunchExplorer(path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Start("open", path);
            }
            else
            {
                Start("xdg-open", path);
            }
        }
        catch { /* shell call failed */ }
    }

    /// <summary>Open the OS file manager with the given file selected, reusing an existing window if the folder is already open.</summary>
    public static void RevealInFileManager(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var dir = Path.GetDirectoryName(path);
                if (dir is not null && TryReuseExplorerWindow(dir, path)) return;
                LaunchExplorerSelect(path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Start("open", "-R", path);
            }
            else
            {
                Start("xdg-open", Path.GetDirectoryName(path) ?? path);
            }
        }
        catch { /* shell call failed */ }
    }

    // --- helpers ---

    private static void Start(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Process.Start(psi);
    }

    // explorer.exe needs its arguments in a very specific raw form; ArgumentList's
    // escaping (a quote around the whole token) makes it ignore the path. Pass a
    // raw command line instead.
    [SupportedOSPlatform("windows")]
    private static void StartRaw(string file, string arguments)
        => Process.Start(new ProcessStartInfo(file) { Arguments = arguments, UseShellExecute = false });

    [SupportedOSPlatform("windows")]
    private static void LaunchExplorer(string path) => StartRaw("explorer.exe", $"\"{path}\"");

    [SupportedOSPlatform("windows")]
    private static void LaunchExplorerSelect(string path)
        => StartRaw("explorer.exe", $"/select,\"{path}\"");

    /// <summary>
    /// Walks already-open Explorer windows. If one is showing <paramref name="folderPath"/>,
    /// brings it to the front (and selects <paramref name="selectFile"/> if given) instead of
    /// opening a new window. Returns true if it handled the request.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool TryReuseExplorerWindow(string folderPath, string? selectFile)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return false;

        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return false;

            dynamic windows = shell.Windows();
            int count = windows.Count;
            var target = folderPath.TrimEnd('\\');

            for (int i = 0; i < count; i++)
            {
                dynamic? w = null;
                try
                {
                    w = windows.Item(i);
                    if (w is null) continue;

                    // Only File Explorer windows (skip Internet Explorer, Edge legacy, etc.).
                    string fullName = w.FullName;
                    if (string.IsNullOrEmpty(fullName) ||
                        !fullName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string current = w.Document.Folder.Self.Path;
                    if (!string.Equals(current.TrimEnd('\\'), target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Match found — focus it.
                    try { SetForegroundWindow((IntPtr)Convert.ToInt64(w.HWND)); } catch { }

                    if (selectFile is not null)
                    {
                        try
                        {
                            // SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED
                            dynamic item = w.Document.Folder.ParseName(Path.GetFileName(selectFile));
                            if (item is not null) w.Document.SelectItem(item, 1 | 4 | 8 | 16);
                        }
                        catch { /* still counts as handled — window is focused */ }
                    }
                    return true;
                }
                catch { /* this window misbehaved; try the next */ }
                finally
                {
                    if (w is not null && Marshal.IsComObject(w)) Marshal.ReleaseComObject(w);
                }
            }
        }
        catch { return false; }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
        }
        return false;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
