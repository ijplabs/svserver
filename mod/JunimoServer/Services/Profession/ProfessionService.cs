using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Redis;
using JunimoServer.Util;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Tools;
using StardewValley.Objects;

namespace JunimoServer.Services.Profession;

/// <summary>
/// Service managing the 5-profession specialization RPG system.
/// Handles role selection, persistence in Redis, starter kit delivery,
/// and chicken allocation for Ranchers.
/// </summary>
public class ProfessionService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly IRedisService _redis;
    private readonly ChatCommandsService _chatCommandsService;

    private const string ProfessionKeyPrefix = "player:profession:";
    private const string StarterKitKeyPrefix = "player:starter_kit:";
    private const string RancherSetKey = "professions:ranchers";

    private readonly ConcurrentDictionary<long, ProfessionType> _professionCache = new();
    private readonly ConcurrentDictionary<long, bool> _kitGrantedCache = new();

    public ProfessionService(
        IModHelper helper,
        IMonitor monitor,
        IRedisService redis,
        ChatCommandsService chatCommandsService
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _redis = redis;
        _chatCommandsService = chatCommandsService;

        RegisterCommands();
        _helper.Events.Player.Warped += OnPlayerWarped;
        _helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
        _helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;

        _monitor.Log("[Profession] ProfessionService initialized.", LogLevel.Info);
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "profesi",
            "Pilih profesi spesialisasi (!profesi <farmer|miner|fisherman|rancher|forager>)",
            HandleProfessionCommand
        );

        _chatCommandsService.RegisterCommand(
            "profession",
            "Pilih profesi spesialisasi (!profession <farmer|miner|fisherman|rancher|forager>)",
            HandleProfessionCommand
        );

        _chatCommandsService.RegisterCommand(
            "role",
            "Lihat atau pilih peran spesialisasi (!role <pilihan>)",
            HandleProfessionCommand
        );

        _chatCommandsService.RegisterCommand(
            "claimkit",
            "Ambil starter kit profesi jika belum diterima (!claimkit)",
            HandleClaimKitCommand
        );
    }

    private void HandleProfessionCommand(string[] args, ReceivedMessage msg)
    {
        long farmerId = msg.SourceFarmer;

        if (args.Length == 0)
        {
            Task.Run(async () =>
            {
                var current = await GetPlayerProfessionAsync(farmerId);
                int rancherCount = await GetRancherCountAsync();

                if (current.HasValue)
                {
                    _helper.SendPrivateMessage(
                        farmerId,
                        $"[aqua][Profesi] Profesi Anda saat ini: [green]{GetProfessionDisplayName(current.Value)}[white]."
                    );
                    _helper.SendPrivateMessage(
                        farmerId,
                        "[gray]Ketik: !claimkit jika Anda belum menerima starter kit di ransel."
                    );
                }
                else
                {
                    _helper.SendPrivateMessage(farmerId, "[aqua]========================================");
                    _helper.SendPrivateMessage(farmerId, "[yellow][Profesi] Anda belum memilih profesi RPG!");
                    _helper.SendPrivateMessage(farmerId, "[white]Ketik salah satu perintah berikut (PILIHAN PERMANEN 1 KALI):");
                    _helper.SendPrivateMessage(farmerId, "  - !profesi farmer    [gray](Petani: Quality Sprinkler, 15 bibit, fertilizer)");
                    _helper.SendPrivateMessage(farmerId, "  - !profesi miner     [gray](Penambang: Copper Pickaxe, 10 torch, 3 bomb, treat)");
                    _helper.SendPrivateMessage(farmerId, "  - !profesi fisherman [gray](Nelayan: Fiberglass Rod, 20 bait, 2 crabpot, dish)");
                    _helper.SendPrivateMessage(farmerId, $"  - !profesi rancher   [gray](Peternak: Kandang Coop, 2 ayam, jerami, pail, shears) [{rancherCount}/2]");
                    _helper.SendPrivateMessage(farmerId, "  - !profesi forager   [gray](Penebang: Copper Axe, charcoal kiln, tree fertilizer)");
                    _helper.SendPrivateMessage(farmerId, "[aqua]========================================");

                    _ = SendProfessionUIModalToClientAsync(farmerId);
                }
            });
            return;
        }

        string choice = args[0].Trim();

        if (choice.Equals("list", StringComparison.OrdinalIgnoreCase) ||
            choice.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            choice.Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            Task.Run(async () =>
            {
                int rancherCount = await GetRancherCountAsync();
                _helper.SendPrivateMessage(farmerId, "[aqua]========================================");
                _helper.SendPrivateMessage(farmerId, "[yellow]5 Profesi Spesialisasi Junimo Valley RPG:");
                _helper.SendPrivateMessage(farmerId, "  - !profesi farmer    : Petani (1x Quality Sprinkler, 15 bibit, fertilizer)");
                _helper.SendPrivateMessage(farmerId, "  - !profesi miner     : Penambang (Copper Pickaxe, 10 torch, 3 bomb, treat)");
                _helper.SendPrivateMessage(farmerId, "  - !profesi fisherman : Nelayan (Fiberglass Rod, 20 bait, 2 crabpot, dish)");
                _helper.SendPrivateMessage(farmerId, $"  - !profesi rancher   : Peternak (1 Coop, 2 Ayam, 30 hay, pail, shears) [{rancherCount}/2]");
                _helper.SendPrivateMessage(farmerId, "  - !profesi forager   : Penebang (Copper Axe, charcoal kiln, tree fertilizer)");
                _helper.SendPrivateMessage(farmerId, "[aqua]========================================");
            });
            return;
        }

        if (choice.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            Task.Run(async () =>
            {
                var current = await GetPlayerProfessionAsync(farmerId);
                var onlineFarmer = Game1.GetPlayer(farmerId);
                bool alreadyDeliveredV2 = await HasKitV2DeliveredAsync(farmerId);
                bool hasItems = current.HasValue && onlineFarmer != null && PlayerHasAnyStarterKitItem(onlineFarmer, current.Value);

                if (alreadyDeliveredV2 || hasItems)
                {
                    _helper.SendPrivateMessage(
                        farmerId,
                        "[red][Profesi] Anda sudah memiliki dan menerima item starter kit. Profesi tidak dapat direset."
                    );
                    return;
                }

                // Reset profession and kit status
                string profKey = ProfessionKeyPrefix + farmerId;
                await _redis.DeleteAsync(profKey);
                _professionCache.TryRemove(farmerId, out _);

                string kitKey = StarterKitKeyPrefix + farmerId;
                await _redis.DeleteAsync(kitKey);
                _kitGrantedCache.TryRemove(farmerId, out _);

                string v2Key = StarterKitKeyPrefix + "v2:" + farmerId;
                await _redis.DeleteAsync(v2Key);

                if (current == ProfessionType.Rancher)
                {
                    await _redis.HashDeleteAsync(RancherSetKey, farmerId.ToString());
                }

                if (onlineFarmer != null)
                {
                    onlineFarmer.modData.Remove("svserver/profession");
                    onlineFarmer.modData.Remove("svserver/starter_kit_granted");
                    onlineFarmer.modData.Remove("svserver/starter_kit_delivered_v2");
                }

                _helper.SendPrivateMessage(
                    farmerId,
                    "[green][Profesi] Pilihan profesi Anda telah direset! Silakan pilih profesi baru via UI atau ketik: !profesi <farmer | miner | fisherman | rancher | forager>."
                );

                _ = SendProfessionUIModalToClientAsync(farmerId);
            });
            return;
        }

        Task.Run(async () =>
        {
            await ProcessProfessionSelectionAsync(farmerId, choice);
        });
    }

    public async Task ProcessProfessionSelectionAsync(long farmerId, string choice)
    {
        if (!TryParseProfession(choice, out var selectedProfession))
        {
            _helper.SendPrivateMessage(
                farmerId,
                $"[red][Profesi] Pilihan '{choice}' tidak valid. Pilihan yang tersedia: farmer, miner, fisherman, rancher, forager."
            );
            return;
        }

        var current = await GetPlayerProfessionAsync(farmerId);

        if (current.HasValue)
        {
            _helper.SendPrivateMessage(
                farmerId,
                $"[yellow][Profesi] Anda sudah memilih profesi [{GetProfessionDisplayName(current.Value)}]. Pilihan profesi bersifat permanen dan hanya dapat dilakukan 1 kali. (Ketik '!profesi reset' jika belum menerima item)."
            );
            return;
        }

        if (selectedProfession == ProfessionType.Rancher)
        {
            int rancherCount = await GetRancherCountAsync();
            if (rancherCount >= 2 && current != ProfessionType.Rancher)
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[red][Profesi] Kuota Peternak (Rancher) sudah penuh (maksimal 2). Silakan pilih profesi lain: farmer, miner, fisherman, atau forager."
                );
                return;
            }
        }

        bool success = await SetPlayerProfessionAsync(farmerId, selectedProfession);
        if (success)
        {
            _helper.SendPrivateMessage(
                farmerId,
                $"[green][Profesi] Selamat! Anda telah memilih profesi: [{GetProfessionDisplayName(selectedProfession)}]."
            );

            // Notify JunimoClientSync UI if present
            try
            {
                _helper.Multiplayer.SendMessage(
                    selectedProfession.ToString(),
                    "ProfessionConfirmed",
                    modIDs: new[] { "JunimoHost.ClientSync" },
                    playerIDs: new[] { farmerId }
                );
            }
            catch { }

            var onlineFarmer = Game1.GetPlayer(farmerId);
            string locName = onlineFarmer?.currentLocation?.NameOrUniqueName ?? "";
            bool inLobby = locName.Contains("Lobby", StringComparison.OrdinalIgnoreCase);

            if (inLobby)
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[aqua][Profesi] Starter kit Anda akan otomatis masuk ke tas ransel begitu Anda login (!login <password>) dan tiba di area pertanian (Farm)."
                );
            }
            else
            {
                bool alreadyDeliveredV2 = await HasKitV2DeliveredAsync(farmerId);
                if (!alreadyDeliveredV2 && onlineFarmer != null)
                {
                    _helper.Events.GameLoop.OneSecondUpdateTicked += DeliverNow;

                    void DeliverNow(object? s, OneSecondUpdateTickedEventArgs ev)
                    {
                        _helper.Events.GameLoop.OneSecondUpdateTicked -= DeliverNow;
                        try
                        {
                            var f = Game1.GetPlayer(farmerId);
                            if (f != null)
                            {
                                bool ok = DeliverStarterKit(f, selectedProfession);
                                if (ok)
                                {
                                    _ = MarkStarterKitReceivedAsync(farmerId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _monitor.Log($"[Profession] Failed immediate delivery: {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }
        }
        else
        {
            _helper.SendPrivateMessage(
                farmerId,
                "[red][Profesi] Gagal menyimpan pilihan profesi. Coba lagi dalam beberapa saat."
            );
        }
    }

    private void HandleClaimKitCommand(string[] args, ReceivedMessage msg)
    {
        long farmerId = msg.SourceFarmer;
        Task.Run(async () =>
        {
            var profession = await GetPlayerProfessionAsync(farmerId);
            if (!profession.HasValue)
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[yellow][Profesi] Anda belum memilih profesi! Ketik: !profesi <farmer | miner | fisherman | rancher | forager> terlebih dahulu."
                );
                return;
            }

            var onlineFarmer = Game1.GetPlayer(farmerId);
            string locName = onlineFarmer?.currentLocation?.NameOrUniqueName ?? "";
            if (onlineFarmer == null || locName.Contains("Lobby", StringComparison.OrdinalIgnoreCase))
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[yellow][Profesi] Silakan login terlebih dahulu (!login <password>) untuk mengambil starter kit di area pertanian (Farm)."
                );
                return;
            }

            bool alreadyDeliveredV2 = await HasKitV2DeliveredAsync(farmerId);
            if (alreadyDeliveredV2)
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[yellow][Profesi] Starter kit Anda sudah berhasil diterima dan dicatat di server. Starter kit hanya diberikan 1 kali."
                );
                return;
            }

            bool hasKit = await HasKitBeenGrantedAsync(farmerId);
            bool hasItems = PlayerHasAnyStarterKitItem(onlineFarmer, profession.Value);

            if (hasKit && hasItems)
            {
                // Mark v2 to permanently prevent duplicate claims
                onlineFarmer.modData["svserver/starter_kit_delivered_v2"] = "1";
                _ = _redis.SetStringAsync(StarterKitKeyPrefix + "v2:" + farmerId, "1");
                _helper.SendPrivateMessage(
                    farmerId,
                    "[yellow][Profesi] Anda sudah memiliki paket starter kit di ransel Anda! Starter kit hanya dapat diambil 1 kali."
                );
                return;
            }

            if (hasKit && !hasItems)
            {
                _monitor.Log($"[Profession] Player {onlineFarmer.Name} had kit marked granted but 0 items in backpack. Healing and delivering directly into inventory...", LogLevel.Warn);
                _helper.SendPrivateMessage(
                    farmerId,
                    "[aqua][Profesi] Memulihkan perlengkapan starter kit langsung ke dalam ransel Anda..."
                );
            }

            _helper.Events.GameLoop.OneSecondUpdateTicked += DeliverNow;

            void DeliverNow(object? s, OneSecondUpdateTickedEventArgs ev)
            {
                _helper.Events.GameLoop.OneSecondUpdateTicked -= DeliverNow;
                try
                {
                    var f = Game1.GetPlayer(farmerId);
                    if (f != null)
                    {
                        bool ok = DeliverStarterKit(f, profession.Value);
                        if (ok)
                        {
                            _ = MarkStarterKitReceivedAsync(farmerId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Profession] Failed claimkit delivery: {ex.Message}", LogLevel.Error);
                }
            }
        });
    }

    private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (e.Peer == null || e.Peer.PlayerID == 0) return;
        long farmerId = e.Peer.PlayerID;

        Task.Run(async () =>
        {
            await Task.Delay(3500);
            var profession = await GetPlayerProfessionAsync(farmerId);
            if (!profession.HasValue)
            {
                _helper.SendPrivateMessage(
                    farmerId,
                    "[Profesi] Selamat datang! Pilih peran profesi RPG Anda dengan mengetik: !profesi farmer (atau miner, fisherman, rancher, forager) untuk mengambil starter kit spesialisasi Anda!"
                );
                _ = SendProfessionUIModalToClientAsync(farmerId);
            }
        });
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != "JunimoHost.ClientSync") return;

        if (e.Type == "SelectProfession")
        {
            string choice = e.ReadAs<string>();
            Task.Run(async () =>
            {
                await ProcessProfessionSelectionAsync(e.FromPlayerID, choice);
            });
        }
        else if (e.Type == "RequestProfessionUI")
        {
            long farmerId = e.FromPlayerID;
            Task.Run(async () =>
            {
                await SendProfessionUIModalToClientAsync(farmerId);
            });
        }
    }

    public async Task SendProfessionUIModalToClientAsync(long farmerId)
    {
        try
        {
            int rancherCount = await GetRancherCountAsync();
            var prof = await GetPlayerProfessionAsync(farmerId);
            string currentName = prof?.ToString() ?? "";

            var payload = $"{currentName}|{rancherCount}|2";
            _helper.Multiplayer.SendMessage(
                payload,
                "ShowProfessionUI",
                modIDs: new[] { "JunimoHost.ClientSync" },
                playerIDs: new[] { farmerId }
            );
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Profession] Error sending ShowProfessionUI: {ex.Message}", LogLevel.Trace);
        }
    }

    public bool TryParseProfession(string input, out ProfessionType profession)
    {
        string norm = input.Trim().ToLowerInvariant();
        switch (norm)
        {
            case "farmer":
            case "petani":
            case "kebun":
            case "tani":
                profession = ProfessionType.Farmer;
                return true;

            case "miner":
            case "penambang":
            case "tambang":
            case "mine":
                profession = ProfessionType.Miner;
                return true;

            case "fisherman":
            case "nelayan":
            case "mancing":
            case "fisher":
            case "fish":
                profession = ProfessionType.Fisherman;
                return true;

            case "rancher":
            case "peternak":
            case "ternak":
                profession = ProfessionType.Rancher;
                return true;

            case "forager":
            case "penebang":
            case "pengumpul":
            case "lumberjack":
            case "tebang":
            case "kayu":
                profession = ProfessionType.Forager;
                return true;

            default:
                profession = ProfessionType.Farmer;
                return false;
        }
    }

    public static string GetProfessionDisplayName(ProfessionType profession)
    {
        return profession switch
        {
            ProfessionType.Farmer => "Petani (Farmer)",
            ProfessionType.Miner => "Penambang (Miner)",
            ProfessionType.Fisherman => "Nelayan (Fisherman)",
            ProfessionType.Rancher => "Peternak (Rancher)",
            ProfessionType.Forager => "Penebang & Pengumpul (Forager)",
            _ => profession.ToString(),
        };
    }

    public ProfessionType? GetCachedProfession(long farmerId)
    {
        if (_professionCache.TryGetValue(farmerId, out var cached))
        {
            return cached;
        }
        return null;
    }

    public async Task<ProfessionType?> GetPlayerProfessionAsync(long farmerId)
    {
        if (_professionCache.TryGetValue(farmerId, out var cached))
        {
            return cached;
        }

        string key = ProfessionKeyPrefix + farmerId;
        string? val = await _redis.GetStringAsync(key);
        if (!string.IsNullOrEmpty(val) && Enum.TryParse<ProfessionType>(val, true, out var parsed))
        {
            _professionCache[farmerId] = parsed;
            return parsed;
        }

        return null;
    }

    public async Task<bool> SetPlayerProfessionAsync(long farmerId, ProfessionType profession)
    {
        try
        {
            var previous = await GetPlayerProfessionAsync(farmerId);
            string key = ProfessionKeyPrefix + farmerId;
            bool ok = await _redis.SetStringAsync(key, profession.ToString());
            if (!ok) return false;

            _professionCache[farmerId] = profession;

            // Maintain rancher set in redis
            if (previous == ProfessionType.Rancher && profession != ProfessionType.Rancher)
            {
                await _redis.HashDeleteAsync(RancherSetKey, farmerId.ToString());
            }
            else if (profession == ProfessionType.Rancher)
            {
                await _redis.HashSetAsync(RancherSetKey, farmerId.ToString(), "1");
            }

            _monitor.Log($"[Profession] Player {farmerId} profession set to {profession}", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Profession] Error setting profession for {farmerId}: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    public async Task<int> GetRancherCountAsync()
    {
        try
        {
            var hash = await _redis.HashGetAllAsync(RancherSetKey);
            return hash != null ? hash.Count : 0;
        }
        catch
        {
            return _professionCache.Values.Count(p => p == ProfessionType.Rancher);
        }
    }

    public async Task<bool> HasKitBeenGrantedAsync(long farmerId)
    {
        if (_kitGrantedCache.TryGetValue(farmerId, out bool granted) && granted)
        {
            return true;
        }

        var farmer = Game1.GetPlayer(farmerId);
        if (farmer != null && farmer.modData.ContainsKey("svserver/starter_kit_granted"))
        {
            _kitGrantedCache[farmerId] = true;
            return true;
        }

        string key = StarterKitKeyPrefix + farmerId;
        string? val = await _redis.GetStringAsync(key);
        if (val == "1")
        {
            _kitGrantedCache[farmerId] = true;
            return true;
        }

        return false;
    }

    public async Task<bool> HasKitV2DeliveredAsync(long farmerId)
    {
        var farmer = Game1.GetPlayer(farmerId);
        if (farmer != null && farmer.modData.TryGetValue("svserver/starter_kit_delivered_v2", out var val) && val == "1")
        {
            return true;
        }

        string key = StarterKitKeyPrefix + "v2:" + farmerId;
        string? redisVal = await _redis.GetStringAsync(key);
        return redisVal == "1";
    }

    public async Task MarkStarterKitReceivedAsync(long farmerId)
    {
        _kitGrantedCache[farmerId] = true;
        string key = StarterKitKeyPrefix + farmerId;
        await _redis.SetStringAsync(key, "1");

        string v2Key = StarterKitKeyPrefix + "v2:" + farmerId;
        await _redis.SetStringAsync(v2Key, "1");

        var farmer = Game1.GetPlayer(farmerId);
        if (farmer != null)
        {
            farmer.modData["svserver/starter_kit_granted"] = "1";
            farmer.modData["svserver/starter_kit_delivered_v2"] = "1";
        }
    }

    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        if (e.Player == null || e.Player.IsMainPlayer) return;

        // Only deliver kit once player leaves the lobby and is on Farm or Cabin
        string locName = e.NewLocation?.NameOrUniqueName ?? "";
        if (locName.Contains("Lobby", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        long farmerId = e.Player.UniqueMultiplayerID;

        Task.Run(async () =>
        {
            var profession = await GetPlayerProfessionAsync(farmerId);
            if (!profession.HasValue)
            {
                // Do NOT auto-assign. Player must choose explicitly via !profesi.
                return;
            }

            bool alreadyDeliveredV2 = await HasKitV2DeliveredAsync(farmerId);
            if (alreadyDeliveredV2)
            {
                return;
            }

            bool hasKit = await HasKitBeenGrantedAsync(farmerId);
            bool hasItems = PlayerHasAnyStarterKitItem(e.Player, profession.Value);

            if (hasKit && hasItems)
            {
                e.Player.modData["svserver/starter_kit_delivered_v2"] = "1";
                _ = _redis.SetStringAsync(StarterKitKeyPrefix + "v2:" + farmerId, "1");
                return;
            }

            _helper.Events.GameLoop.OneSecondUpdateTicked += DeliverKitOnTick;

            void DeliverKitOnTick(object? s, OneSecondUpdateTickedEventArgs ev)
            {
                _helper.Events.GameLoop.OneSecondUpdateTicked -= DeliverKitOnTick;
                try
                {
                    bool ok = DeliverStarterKit(e.Player, profession.Value);
                    if (ok)
                    {
                        _ = MarkStarterKitReceivedAsync(farmerId);
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Profession] Failed delivering kit to {e.Player.Name}: {ex.Message}", LogLevel.Error);
                }
            }
        });
    }

    public static bool PlayerHasAnyStarterKitItem(Farmer farmer, ProfessionType profession)
    {
        if (farmer?.Items == null) return false;

        return profession switch
        {
            ProfessionType.Farmer => farmer.Items.Any(i => i != null && (i.ItemId == "621" || i.ItemId == "(O)621" || i.Name.Contains("Sprinkler"))),
            ProfessionType.Miner => farmer.Items.Any(i => i != null && (i.ItemId == "286" || i.ItemId == "243" || i is Pickaxe p && p.UpgradeLevel >= 1)),
            ProfessionType.Fisherman => farmer.Items.Any(i => i != null && (i.ItemId == "710" || i.ItemId == "242" || i is FishingRod f && f.UpgradeLevel >= 2)),
            ProfessionType.Rancher => farmer.Items.Any(i => i != null && (i.ItemId == "178" || i is MilkPail || i is Shears)),
            ProfessionType.Forager => farmer.Items.Any(i => i != null && (i.ItemId == "805" || i.ItemId == "241" || i is Axe a && a.UpgradeLevel >= 1 || i.ItemId == "114" || i.ItemId == "(BC)114")),
            _ => false,
        };
    }

    public bool DeliverStarterKit(Farmer farmer, ProfessionType profession)
    {
        if (farmer == null) return false;

        farmer.modData["svserver/profession"] = profession.ToString();

        _monitor.Log($"[Profession] Delivering {profession} starter kit directly into {farmer.Name}'s backpack ({farmer.UniqueMultiplayerID})", LogLevel.Info);

        var loc = farmer.currentLocation ?? Game1.getLocationFromName("Farm");

        // Clean up any unpickable item debris left from previous versions
        if (loc != null)
        {
            try
            {
                var stuckDebris = loc.debris.Where(d => d.item != null).ToList();
                foreach (var d in stuckDebris)
                {
                    loc.debris.Remove(d);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Profession] Could not clear old debris: {ex.Message}", LogLevel.Trace);
            }
        }

        var starterItems = new List<Item>();

        switch (profession)
        {
            case ProfessionType.Farmer:
                // 1x Quality Sprinkler
                starterItems.Add(ItemRegistry.Create("(O)621", 1));

                // 15x Seasonal Seeds
                string seedId = Game1.currentSeason switch
                {
                    "summer" => "(O)487", // Corn Seeds
                    "fall" => "(O)490",   // Eggplant Seeds
                    "winter" => "(O)495", // Winter Seeds
                    _ => "(O)472",        // Parsnip Seeds (Spring default)
                };
                starterItems.Add(ItemRegistry.Create(seedId, 15));

                // 15x Basic Fertilizer
                starterItems.Add(ItemRegistry.Create("(O)368", 15));
                break;

            case ProfessionType.Miner:
                // Upgrade Pickaxe to Copper in-place if farmer has one, otherwise add it
                var existingPickaxe = farmer.Items.OfType<Pickaxe>().FirstOrDefault();
                if (existingPickaxe != null)
                {
                    existingPickaxe.UpgradeLevel = Math.Max(existingPickaxe.UpgradeLevel, 1);
                }
                else
                {
                    starterItems.Add(new Pickaxe { UpgradeLevel = 1 });
                }

                // 10x Torches, 3x Cherry Bombs, 2x Miner's Treat
                starterItems.Add(ItemRegistry.Create("(O)93", 10));
                starterItems.Add(ItemRegistry.Create("(O)286", 3));
                starterItems.Add(ItemRegistry.Create("(O)243", 2));
                break;

            case ProfessionType.Fisherman:
                // Fiberglass Rod
                starterItems.Add(new FishingRod(2));

                // 20x Bait, 2x Crab Pot, 2x Dish o' The Sea
                starterItems.Add(ItemRegistry.Create("(O)685", 20));
                starterItems.Add(ItemRegistry.Create("(O)710", 2));
                starterItems.Add(ItemRegistry.Create("(O)242", 2));
                break;

            case ProfessionType.Rancher:
                // Setup Coop and chickens on Farm
                try
                {
                    SetupRancherCoopAndAnimals(farmer);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Profession] Error setting up coop/animals for {farmer.Name}: {ex.Message}", LogLevel.Error);
                }

                // 30x Hay, Milk Pail, Shears
                starterItems.Add(ItemRegistry.Create("(O)178", 30));
                starterItems.Add(new MilkPail());
                starterItems.Add(new Shears());
                break;

            case ProfessionType.Forager:
                // Upgrade Axe to Copper in-place if farmer has one, otherwise add it
                var existingAxe = farmer.Items.OfType<Axe>().FirstOrDefault();
                if (existingAxe != null)
                {
                    existingAxe.UpgradeLevel = Math.Max(existingAxe.UpgradeLevel, 1);
                }
                else
                {
                    starterItems.Add(new Axe { UpgradeLevel = 1 });
                }

                // 1x Charcoal Kiln, 10x Tree Fertilizer, 2x Survival Burger
                starterItems.Add(ItemRegistry.Create("(BC)114", 1));
                starterItems.Add(ItemRegistry.Create("(O)805", 10));
                starterItems.Add(ItemRegistry.Create("(O)241", 2));
                break;
        }

        var overflow = new List<Item>();
        foreach (var item in starterItems)
        {
            if (item == null) continue;
            var remaining = farmer.addItemToInventory(item);
            if (remaining != null)
            {
                overflow.Add(remaining);
            }
        }

        if (overflow.Count > 0 && loc != null)
        {
            foreach (var leftover in overflow)
            {
                Game1.createItemDebris(leftover, farmer.Position, 2, loc);
            }
            _helper.SendPrivateMessage(
                farmer.UniqueMultiplayerID,
                $"[yellow][Profesi] Ransel penuh! Sebagian starter kit [{GetProfessionDisplayName(profession)}] dijatuhkan di dekat Anda."
            );
        }
        else
        {
            _helper.SendPrivateMessage(
                farmer.UniqueMultiplayerID,
                $"[green][Profesi] Selamat! Paket perlengkapan starter [{GetProfessionDisplayName(profession)}] telah dimasukkan langsung ke dalam tas ransel Anda!"
            );
        }

        farmer.modData["svserver/starter_kit_granted"] = "1";
        farmer.modData["svserver/starter_kit_delivered_v2"] = "1";
        _ = _redis.SetStringAsync(StarterKitKeyPrefix + "v2:" + farmer.UniqueMultiplayerID, "1");
        return true;
    }

    private void SetupRancherCoopAndAnimals(Farmer farmer)
    {
        var farm = Game1.getFarm();
        if (farm == null) return;

        // Find existing coop or create one
        var coop = farm.buildings.FirstOrDefault(b =>
            b.buildingType.Value.Contains("Coop", StringComparison.OrdinalIgnoreCase) ||
            b.GetIndoors() is AnimalHouse
        );

        if (coop == null)
        {
            Vector2 coopTile = new Vector2(48, 14);

            // Clear ground tiles
            for (int x = (int)coopTile.X; x < (int)coopTile.X + 6; x++)
            {
                for (int y = (int)coopTile.Y; y < (int)coopTile.Y + 3; y++)
                {
                    var pos = new Vector2(x, y);
                    farm.terrainFeatures.Remove(pos);
                    farm.Objects.Remove(pos);
                    for (int i = farm.resourceClumps.Count - 1; i >= 0; i--)
                    {
                        if (farm.resourceClumps[i].Tile == pos)
                        {
                            farm.resourceClumps.RemoveAt(i);
                        }
                    }
                }
            }

            coop = new Building("Coop", coopTile);
            farm.buildings.Add(coop);
            coop.load();
            _monitor.Log("[Profession] Spawned new Starter Coop at (48, 14)", LogLevel.Info);
        }

        if (coop.GetIndoors() is not AnimalHouse animalHouse)
        {
            _monitor.Log("[Profession] Coop indoors is not AnimalHouse", LogLevel.Warn);
            return;
        }

        // Determine if this farmer already has animals in the coop
        bool alreadyHasAnimals = animalHouse.animals.Values.Any(a => a.ownerID.Value == farmer.UniqueMultiplayerID);
        if (alreadyHasAnimals)
        {
            return;
        }

        // Check if there is already another rancher with white chickens
        bool hasWhiteChickens = animalHouse.animals.Values.Any(a => a.type.Value.Contains("White", StringComparison.OrdinalIgnoreCase));
        bool isFirstRancher = !hasWhiteChickens;

        string chickenType = isFirstRancher ? "White Chicken" : "Brown Chicken";
        string prefixName1 = isFirstRancher ? "Clucky" : "Chicky";
        string prefixName2 = isFirstRancher ? "Pipit" : "Nugget";

        var names = new[] { $"[{farmer.Name}] {prefixName1}", $"[{farmer.Name}] {prefixName2}" };

        foreach (var name in names)
        {
            long animalId = Game1.Multiplayer.getNewID();
            var animal = new FarmAnimal(chickenType, animalId, farmer.UniqueMultiplayerID)
            {
                Name = name,
                displayName = name,
                home = coop,
            };
            animal.ownerID.Value = farmer.UniqueMultiplayerID;
            animal.modData["svserver/owner_name"] = farmer.Name;
            animal.modData["svserver/owner_id"] = farmer.UniqueMultiplayerID.ToString();
            animal.Position = new Vector2(3 * 64, 3 * 64);

            animalHouse.animals.Add(animalId, animal);
            _monitor.Log($"[Profession] Spawned {chickenType} '{name}' for {farmer.Name}", LogLevel.Info);
        }
    }
}
