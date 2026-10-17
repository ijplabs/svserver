using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Roles;
using JunimoServer.Util;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace JunimoServer.Services.Backup;

public class BackupEntry
{
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Season { get; set; } = "";
    public int Day { get; set; }
    public int Year { get; set; }
}

/// <summary>
/// Service providing automated world save backups and snapshotting.
/// Automatically creates compressed zip archives of the active save upon day ends (GameLoop.Saved)
/// and allows manual backups via in-game chat (!backup) or REST API.
/// </summary>
public class BackupService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly RoleService _roleService;
    private readonly ChatCommandsService _chatCommandsService;

    public BackupService(
        IModHelper helper,
        IMonitor monitor,
        RoleService roleService,
        ChatCommandsService chatCommandsService
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _roleService = roleService;
        _chatCommandsService = chatCommandsService;

        _helper.Events.GameLoop.SaveCreated += OnSaveCreated;
        _helper.Events.GameLoop.Saved += OnSaved;

        RegisterCommands();
        _monitor.Log("[Backup] BackupService initialized (Retention: 30 snapshots)", LogLevel.Info);
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "backup",
            "Creates an immediate backup archive of the active save file.",
            (args, msg) =>
            {
                if (!_roleService.IsPlayerAdmin(msg.SourceFarmer))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "[Backup] Admin permissions required.");
                    return;
                }

                _helper.SendPublicMessage("[Backup] Creating save backup archive...");
                var result = CreateBackup();
                if (result != null)
                {
                    _helper.SendPublicMessage($"[Backup] Backup successful: {result.FileName} ({result.SizeBytes / 1024} KB)");
                }
                else
                {
                    _helper.SendPublicMessage("[Backup] Backup failed. Check server logs.");
                }
            },
            requiresAdmin: true
        );
    }

    private void OnSaveCreated(object sender, SaveCreatedEventArgs e)
    {
        CreateBackup();
    }

    private void OnSaved(object sender, SavedEventArgs e)
    {
        CreateBackup();
    }

    public string GetBackupsDirectory()
    {
        string dir;
        if (Directory.Exists("/data"))
        {
            dir = "/data/backups";
        }
        else
        {
            dir = Path.Combine(Constants.SavesPath, "..", "Backups");
        }

        Directory.CreateDirectory(dir);
        return dir;
    }

    public BackupEntry? CreateBackup()
    {
        if (string.IsNullOrEmpty(Constants.SaveFolderName))
        {
            _monitor.Log("[Backup] Cannot create backup: no save folder is currently loaded.", LogLevel.Warn);
            return null;
        }

        var sourcePath = Path.Combine(Constants.SavesPath, Constants.SaveFolderName);
        if (!Directory.Exists(sourcePath))
        {
            _monitor.Log($"[Backup] Cannot create backup: source save directory '{sourcePath}' does not exist.", LogLevel.Warn);
            return null;
        }

        try
        {
            var backupsDir = GetBackupsDirectory();
            var dateNow = SDate.Now();
            var timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{Constants.SaveFolderName}_{dateNow.Season}_{dateNow.Day}_Y{dateNow.Year}_{timeStamp}.zip";
            var targetZip = Path.Combine(backupsDir, fileName);

            ZipFile.CreateFromDirectory(sourcePath, targetZip, CompressionLevel.Optimal, false);

            var fileInfo = new FileInfo(targetZip);
            _monitor.Log($"[Backup] Successfully created save backup: {fileName} ({fileInfo.Length} bytes)", LogLevel.Info);

            PruneOldBackups();

            return new BackupEntry
            {
                FileName = fileName,
                SizeBytes = fileInfo.Length,
                CreatedAtUtc = fileInfo.CreationTimeUtc,
                Season = dateNow.Season,
                Day = dateNow.Day,
                Year = dateNow.Year
            };
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Backup] Exception during backup creation: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            return null;
        }
    }

    public List<BackupEntry> GetBackups()
    {
        var list = new List<BackupEntry>();
        var backupsDir = GetBackupsDirectory();
        var dirInfo = new DirectoryInfo(backupsDir);

        if (!dirInfo.Exists)
            return list;

        foreach (var file in dirInfo.GetFiles("*.zip").OrderByDescending(f => f.CreationTimeUtc))
        {
            list.Add(new BackupEntry
            {
                FileName = file.Name,
                SizeBytes = file.Length,
                CreatedAtUtc = file.CreationTimeUtc
            });
        }

        return list;
    }

    public string? GetBackupFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        {
            return null;
        }

        var fullPath = Path.Combine(GetBackupsDirectory(), fileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public bool DeleteBackup(string fileName)
    {
        var path = GetBackupFilePath(fileName);
        if (path != null && File.Exists(path))
        {
            try
            {
                File.Delete(path);
                _monitor.Log($"[Backup] Manually deleted backup archive: {fileName}", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Backup] Failed to delete backup archive {fileName}: {ex.Message}", LogLevel.Error);
            }
        }
        return false;
    }

    private void PruneOldBackups()
    {
        try
        {
            var backupsDir = GetBackupsDirectory();
            var dirInfo = new DirectoryInfo(backupsDir);
            var files = dirInfo.GetFiles("*.zip")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(30)
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                    _monitor.Log($"[Backup] Pruned old backup archive: {file.Name}", LogLevel.Trace);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Backup] Failed to prune old backups: {ex.Message}", LogLevel.Debug);
        }
    }
}
