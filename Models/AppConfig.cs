using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace IsSubtitled.Models;

/// <summary>Persisted settings, stored under %AppData%\IsSubtitled\config.json.</summary>
public sealed class AppConfig
{
    public string? LastPath { get; set; }
    public List<string> ExcludedDirs { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string ConfigPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IsSubtitled");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
        }
        catch { /* corrupt/missing -> defaults */ }
        return new AppConfig();
    }

    public void Save()
    {
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts)); }
        catch { /* best effort */ }
    }
}
