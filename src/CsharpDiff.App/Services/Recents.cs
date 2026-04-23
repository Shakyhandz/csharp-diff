using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CsharpDiff.App.Services;

public sealed record FolderPair(string Left, string Right)
{
    public string Display => $"{Shorten(Left)}  ↔  {Shorten(Right)}";
    private static string Shorten(string p) => p.Length <= 40 ? p : "…" + p[^39..];
}

public static class Recents
{
    private const int MaxEntries = 5;
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "csharp-diff");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "recents.json");

    public static List<FolderPair> Load()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return new List<FolderPair>();
            var json = File.ReadAllText(ConfigFile);
            return JsonSerializer.Deserialize<List<FolderPair>>(json) ?? new List<FolderPair>();
        }
        catch
        {
            return new List<FolderPair>();
        }
    }

    public static void Save(FolderPair entry)
    {
        try
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p.Left, entry.Left, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(p.Right, entry.Right, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, entry);
            if (list.Count > MaxEntries) list = list.Take(MaxEntries).ToList();
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(list));
        }
        catch
        {
            // recents are best-effort
        }
    }
}
