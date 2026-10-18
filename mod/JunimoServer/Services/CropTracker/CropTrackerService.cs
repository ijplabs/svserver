using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JunimoServer.Services.Redis;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace JunimoServer.Services.CropTracker;

public class CropTrackerService : ModService
{
    private readonly IRedisService _redis;
    private readonly IMonitor _monitor;
    private readonly IModHelper _helper;

    private FarmCropsSnapshot _currentSnapshot = new()
    {
        CapturedAtUtc = DateTime.UtcNow
    };

    public CropTrackerService(
        IModHelper helper,
        IMonitor monitor,
        IRedisService redis
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _redis = redis;

        _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        _helper.Events.GameLoop.DayStarted += OnDayStarted;
        _helper.Events.GameLoop.TimeChanged += OnTimeChanged;
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        RefreshSnapshot();
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        RefreshSnapshot();
    }

    private void OnTimeChanged(object sender, TimeChangedEventArgs e)
    {
        // Re-evaluate on 10-minute in-game ticks (e.g., watering, growth)
        RefreshSnapshot();
    }

    public FarmCropsSnapshot GetLatestSnapshot()
    {
        return _currentSnapshot;
    }

    public void RefreshSnapshot()
    {
        try
        {
            var crops = new List<CropTileState>();
            int readyCount = 0;
            int wateredCount = 0;
            int unwateredCount = 0;
            int deadCount = 0;

            Utility.ForEachLocation(location =>
            {
                var locName = location.NameOrUniqueName;

                // 1. Terrain features
                foreach (var feature in location.terrainFeatures.Values)
                {
                    if (feature is not HoeDirt dirt || dirt.crop == null)
                    {
                        continue;
                    }

                    // If an indoor pot sits on top of this dirt, the pot owns the tile
                    if (location.Objects.TryGetValue(dirt.Tile, out var objAtTile) && objAtTile is IndoorPot)
                    {
                        continue;
                    }

                    var cropState = ExtractCropState(locName, dirt.Tile, dirt);
                    if (cropState != null)
                    {
                        crops.Add(cropState);
                        if (cropState.IsDead)
                        {
                            deadCount++;
                        }
                        else if (cropState.IsReadyForHarvest)
                        {
                            readyCount++;
                        }

                        if (cropState.IsWatered)
                        {
                            wateredCount++;
                        }
                        else
                        {
                            unwateredCount++;
                        }
                    }
                }

                // 2. Indoor pots
                foreach (var pot in location.Objects.Values.OfType<IndoorPot>())
                {
                    var dirt = pot.hoeDirt.Value;
                    if (dirt?.crop == null)
                    {
                        continue;
                    }

                    var cropState = ExtractCropState(locName, pot.TileLocation, dirt);
                    if (cropState != null)
                    {
                        crops.Add(cropState);
                        if (cropState.IsDead)
                        {
                            deadCount++;
                        }
                        else if (cropState.IsReadyForHarvest)
                        {
                            readyCount++;
                        }

                        if (cropState.IsWatered)
                        {
                            wateredCount++;
                        }
                        else
                        {
                            unwateredCount++;
                        }
                    }
                }

                return true;
            });

            var snapshot = new FarmCropsSnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                TotalCrops = crops.Count,
                ReadyCount = readyCount,
                WateredCount = wateredCount,
                UnwateredCount = unwateredCount,
                DeadCount = deadCount,
                Crops = crops
            };

            _currentSnapshot = snapshot;

            // Non-blocking background sync to Redis and PubSub
            _ = Task.Run(async () =>
            {
                try
                {
                    await _redis.SetAsync("cache:farm:crops", snapshot, TimeSpan.FromHours(24));
                    await _redis.PublishAsync("sv:events", JsonConvert.SerializeObject(new
                    {
                        type = "crops_updated",
                        total = snapshot.TotalCrops,
                        ready = snapshot.ReadyCount,
                        watered = snapshot.WateredCount,
                        timestamp = DateTime.UtcNow
                    }));
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[CropTracker] Background Redis sync error: {ex.Message}", LogLevel.Debug);
                }
            });
        }
        catch (Exception ex)
        {
            _monitor.Log($"[CropTracker] Error updating crops snapshot: {ex}", LogLevel.Warn);
        }
    }

    private CropTileState? ExtractCropState(string location, Vector2 tile, HoeDirt dirt)
    {
        var crop = dirt.crop;
        if (crop == null)
        {
            return null;
        }

        bool isDead = crop.dead.Value;
        bool isWatered = dirt.state.Value == 1; // HoeDirt.watered
        int currentPhase = crop.currentPhase.Value;
        int totalPhases = crop.phaseDays?.Count ?? 0;
        int regrowDays = crop.GetData()?.RegrowDays ?? -1;

        // Calculate readiness and days until harvest
        bool isReady = false;
        int daysUntilHarvest = 0;

        if (isDead)
        {
            daysUntilHarvest = -1;
        }
        else if (crop.phaseDays != null && totalPhases > 0)
        {
            if (currentPhase >= totalPhases - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value == 0))
            {
                isReady = true;
                daysUntilHarvest = 0;
            }
            else
            {
                int remaining = Math.Max(0, crop.phaseDays[Math.Min(currentPhase, totalPhases - 1)] - crop.dayOfCurrentPhase.Value);
                for (int i = currentPhase + 1; i < totalPhases - 1; i++)
                {
                    remaining += crop.phaseDays[i];
                }
                daysUntilHarvest = remaining;
            }
        }

        string harvestItemId = crop.indexOfHarvest.Value ?? string.Empty;
        if (string.IsNullOrEmpty(harvestItemId))
        {
            try
            {
                var cropData = crop.GetData();
                if (cropData != null && !string.IsNullOrEmpty(cropData.HarvestItemId))
                {
                    harvestItemId = cropData.HarvestItemId;
                }
            }
            catch { }
        }

        string harvestItemName = string.Empty;
        if (!string.IsNullOrEmpty(harvestItemId))
        {
            var itemData = ItemRegistry.GetData(harvestItemId) ?? ItemRegistry.GetData("(O)" + harvestItemId);
            harvestItemName = itemData?.DisplayName ?? harvestItemId;
        }

        if (string.IsNullOrEmpty(harvestItemName) || int.TryParse(harvestItemName, out _))
        {
            var seedId = crop.netSeedIndex.Value;
            if (!string.IsNullOrEmpty(seedId))
            {
                var seedData = ItemRegistry.GetData(seedId) ?? ItemRegistry.GetData("(O)" + seedId);
                if (seedData != null && !string.IsNullOrEmpty(seedData.DisplayName))
                {
                    harvestItemName = seedData.DisplayName.Replace(" Seeds", "").Replace(" Starter", "").Trim();
                }
            }
        }

        string? fertilizerId = string.IsNullOrEmpty(dirt.fertilizer.Value) ? null : dirt.fertilizer.Value;
        string? fertilizerName = null;
        if (fertilizerId != null)
        {
            var fertData = ItemRegistry.GetData(fertilizerId);
            fertilizerName = fertData?.DisplayName ?? fertilizerId;
        }

        long? ownerId = null;
        string? ownerName = null;
        if (dirt.modData.TryGetValue("svserver/owner_id", out var oId) && long.TryParse(oId, out var parsedOId))
        {
            ownerId = parsedOId;
        }
        if (dirt.modData.TryGetValue("svserver/owner_name", out var oName) && !string.IsNullOrEmpty(oName))
        {
            ownerName = oName;
        }

        return new CropTileState
        {
            X = (int)tile.X,
            Y = (int)tile.Y,
            Location = location,
            CropId = crop.netSeedIndex.Value ?? string.Empty,
            HarvestItemId = harvestItemId,
            HarvestItemName = harvestItemName,
            CurrentPhase = currentPhase,
            TotalPhases = totalPhases,
            DaysUntilHarvest = daysUntilHarvest,
            IsWatered = isWatered,
            FertilizerId = fertilizerId,
            FertilizerName = fertilizerName,
            IsReadyForHarvest = isReady,
            IsDead = isDead,
            RegrowDays = regrowDays,
            OwnerId = ownerId,
            OwnerName = ownerName,
        };
    }
}
