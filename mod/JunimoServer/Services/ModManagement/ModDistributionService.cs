using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using StardewModdingAPI;

namespace JunimoServer.Services.ModManagement;

public class SyncModInfo
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string FolderName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string DownloadUrl { get; set; } = "";
}

public class SyncManifestResponse
{
    public string ServerVersion { get; set; } = "1.0.0";
    public int ModCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public List<SyncModInfo> Mods { get; set; } = new();
    public string BundleDownloadUrl { get; set; } = "/api/mods/sync/bundle";
    public string ClientModDownloadUrl { get; set; } = "/api/mods/sync/client-mod";
}

public class ModDistributionService : ModService
{
    private static readonly HashSet<string> ExcludedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "JunimoServer",
        "JunimoServer.Shared",
        ".git",
        ".game_libs",
        "bin",
        "obj"
    };

    private static readonly HashSet<string> ExcludedModIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "JunimoHost.Server",
        "JunimoServer.Shared"
    };

    private readonly ConcurrentDictionary<string, (DateTime CachedAt, byte[] Data)> _zipCache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public ModDistributionService(IModHelper helper, IMonitor monitor)
        : base(helper, monitor)
    {
    }

    private string GetModsDirectory()
    {
        try
        {
            var parent = Directory.GetParent(Helper.DirectoryPath);
            if (parent != null && parent.Exists)
            {
                return parent.FullName;
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Failed to resolve parent Mods directory: {ex.Message}", LogLevel.Warn);
        }

        return Helper.DirectoryPath;
    }

    public List<SyncModInfo> GetSyncableMods()
    {
        var result = new List<SyncModInfo>();
        var modsDir = GetModsDirectory();

        if (!Directory.Exists(modsDir))
        {
            return result;
        }

        foreach (var dir in Directory.GetDirectories(modsDir))
        {
            var folderName = Path.GetFileName(dir);
            if (ExcludedFolderNames.Contains(folderName))
            {
                continue;
            }

            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(manifestPath);
                var raw = JsonConvert.DeserializeObject<dynamic>(json);
                if (raw == null)
                {
                    continue;
                }

                string uniqueId = (string?)raw.UniqueID ?? folderName;
                if (ExcludedModIds.Contains(uniqueId))
                {
                    continue;
                }

                string name = (string?)raw.Name ?? folderName;
                string version = (string?)raw.Version ?? "1.0.0";
                string author = (string?)raw.Author ?? "";
                string description = (string?)raw.Description ?? "";

                long sizeBytes = 0;
                try
                {
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    sizeBytes = files.Sum(f => new FileInfo(f).Length);
                }
                catch
                {
                    // Ignore file access exceptions for size calculation
                }

                result.Add(new SyncModInfo
                {
                    UniqueId = uniqueId,
                    Name = name,
                    Version = version,
                    Author = author,
                    Description = description,
                    FolderName = folderName,
                    SizeBytes = sizeBytes,
                    DownloadUrl = $"/api/mods/sync/download/{Uri.EscapeDataString(folderName)}"
                });
            }
            catch (Exception ex)
            {
                Monitor.Log($"[ModDistribution] Error parsing manifest in {folderName}: {ex.Message}", LogLevel.Trace);
            }
        }

        return result;
    }

    public SyncManifestResponse GetSyncManifest()
    {
        var mods = GetSyncableMods();
        return new SyncManifestResponse
        {
            ServerVersion = Helper.ModRegistry.Get("JunimoHost.Server")?.Manifest?.Version?.ToString() ?? "1.0.0",
            ModCount = mods.Count,
            TotalSizeBytes = mods.Sum(m => m.SizeBytes),
            Mods = mods
        };
    }

    public byte[]? GetModZip(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName) || ExcludedFolderNames.Contains(folderName))
        {
            return null;
        }

        // Check cache
        if (_zipCache.TryGetValue(folderName, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            return cached.Data;
        }

        var modsDir = GetModsDirectory();
        var targetDir = Path.Combine(modsDir, folderName);

        if (!Directory.Exists(targetDir))
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                AddDirectoryToArchive(archive, targetDir, folderName);
            }

            var bytes = ms.ToArray();
            _zipCache[folderName] = (DateTime.UtcNow, bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Failed to create zip for {folderName}: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    public byte[]? GetBundleZip()
    {
        const string bundleKey = "__bundle__";
        if (_zipCache.TryGetValue(bundleKey, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            return cached.Data;
        }

        var modsDir = GetModsDirectory();
        var syncMods = GetSyncableMods();

        try
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var mod in syncMods)
                {
                    var sourceDir = Path.Combine(modsDir, mod.FolderName);
                    if (Directory.Exists(sourceDir))
                    {
                        AddDirectoryToArchive(archive, sourceDir, mod.FolderName);
                    }
                }
            }

            var bytes = ms.ToArray();
            _zipCache[bundleKey] = (DateTime.UtcNow, bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Failed to create bundle zip: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string sourceDirectory, string entryPrefix)
    {
        var allFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var entryName = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }
}
