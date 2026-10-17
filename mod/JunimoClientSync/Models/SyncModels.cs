using System.Collections.Generic;

namespace JunimoClientSync.Models;

public class SyncModItem
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

public class SyncManifest
{
    public string ServerVersion { get; set; } = "";
    public int ModCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public List<SyncModItem> Mods { get; set; } = new();
    public string BundleDownloadUrl { get; set; } = "";
}
