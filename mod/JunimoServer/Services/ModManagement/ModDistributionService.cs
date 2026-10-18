using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
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

    public static string GetPersistentModsDirectory()
    {
        if (Directory.Exists("/data/game"))
        {
            var path = Path.Combine("/data/game", "custom-mods");
            Directory.CreateDirectory(path);
            return path;
        }
        if (Directory.Exists("/data"))
        {
            var path = Path.Combine("/data", "custom-mods");
            Directory.CreateDirectory(path);
            return path;
        }
        var localDir = Path.Combine(Directory.GetCurrentDirectory(), "custom-mods");
        Directory.CreateDirectory(localDir);
        return localDir;
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

    private SyncModInfo? GetModInfoFromFolder(string dir, string folderName, string? relativeDownloadPath = null)
    {
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var raw = JsonConvert.DeserializeObject<dynamic>(json);
            if (raw == null)
            {
                return null;
            }

            string uniqueId = (string?)raw.UniqueID ?? folderName;
            if (ExcludedModIds.Contains(uniqueId))
            {
                return null;
            }

            string name = (string?)raw.Name ?? folderName;
            string version = (string?)raw.Version ?? "1.0.0";
            string author = (string?)raw.Author ?? "";
            string description = (string?)raw.Description ?? "";
            string downloadKey = relativeDownloadPath ?? folderName;

            long sizeBytes = 0;
            try
            {
                var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                sizeBytes = files.Sum(f => new FileInfo(f).Length);
            }
            catch
            {
            }

            return new SyncModInfo
            {
                UniqueId = uniqueId,
                Name = name,
                Version = version,
                Author = author,
                Description = description,
                FolderName = downloadKey,
                SizeBytes = sizeBytes,
                DownloadUrl = $"/api/mods/sync/download/{Uri.EscapeDataString(downloadKey)}"
            };
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Error parsing manifest in {folderName}: {ex.Message}", LogLevel.Trace);
            return null;
        }
    }

    public List<SyncModInfo> GetSyncableMods()
    {
        var result = new List<SyncModInfo>();
        var seenUniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dirsToScan = new List<string>();
        var modsDir = GetModsDirectory();
        if (Directory.Exists(modsDir)) dirsToScan.Add(modsDir);

        var persistentDir = GetPersistentModsDirectory();
        if (Directory.Exists(persistentDir) && !persistentDir.Equals(modsDir, StringComparison.OrdinalIgnoreCase))
        {
            dirsToScan.Add(persistentDir);
        }

        foreach (var parentDir in dirsToScan)
        {
            // 1. Recursive search: Find all manifest.json (handles nested multi-mod packs like Stardew Valley Expanded)
            try
            {
                var manifestFiles = Directory.GetFiles(parentDir, "manifest.json", SearchOption.AllDirectories);
                foreach (var manifestPath in manifestFiles)
                {
                    var modDir = Path.GetDirectoryName(manifestPath);
                    if (string.IsNullOrEmpty(modDir)) continue;

                    var relativePath = Path.GetRelativePath(parentDir, modDir).Replace('\\', '/');
                    if (relativePath.StartsWith(".") || relativePath.Contains("/.") ||
                        relativePath.Split('/').Any(p => ExcludedFolderNames.Contains(p)))
                    {
                        continue;
                    }

                    var folderName = Path.GetFileName(modDir);
                    var modInfo = GetModInfoFromFolder(modDir, folderName, relativePath);
                    if (modInfo != null && seenUniqueIds.Add(modInfo.UniqueId))
                    {
                        seenFolders.Add(relativePath);
                        seenFolders.Add(folderName);
                        result.Add(modInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"[ModDistribution] Error scanning recursive manifests in {parentDir}: {ex.Message}", LogLevel.Warn);
            }

            // 2. Fallback: Check top-level directories that might not have a direct manifest.json
            foreach (var dir in Directory.GetDirectories(parentDir))
            {
                var folderName = Path.GetFileName(dir);
                if (ExcludedFolderNames.Contains(folderName) || seenFolders.Contains(folderName))
                {
                    continue;
                }

                // If sub-mods inside this folder were already scanned, skip
                if (result.Any(r => r.FolderName.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                long size = 0;
                try
                {
                    size = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                }
                catch { }

                if (size > 0 && seenFolders.Add(folderName))
                {
                    result.Add(new SyncModInfo
                    {
                        UniqueId = folderName,
                        Name = folderName,
                        Version = "1.0.0",
                        FolderName = folderName,
                        SizeBytes = size,
                        DownloadUrl = $"/api/mods/sync/download/{Uri.EscapeDataString(folderName)}"
                    });
                }
            }
        }

        return result;
    }

    public List<SyncModInfo> GetCustomModsList()
    {
        var result = new List<SyncModInfo>();
        var seenUniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var persistentDir = GetPersistentModsDirectory();

        if (Directory.Exists(persistentDir))
        {
            // Recursive scan for all manifests in persistent directory
            try
            {
                var manifestFiles = Directory.GetFiles(persistentDir, "manifest.json", SearchOption.AllDirectories);
                foreach (var manifestPath in manifestFiles)
                {
                    var modDir = Path.GetDirectoryName(manifestPath);
                    if (string.IsNullOrEmpty(modDir)) continue;

                    var relativePath = Path.GetRelativePath(persistentDir, modDir).Replace('\\', '/');
                    if (relativePath.StartsWith(".") || relativePath.Contains("/.") ||
                        relativePath.Split('/').Any(p => ExcludedFolderNames.Contains(p)))
                    {
                        continue;
                    }

                    var folderName = Path.GetFileName(modDir);
                    var modInfo = GetModInfoFromFolder(modDir, folderName, relativePath);
                    if (modInfo != null && seenUniqueIds.Add(modInfo.UniqueId))
                    {
                        result.Add(modInfo);
                    }
                }
            }
            catch { }

            // Fallback for custom top-level folders
            foreach (var dir in Directory.GetDirectories(persistentDir))
            {
                var folderName = Path.GetFileName(dir);
                if (ExcludedFolderNames.Contains(folderName) ||
                    result.Any(r => r.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase) || r.FolderName.StartsWith(folderName + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                long size = 0;
                try
                {
                    size = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                }
                catch { }

                result.Add(new SyncModInfo
                {
                    UniqueId = folderName,
                    Name = folderName,
                    Version = "custom",
                    FolderName = folderName,
                    SizeBytes = size,
                    DownloadUrl = $"/api/mods/sync/download/{Uri.EscapeDataString(folderName)}"
                });
            }
        }

        return result;
    }

    public (bool Success, string Message, List<SyncModInfo> InstalledMods) InstallModZip(Stream zipStream, string fileName)
    {
        return InstallModZipAsync(zipStream, fileName).GetAwaiter().GetResult();
    }

    public async Task<(bool Success, string Message, List<SyncModInfo> InstalledMods)> InstallModZipAsync(Stream zipStream, string fileName)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"upload_{Guid.NewGuid():N}.zip");
        try
        {
            await using (var fs = File.Create(tempFile))
            {
                await zipStream.CopyToAsync(fs);
            }

            if (new FileInfo(tempFile).Length == 0)
            {
                return (false, "File upload kosong atau rusak.", new List<SyncModInfo>());
            }

            var persistentDir = GetPersistentModsDirectory();
            var activeModsDir = GetModsDirectory();

            using var archive = ZipFile.OpenRead(tempFile);
            if (archive.Entries.Count == 0)
            {
                return (false, "File ZIP tidak memiliki isi.", new List<SyncModInfo>());
            }

            var detectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check if manifest.json is at the root
            var rootManifest = archive.Entries.FirstOrDefault(e => e.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));
            if (rootManifest != null)
            {
                string folderName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(folderName)) folderName = "CustomMod_" + DateTime.UtcNow.Ticks;

                try
                {
                    using var s = rootManifest.Open();
                    using var r = new StreamReader(s);
                    var raw = JsonConvert.DeserializeObject<dynamic>(r.ReadToEnd());
                    if (raw?.Name != null) folderName = SanitizeFolderName((string)raw.Name);
                    else if (raw?.UniqueID != null) folderName = SanitizeFolderName((string)raw.UniqueID);
                }
                catch { }

                var targetPersistent = Path.Combine(persistentDir, folderName);
                var targetActive = Path.Combine(activeModsDir, folderName);

                if (Directory.Exists(targetPersistent)) Directory.Delete(targetPersistent, true);
                if (Directory.Exists(targetActive)) Directory.Delete(targetActive, true);

                Directory.CreateDirectory(targetPersistent);
                Directory.CreateDirectory(targetActive);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var pPath = Path.Combine(targetPersistent, entry.FullName);
                    var aPath = Path.Combine(targetActive, entry.FullName);

                    Directory.CreateDirectory(Path.GetDirectoryName(pPath)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);

                    entry.ExtractToFile(pPath, true);
                    File.Copy(pPath, aPath, true);
                }

                detectedFolders.Add(folderName);
            }
            else
            {
                // Multi-mod or top-level directory structure
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var cleanPath = entry.FullName.Replace('\\', '/').TrimStart('/');
                    var parts = cleanPath.Split('/');
                    if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                    {
                        detectedFolders.Add(parts[0]);
                    }

                    var pPath = Path.Combine(persistentDir, cleanPath);
                    var aPath = Path.Combine(activeModsDir, cleanPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(pPath)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);

                    entry.ExtractToFile(pPath, true);
                    try
                    {
                        File.Copy(pPath, aPath, true);
                    }
                    catch { }
                }
            }

            _zipCache.Clear();

            var installed = new List<SyncModInfo>();
            foreach (var folder in detectedFolders)
            {
                var mod = GetModInfoFromFolder(Path.Combine(persistentDir, folder), folder);
                if (mod != null)
                {
                    installed.Add(mod);
                }
            }

            Monitor.Log($"[ModDistribution] Successfully installed {installed.Count} mod(s) to persistent storage ({persistentDir})", LogLevel.Info);
            return (true, $"Berhasil mengunggah dan menginstal {detectedFolders.Count} folder mod ke penyimpanan aman server.", installed);
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Mod upload failed: {ex.Message}", LogLevel.Error);
            return (false, $"Gagal mengekstrak mod: {ex.Message}", new List<SyncModInfo>());
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch { }
        }
    }

    public bool DeleteCustomMod(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName) || ExcludedFolderNames.Contains(folderName))
        {
            return false;
        }

        bool deleted = false;
        var persistentDir = Path.Combine(GetPersistentModsDirectory(), folderName);
        var activeDir = Path.Combine(GetModsDirectory(), folderName);

        if (Directory.Exists(persistentDir))
        {
            try
            {
                Directory.Delete(persistentDir, true);
                deleted = true;
            }
            catch (Exception ex)
            {
                Monitor.Log($"[ModDistribution] Failed to delete persistent mod {persistentDir}: {ex.Message}", LogLevel.Warn);
            }
        }

        if (Directory.Exists(activeDir))
        {
            try
            {
                Directory.Delete(activeDir, true);
                deleted = true;
            }
            catch (Exception ex)
            {
                Monitor.Log($"[ModDistribution] Failed to delete active mod {activeDir}: {ex.Message}", LogLevel.Warn);
            }
        }

        _zipCache.Clear();
        return deleted;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "CustomMod" : cleaned;
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

        // Check in-memory cache first
        if (_zipCache.TryGetValue(folderName, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            return cached.Data;
        }

        var modsDir = GetModsDirectory();
        var targetDir = Path.Combine(modsDir, folderName);

        if (!Directory.Exists(targetDir))
        {
            var persistentDir = GetPersistentModsDirectory();
            var altDir = Path.Combine(persistentDir, folderName);
            if (Directory.Exists(altDir))
            {
                targetDir = altDir;
            }
            else
            {
                return null;
            }
        }

        var cacheDir = Path.Combine(Path.GetTempPath(), "junimo_mod_cache");
        Directory.CreateDirectory(cacheDir);
        var safeZipName = folderName.Replace('/', '_').Replace('\\', '_');
        var cachedZipFile = Path.Combine(cacheDir, $"{safeZipName}.zip");

        var dirInfo = new DirectoryInfo(targetDir);
        var lastWrite = dirInfo.LastWriteTimeUtc;

        if (File.Exists(cachedZipFile))
        {
            var fileInfo = new FileInfo(cachedZipFile);
            if (fileInfo.LastWriteTimeUtc >= lastWrite && fileInfo.Length > 0)
            {
                try
                {
                    var data = File.ReadAllBytes(cachedZipFile);
                    _zipCache[folderName] = (DateTime.UtcNow, data);
                    return data;
                }
                catch { }
            }
        }

        try
        {
            var tempTarget = cachedZipFile + ".tmp";
            if (File.Exists(tempTarget)) File.Delete(tempTarget);

            using (var fs = new FileStream(tempTarget, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                var entryPrefix = Path.GetFileName(targetDir);
                AddDirectoryToArchive(archive, targetDir, entryPrefix);
            }

            if (File.Exists(cachedZipFile)) File.Delete(cachedZipFile);
            File.Move(tempTarget, cachedZipFile);

            var bytes = File.ReadAllBytes(cachedZipFile);
            _zipCache[folderName] = (DateTime.UtcNow, bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Monitor.Log($"[ModDistribution] Failed to zip mod '{folderName}': {ex.Message}", LogLevel.Error);
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
        var persistentDir = GetPersistentModsDirectory();
        var syncMods = GetSyncableMods();

        try
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var mod in syncMods)
                {
                    var sourceDir = Path.Combine(modsDir, mod.FolderName);
                    if (!Directory.Exists(sourceDir))
                    {
                        var altDir = Path.Combine(persistentDir, mod.FolderName);
                        if (Directory.Exists(altDir))
                        {
                            sourceDir = altDir;
                        }
                    }

                    if (Directory.Exists(sourceDir))
                    {
                        var entryPrefix = Path.GetFileName(sourceDir);
                        AddDirectoryToArchive(archive, sourceDir, entryPrefix);
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
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
        }
    }
}

