using System;
using System.Collections.Generic;

namespace JunimoServer.Services.CropTracker;

public class CropTileState
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Location { get; set; } = string.Empty;
    public string CropId { get; set; } = string.Empty;
    public string HarvestItemId { get; set; } = string.Empty;
    public string HarvestItemName { get; set; } = string.Empty;
    public int CurrentPhase { get; set; }
    public int TotalPhases { get; set; }
    public int DaysUntilHarvest { get; set; }
    public bool IsWatered { get; set; }
    public string? FertilizerId { get; set; }
    public string? FertilizerName { get; set; }
    public bool IsReadyForHarvest { get; set; }
    public bool IsDead { get; set; }
    public int RegrowDays { get; set; }
}

public class FarmCropsSnapshot
{
    public DateTime CapturedAtUtc { get; set; }
    public int TotalCrops { get; set; }
    public int ReadyCount { get; set; }
    public int WateredCount { get; set; }
    public int UnwateredCount { get; set; }
    public int DeadCount { get; set; }
    public List<CropTileState> Crops { get; set; } = new();
}
