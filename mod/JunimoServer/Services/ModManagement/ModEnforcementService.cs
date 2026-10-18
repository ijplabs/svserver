using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using JunimoServer.Util;

namespace JunimoServer.Services.ModManagement;

public class ModPolicyData
{
    public string EnforcementMode { get; set; } = "Disabled"; // "Disabled", "RequireSmapi", "StrictRequired"
    public string UnknownModsMode { get; set; } = "Ignore"; // "Ignore" (default - safe for cosmetics), "StrictReject"
    public List<RequiredModRule> RequiredMods { get; set; } = new();
    public List<BlacklistedModRule> BlacklistedMods { get; set; } = new();
    public List<string> WhitelistModIds { get; set; } = new();
}

public class RequiredModRule
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string MinVersion { get; set; } = "";
    public bool Required { get; set; } = true;
}

public class BlacklistedModRule
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class ServerInstalledModInfo
{
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsFramework { get; set; }
}

public class PeerAuditInfo
{
    public long PlayerId { get; set; }
    public string FarmerName { get; set; } = "";
    public bool HasSmapi { get; set; }
    public string SmapiVersion { get; set; } = "";
    public int ModCount { get; set; }
    public DateTime ConnectedAtUtc { get; set; }
    public List<PeerModItem> Mods { get; set; } = new();
}

public class PeerModItem
{
    public string UniqueId { get; set; } = "";
    public string Version { get; set; } = "";
}

public class ModRejectionLog
{
    public long PlayerId { get; set; }
    public string FarmerName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ModId { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
}

/// <summary>
/// Centralized Mod Management and Client Mod Enforcement Service.
/// Directly validates connecting multiplayer peers using SMAPI multiplayer protocol.
/// Evaluates SMAPI presence, required mod IDs/minimum versions, and blacklisted mods.
/// Rejects non-compliant clients with explanatory messages without requiring custom client mods.
/// </summary>
public class ModEnforcementService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    private readonly object _lock = new();
    private ModPolicyData _policy = new();

    private readonly ConcurrentDictionary<long, PeerAuditInfo> _connectedPeers = new();
    private readonly List<ModRejectionLog> _recentRejections = new();
    private const int MaxRejectionHistory = 50;

    public ModEnforcementService(IModHelper helper, IMonitor monitor) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;

        LoadPolicy();

        _helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
        _helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;

        _monitor.Log($"[ModEnforcement] Initialized. Mode: {_policy.EnforcementMode}, Required: {_policy.RequiredMods.Count}, Blacklisted: {_policy.BlacklistedMods.Count}", LogLevel.Info);
    }

    private string GetConfigFilePath()
    {
        // 1. Persistent volume: /data/settings (mounted in Docker Compose & Coolify)
        if (Directory.Exists("/data/settings"))
        {
            return "/data/settings/mod-policy.json";
        }
        // 2. Persistent volume: /data/game (mounted named volume game-data)
        if (Directory.Exists("/data/game"))
        {
            return "/data/game/mod-policy.json";
        }
        // 3. Fallback: /data (container root)
        if (Directory.Exists("/data"))
        {
            return "/data/mod-policy.json";
        }
        return Path.Combine(Constants.SavesPath, "..", "mod-policy.json");
    }

    private void LoadPolicy()
    {
        lock (_lock)
        {
            try
            {
                var candidatePaths = new[]
                {
                    GetConfigFilePath(),
                    "/data/settings/mod-policy.json",
                    "/data/game/mod-policy.json",
                    "/data/mod-policy.json",
                    Path.Combine(Constants.SavesPath, "..", "mod-policy.json")
                };

                foreach (var path in candidatePaths.Distinct())
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var loaded = JsonConvert.DeserializeObject<ModPolicyData>(json);
                        if (loaded != null && (loaded.RequiredMods.Count > 0 || loaded.BlacklistedMods.Count > 0 || !string.Equals(loaded.EnforcementMode, "Disabled", StringComparison.OrdinalIgnoreCase)))
                        {
                            _policy = loaded;
                            // Ensure migrated to primary persistent location
                            SavePolicyInternal();
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[ModEnforcement] Error loading policy file: {ex.Message}", LogLevel.Warn);
            }

            _policy = new ModPolicyData
            {
                EnforcementMode = "Disabled",
                UnknownModsMode = "Ignore",
                RequiredMods = new List<RequiredModRule>(),
                BlacklistedMods = new List<BlacklistedModRule>(),
                WhitelistModIds = new List<string>()
            };
            SavePolicyInternal();
        }
    }

    private void SavePolicyInternal()
    {
        try
        {
            var path = GetConfigFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonConvert.SerializeObject(_policy, Formatting.Indented);
            File.WriteAllText(path, json);

            // Also mirror backup to /data/game/mod-policy.json if /data/game exists and path is different
            if (Directory.Exists("/data/game") && !path.StartsWith("/data/game", StringComparison.OrdinalIgnoreCase))
            {
                try { File.WriteAllText("/data/game/mod-policy.json", json); } catch { }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[ModEnforcement] Error saving policy file: {ex.Message}", LogLevel.Warn);
        }
    }

    public ModPolicyData GetPolicy()
    {
        lock (_lock)
        {
            return new ModPolicyData
            {
                EnforcementMode = _policy.EnforcementMode ?? "Disabled",
                UnknownModsMode = _policy.UnknownModsMode ?? "Ignore",
                RequiredMods = new List<RequiredModRule>(_policy.RequiredMods),
                BlacklistedMods = new List<BlacklistedModRule>(_policy.BlacklistedMods),
                WhitelistModIds = new List<string>(_policy.WhitelistModIds ?? new List<string>())
            };
        }
    }

    public void UpdatePolicy(ModPolicyData newPolicy)
    {
        lock (_lock)
        {
            _policy.EnforcementMode = newPolicy.EnforcementMode ?? "Disabled";
            _policy.UnknownModsMode = newPolicy.UnknownModsMode ?? "Ignore";
            _policy.RequiredMods = newPolicy.RequiredMods ?? new List<RequiredModRule>();
            _policy.BlacklistedMods = newPolicy.BlacklistedMods ?? new List<BlacklistedModRule>();
            _policy.WhitelistModIds = newPolicy.WhitelistModIds ?? new List<string>();
            SavePolicyInternal();
        }

        _monitor.Log($"[ModEnforcement] Policy updated: Mode={_policy.EnforcementMode}, UnknownModsMode={_policy.UnknownModsMode}, {_policy.RequiredMods.Count} required mods", LogLevel.Info);
    }

    public static bool IsInternalServerMod(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true;
        return id.Equals("JunimoHost.Server", StringComparison.OrdinalIgnoreCase) ||
               id.Equals("JunimoServer.Shared", StringComparison.OrdinalIgnoreCase) ||
               id.Equals("SaveBackup", StringComparison.OrdinalIgnoreCase) ||
               id.Equals("ConsoleCommands", StringComparison.OrdinalIgnoreCase) ||
               id.StartsWith("SMAPI.", StringComparison.OrdinalIgnoreCase) ||
               id.Equals("ErrorHandler", StringComparison.OrdinalIgnoreCase);
    }

    public List<ServerInstalledModInfo> GetInstalledServerMods()
    {
        var list = new List<ServerInstalledModInfo>();
        try
        {
            foreach (var mod in _helper.ModRegistry.GetAll())
            {
                if (mod?.Manifest == null) continue;

                var id = mod.Manifest.UniqueID ?? "";
                if (IsInternalServerMod(id)) continue;

                var name = mod.Manifest.Name ?? id;
                var version = mod.Manifest.Version?.ToString() ?? "1.0.0";
                var author = mod.Manifest.Author ?? "";
                var description = mod.Manifest.Description ?? "";
                bool isFramework = id.Contains("ContentPatcher") || id.Contains("SMAPI") || id.Contains("Expanded") || id.Contains("SpaceCore");

                list.Add(new ServerInstalledModInfo
                {
                    UniqueId = id,
                    Name = name,
                    Version = version,
                    Author = author,
                    Description = description,
                    IsFramework = isFramework
                });
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[ModEnforcement] Error scanning server mods: {ex.Message}", LogLevel.Warn);
        }

        return list.OrderBy(m => m.Name).ToList();
    }

    public int AutoSyncServerMods(IEnumerable<ServerInstalledModInfo>? extraMods = null)
    {
        int added = 0;
        lock (_lock)
        {
            var serverMods = GetInstalledServerMods();
            if (extraMods != null)
            {
                foreach (var extra in extraMods)
                {
                    if (!IsInternalServerMod(extra.UniqueId) && !serverMods.Any(s => s.UniqueId.Equals(extra.UniqueId, StringComparison.OrdinalIgnoreCase)))
                    {
                        serverMods.Add(extra);
                    }
                }
            }

            foreach (var mod in serverMods)
            {
                if (IsInternalServerMod(mod.UniqueId))
                {
                    continue;
                }

                if (!_policy.RequiredMods.Any(r => r.UniqueId.Equals(mod.UniqueId, StringComparison.OrdinalIgnoreCase)))
                {
                    _policy.RequiredMods.Add(new RequiredModRule
                    {
                        UniqueId = mod.UniqueId,
                        Name = mod.Name,
                        MinVersion = mod.Version != "custom" ? mod.Version : "",
                        Required = true
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                SavePolicyInternal();
            }
        }

        return added;
    }

    public List<PeerAuditInfo> GetConnectedPeerAudits()
    {
        return _connectedPeers.Values.ToList();
    }

    public List<ModRejectionLog> GetRecentRejections()
    {
        lock (_lock)
        {
            return new List<ModRejectionLog>(_recentRejections);
        }
    }

    private void RecordRejection(long playerId, string farmerName, string reason, string modId)
    {
        lock (_lock)
        {
            _recentRejections.Insert(0, new ModRejectionLog
            {
                PlayerId = playerId,
                FarmerName = farmerName,
                Reason = reason,
                ModId = modId,
                TimestampUtc = DateTime.UtcNow
            });

            if (_recentRejections.Count > MaxRejectionHistory)
            {
                _recentRejections.RemoveAt(_recentRejections.Count - 1);
            }
        }

        _monitor.Log($"[ModEnforcement] REJECTED player {farmerName} ({playerId}): {reason}", LogLevel.Warn);
    }

    private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
    {
        long playerId = e.Peer.PlayerID;
        var farmer = Game1.getFarmer(playerId) ?? Game1.otherFarmers.Values.FirstOrDefault(f => f.UniqueMultiplayerID == playerId);
        string farmerName = !string.IsNullOrWhiteSpace(farmer?.Name) ? farmer.Name : $"Farmer #{playerId}";

        // Audit player mods
        var audit = new PeerAuditInfo
        {
            PlayerId = playerId,
            FarmerName = farmerName,
            HasSmapi = e.Peer.HasSmapi,
            SmapiVersion = e.Peer.ApiVersion?.ToString() ?? (e.Peer.HasSmapi ? "Detected" : "None (Vanilla)"),
            ConnectedAtUtc = DateTime.UtcNow,
            Mods = new List<PeerModItem>()
        };

        if (e.Peer.Mods != null)
        {
            foreach (var mod in e.Peer.Mods)
            {
                audit.Mods.Add(new PeerModItem
                {
                    UniqueId = mod.ID,
                    Version = mod.Version?.ToString() ?? "unknown"
                });
            }
        }
        audit.ModCount = audit.Mods.Count;
        _connectedPeers[playerId] = audit;

        // Perform enforcement
        ModPolicyData policy;
        lock (_lock)
        {
            policy = _policy;
        }

        if (policy.EnforcementMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Check 1: SMAPI Requirement
        if (!e.Peer.HasSmapi)
        {
            string rejectMsg = "[Server] Connection rejected: SMAPI is required on this server.";
            _helper.SendPrivateMessage(playerId, rejectMsg);
            RecordRejection(playerId, farmerName, "Vanilla client without SMAPI", "SMAPI");
            Game1.server.kick(playerId);
            return;
        }

        // Check 2: Blacklisted Mods
        if (policy.BlacklistedMods != null && policy.BlacklistedMods.Count > 0)
        {
            foreach (var black in policy.BlacklistedMods)
            {
                var clientMod = e.Peer.GetMod(black.UniqueId) ??
                               e.Peer.Mods?.FirstOrDefault(m => string.Equals(m.ID, black.UniqueId, StringComparison.OrdinalIgnoreCase));

                if (clientMod != null)
                {
                    string rejectMsg = $"[Server] Disallowed mod detected: {black.Name} ({black.UniqueId}). {black.Reason}";
                    _helper.SendPrivateMessage(playerId, rejectMsg);
                    RecordRejection(playerId, farmerName, $"Disallowed mod: {black.Name}", black.UniqueId);
                    Game1.server.kick(playerId);
                    return;
                }
            }
        }

        // Check 3: Strict Required Mods
        if (policy.EnforcementMode.Equals("StrictRequired", StringComparison.OrdinalIgnoreCase) &&
            policy.RequiredMods != null && policy.RequiredMods.Count > 0)
        {
            foreach (var req in policy.RequiredMods.Where(r => r.Required))
            {
                var clientMod = e.Peer.GetMod(req.UniqueId) ??
                               e.Peer.Mods?.FirstOrDefault(m => string.Equals(m.ID, req.UniqueId, StringComparison.OrdinalIgnoreCase));

                if (clientMod == null)
                {
                    string rejectMsg = $"[Server] Missing required mod: {req.Name} ({req.UniqueId}). Please install it to join.";
                    _helper.SendPrivateMessage(playerId, rejectMsg);
                    RecordRejection(playerId, farmerName, $"Missing required mod: {req.Name}", req.UniqueId);
                    Game1.server.kick(playerId);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(req.MinVersion) && clientMod.Version != null)
                {
                    string clientVerStr = clientMod.Version.ToString();
                    if (IsVersionOlder(clientVerStr, req.MinVersion))
                    {
                        string rejectMsg = $"[Server] Mod '{req.Name}' is outdated (Installed: {clientVerStr}, Required >= {req.MinVersion}).";
                        _helper.SendPrivateMessage(playerId, rejectMsg);
                        RecordRejection(playerId, farmerName, $"Outdated mod {req.Name} ({clientVerStr} < {req.MinVersion})", req.UniqueId);
                        Game1.server.kick(playerId);
                        return;
                    }
                }
            }
        }

        // Check 4: Unknown Mods Handling (Strict Whitelist Mode)
        if (policy.UnknownModsMode.Equals("StrictReject", StringComparison.OrdinalIgnoreCase) && e.Peer.Mods != null)
        {
            var allowedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ConsoleCommands",
                "SaveBackup",
                "ErrorHandler",
                "JunimoClientSync"
            };

            if (policy.RequiredMods != null)
            {
                foreach (var req in policy.RequiredMods)
                {
                    if (!string.IsNullOrWhiteSpace(req.UniqueId))
                    {
                        allowedModIds.Add(req.UniqueId);
                    }
                }
            }

            if (policy.WhitelistModIds != null)
            {
                foreach (var w in policy.WhitelistModIds)
                {
                    if (!string.IsNullOrWhiteSpace(w))
                    {
                        allowedModIds.Add(w);
                    }
                }
            }

            foreach (var peerMod in e.Peer.Mods)
            {
                if (!allowedModIds.Contains(peerMod.ID))
                {
                    string rejectMsg = $"[Server] Mod asing terdeteksi: {peerMod.ID}. Server hanya memperbolehkan mod yang masuk whitelist.";
                    _helper.SendPrivateMessage(playerId, rejectMsg);
                    RecordRejection(playerId, farmerName, $"Mod di luar whitelist: {peerMod.ID}", peerMod.ID);
                    Game1.server.kick(playerId);
                    return;
                }
            }
        }

        _monitor.Log($"[ModEnforcement] Client {farmerName} passed mod validation ({audit.ModCount} mods verified, unknown mods mode: {policy.UnknownModsMode})", LogLevel.Info);
    }

    private void OnPeerDisconnected(object sender, PeerDisconnectedEventArgs e)
    {
        _connectedPeers.TryRemove(e.Peer.PlayerID, out _);
    }

    private static bool IsVersionOlder(string installedVer, string minVer)
    {
        if (string.IsNullOrWhiteSpace(minVer)) return false;
        if (string.IsNullOrWhiteSpace(installedVer)) return true;

        try
        {
            var v1 = new SemanticVersion(installedVer);
            var v2 = new SemanticVersion(minVer);
            return v1.IsOlderThan(v2);
        }
        catch
        {
            var parts1 = installedVer.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            var parts2 = minVer.Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
            {
                if (int.TryParse(parts1[i], out var n1) && int.TryParse(parts2[i], out var n2))
                {
                    if (n1 < n2) return true;
                    if (n1 > n2) return false;
                }
            }
            return false;
        }
    }
}
