using System;
using System.Linq;
using HarmonyLib;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Util;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace JunimoServer.Services.Ownership;

/// <summary>
/// Service managing ownership protections for private chests,
/// shared community storage (2 Big Chests near farmhouse),
/// and animal feedback.
/// </summary>
public class OwnershipProtectionService : ModService
{
    private static OwnershipProtectionService? _instance;
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ChatCommandsService _chatCommandsService;

    public OwnershipProtectionService(
        IModHelper helper,
        IMonitor monitor,
        Harmony harmony,
        ChatCommandsService chatCommandsService
    ) : base(helper, monitor)
    {
        _instance = this;
        _helper = helper;
        _monitor = monitor;
        _chatCommandsService = chatCommandsService;

        // Patch Chest interaction
        harmony.Patch(
            original: AccessTools.Method(typeof(Chest), nameof(Chest.checkForAction)),
            prefix: new HarmonyMethod(typeof(OwnershipProtectionService), nameof(Chest_CheckForAction_Prefix))
        );

        // Patch FarmAnimal petting
        harmony.Patch(
            original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.pet)),
            postfix: new HarmonyMethod(typeof(OwnershipProtectionService), nameof(FarmAnimal_Pet_Postfix))
        );

        RegisterCommands();
        _helper.Events.GameLoop.SaveLoaded += (s, e) => EnsureCommunityStorage();
        _helper.Events.GameLoop.DayStarted += (s, e) => EnsureCommunityStorage();

        _monitor.Log("[Ownership] OwnershipProtectionService initialized.", LogLevel.Info);
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "chest",
            "Kelola hak akses peti pribadi (!chest share <nama>, !chest lock)",
            HandleChestCommand
        );
    }

    private void HandleChestCommand(string[] args, ReceivedMessage msg)
    {
        long farmerId = msg.SourceFarmer;
        var farmer = Game1.GetPlayer(farmerId);
        if (farmer == null) return;

        if (args.Length == 0)
        {
            _helper.SendPrivateMessage(farmerId, "[Peti] Penggunaan: !chest share <nama_pemain> atau !chest lock");
            return;
        }

        string action = args[0].ToLowerInvariant();
        var loc = farmer.currentLocation;
        if (loc == null) return;

        // Find closest chest to farmer within 3 tiles
        Chest? targetChest = null;
        float minDistance = float.MaxValue;
        foreach (var obj in loc.Objects.Values)
        {
            if (obj is Chest chest && chest.owner.Value == farmerId)
            {
                float dist = Vector2.Distance(chest.TileLocation, farmer.Tile);
                if (dist < minDistance && dist <= 3.0f)
                {
                    minDistance = dist;
                    targetChest = chest;
                }
            }
        }

        if (targetChest == null)
        {
            _helper.SendPrivateMessage(farmerId, "[Peti] Berdirilah di dekat peti milik Anda (maksimal 3 ubin) untuk mengubah akses.");
            return;
        }

        if (action == "share")
        {
            if (args.Length < 2)
            {
                _helper.SendPrivateMessage(farmerId, "[Peti] Penggunaan: !chest share <nama_pemain>");
                return;
            }

            string targetName = args[1];
            var friend = _helper.FindPlayerIdByFarmerNameOrUserName(targetName);
            if (friend == null)
            {
                _helper.SendPrivateMessage(farmerId, $"[Peti] Pemain '{targetName}' tidak ditemukan atau sedang offline.");
                return;
            }

            targetChest.modData[$"svserver/shared_with_{friend.UniqueMultiplayerID}"] = "true";
            _helper.SendPrivateMessage(farmerId, $"[Peti] Akses peti berhasil dibagikan kepada {friend.Name}.");
            _helper.SendPrivateMessage(friend.UniqueMultiplayerID, $"[Peti] {farmer.Name} telah membagikan akses ke peti pribadinya kepada Anda.");
            return;
        }

        if (action == "lock")
        {
            var keysToRemove = targetChest.modData.Keys.Where(k => k.StartsWith("svserver/shared_with_")).ToList();
            foreach (var k in keysToRemove)
            {
                targetChest.modData.Remove(k);
            }

            _helper.SendPrivateMessage(farmerId, "[Peti] Hak akses bersama telah dicabut. Peti kembali terkunci eksklusif untuk Anda.");
            return;
        }

        _helper.SendPrivateMessage(farmerId, "[Peti] Perintah tidak dikenal. Gunakan: !chest share <nama> atau !chest lock");
    }

    public void EnsureCommunityStorage()
    {
        try
        {
            var farm = Game1.getFarm();
            if (farm == null) return;

            // Coordinates in front of main farmhouse / shipping bin
            Vector2 chest1Tile = new Vector2(68, 14);
            Vector2 chest2Tile = new Vector2(69, 14);

            EnsureBigChest(farm, chest1Tile, new Color(34, 139, 34), "Gudang Bersama - Sumber Daya & Material");
            EnsureBigChest(farm, chest2Tile, new Color(255, 215, 0), "Gudang Bersama - Bibit, Makanan & Peralatan");
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Ownership] Error ensuring community storage: {ex.Message}", LogLevel.Error);
        }
    }

    private void EnsureBigChest(Farm farm, Vector2 tile, Color color, string description)
    {
        if (farm.Objects.TryGetValue(tile, out var existingObj) && existingObj is Chest chest)
        {
            chest.modData["svserver/community_storage"] = "true";
            chest.modData["svserver/storage_name"] = description;
            return;
        }

        // Clear obstacle on tile if any
        farm.terrainFeatures.Remove(tile);
        farm.Objects.Remove(tile);

        var newChest = new Chest(playerChest: true, tile)
        {
            SpecialChestType = Chest.SpecialChestTypes.BigChest,
            playerChoiceColor = { Value = color },
        };
        newChest.modData["svserver/community_storage"] = "true";
        newChest.modData["svserver/storage_name"] = description;

        farm.Objects[tile] = newChest;
        _monitor.Log($"[Ownership] Placed {description} at ({tile.X}, {tile.Y})", LogLevel.Info);
    }

    private static bool Chest_CheckForAction_Prefix(Chest __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
    {
        if (justCheckingForActivity || who == null)
        {
            return true;
        }

        // Always allow Community Storage (Shared Chests)
        if (__instance.modData.ContainsKey("svserver/community_storage") ||
            __instance.modData.TryGetValue("svserver/community_storage", out var isComm) && isComm == "true")
        {
            return true;
        }

        // Host always has bypass
        if (who.IsMainPlayer)
        {
            return true;
        }

        // Check ownership
        long ownerId = __instance.owner.Value;
        if (ownerId == 0 && __instance.modData.TryGetValue("svserver/owner_id", out var ownerIdStr))
        {
            long.TryParse(ownerIdStr, out ownerId);
        }

        // Unowned chests (e.g. naturally generated or starter) are open
        if (ownerId == 0)
        {
            return true;
        }

        // Owner can access
        if (ownerId == who.UniqueMultiplayerID)
        {
            return true;
        }

        // Check if shared with this player
        if (__instance.modData.ContainsKey($"svserver/shared_with_{who.UniqueMultiplayerID}"))
        {
            return true;
        }

        // Access denied
        string ownerName = "Pemain Lain";
        if (__instance.modData.TryGetValue("svserver/owner_name", out var oName) && !string.IsNullOrEmpty(oName))
        {
            ownerName = oName;
        }
        else
        {
            var ownerFarmer = Game1.GetPlayer(ownerId);
            if (ownerFarmer != null && !string.IsNullOrEmpty(ownerFarmer.Name))
            {
                ownerName = ownerFarmer.Name;
            }
        }

        _instance?._helper.SendPrivateMessage(
            who.UniqueMultiplayerID,
            $"[Penyimpanan] Peti ini milik {ownerName}. Akses terkunci."
        );
        who.currentLocation?.playSound("doorClose");

        __result = false;
        return false;
    }

    private static void FarmAnimal_Pet_Postfix(FarmAnimal __instance, Farmer who)
    {
        if (__instance == null || who == null || _instance == null) return;

        long ownerId = __instance.ownerID.Value;
        if (ownerId != 0 && ownerId != who.UniqueMultiplayerID && !who.IsMainPlayer)
        {
            string ownerName = "Peternak Lain";
            if (__instance.modData.TryGetValue("svserver/owner_name", out var oName) && !string.IsNullOrEmpty(oName))
            {
                ownerName = oName;
            }
            else
            {
                var owner = Game1.GetPlayer(ownerId);
                if (owner != null && !string.IsNullOrEmpty(owner.Name))
                {
                    ownerName = owner.Name;
                }
            }

            _instance._helper.SendPrivateMessage(
                who.UniqueMultiplayerID,
                $"[{__instance.Name}] merasa senang dielus! (Ayam milik {ownerName})"
            );
        }
    }
}
