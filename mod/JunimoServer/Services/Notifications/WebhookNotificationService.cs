using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JunimoServer.Services.Backup;
using JunimoServer.Services.CropTracker;
using JunimoServer.Util;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace JunimoServer.Services.Notifications;

/// <summary>
/// Service providing automated Discord webhook notifications for server events:
/// server boot/online, player joins/leaves, save backups, and daily 06:00 AM morning reports.
/// Strictly follows Zero Emoji Policy, utilizing clean typographic embeds.
/// </summary>
public class WebhookNotificationService : ModService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly BackupService _backupService;
    private readonly CropTrackerService _cropTrackerService;

    private readonly ConcurrentDictionary<long, string> _knownPlayerNames = new();

    public WebhookNotificationService(
        IModHelper helper,
        IMonitor monitor,
        BackupService backupService,
        CropTrackerService cropTrackerService
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _backupService = backupService;
        _cropTrackerService = cropTrackerService;

        _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        _helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
        _helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;
        _helper.Events.GameLoop.DayStarted += OnDayStarted;

        _backupService.OnBackupCreated += OnBackupCreated;

        if (Env.DiscordWebhookEnabled)
        {
            _monitor.Log("[Webhook] WebhookNotificationService active. Discord webhook configured.", LogLevel.Info);
        }
        else
        {
            _monitor.Log("[Webhook] WebhookNotificationService initialized in standby mode (DISCORD_WEBHOOK_URL unset).", LogLevel.Info);
        }
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        if (!Env.DiscordWebhookEnabled)
        {
            return;
        }

        string farmName = Game1.player?.farmName.Value ?? "Valley Farm";
        string season = Game1.currentSeason ?? "Spring";
        int day = Game1.dayOfMonth;
        int year = Game1.year;
        string inviteCode = string.IsNullOrWhiteSpace(InviteCodes.Joinable) ? "Direct IP / Steam" : InviteCodes.Describe();

        var fields = new List<DiscordEmbedField>
        {
            new("Farm", farmName, true),
            new("Date", $"Year {year}, {season} Day {day}", true),
            new("Invite Code", inviteCode, false)
        };

        SendEmbed(
            title: "[SERVER ONLINE] Stardew Valley Server Ready",
            description: "The dedicated host is initialized and accepting connections.",
            color: 0x10B981, // Emerald Green
            fields: fields
        );
    }

    private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
    {
        if (!Env.DiscordWebhookEnabled)
        {
            return;
        }

        long playerId = e.Peer.PlayerID;
        var farmer = Game1.getFarmer(playerId) ?? Game1.otherFarmers.Values.FirstOrDefault(f => f.UniqueMultiplayerID == playerId);
        string farmerName = !string.IsNullOrWhiteSpace(farmer?.Name) ? farmer.Name : $"Farmer #{playerId}";
        _knownPlayerNames[playerId] = farmerName;

        int onlineCount = Game1.getOnlineFarmers().Count(f => f.UniqueMultiplayerID != Game1.player?.UniqueMultiplayerID);
        int maxPlayers = Game1.netWorldState.Value?.CurrentPlayerLimit ?? 4;

        var fields = new List<DiscordEmbedField>
        {
            new("Farmer", farmerName, true),
            new("Online Players", $"{onlineCount} / {maxPlayers}", true)
        };

        SendEmbed(
            title: $"[PLAYER JOINED] {farmerName}",
            description: $"Farmer {farmerName} connected to the valley.",
            color: 0x3B82F6, // Blue
            fields: fields
        );
    }

    private void OnPeerDisconnected(object sender, PeerDisconnectedEventArgs e)
    {
        if (!Env.DiscordWebhookEnabled)
        {
            return;
        }

        long playerId = e.Peer.PlayerID;
        if (!_knownPlayerNames.TryGetValue(playerId, out var farmerName))
        {
            farmerName = $"Farmer #{playerId}";
        }
        _knownPlayerNames.TryRemove(playerId, out _);

        int onlineCount = Math.Max(0, Game1.getOnlineFarmers().Count(f => f.UniqueMultiplayerID != Game1.player?.UniqueMultiplayerID && f.UniqueMultiplayerID != playerId));
        int maxPlayers = Game1.netWorldState.Value?.CurrentPlayerLimit ?? 4;

        var fields = new List<DiscordEmbedField>
        {
            new("Farmer", farmerName, true),
            new("Online Players", $"{onlineCount} / {maxPlayers}", true)
        };

        SendEmbed(
            title: $"[PLAYER LEFT] {farmerName}",
            description: $"Farmer {farmerName} disconnected from the valley.",
            color: 0x64748B, // Slate
            fields: fields
        );
    }

    private void OnBackupCreated(BackupEntry entry)
    {
        if (!Env.DiscordWebhookEnabled)
        {
            return;
        }

        var fields = new List<DiscordEmbedField>
        {
            new("File Name", $"`{entry.FileName}`", false),
            new("Size", $"{entry.SizeBytes / 1024:N0} KB", true),
            new("Farm Date", $"Year {entry.Year}, {entry.Season} Day {entry.Day}", true)
        };

        SendEmbed(
            title: "[BACKUP CREATED] Save Archive Stored",
            description: "An automated snapshot of the world save has been created.",
            color: 0x8B5CF6, // Purple
            fields: fields
        );
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        if (!Env.DiscordWebhookEnabled)
        {
            return;
        }

        string todayWeather = GetCurrentWeather();
        string tomorrowWeather = GetTomorrowWeather();

        var cropSnap = _cropTrackerService?.GetLatestSnapshot();
        int totalCrops = cropSnap?.TotalCrops ?? 0;
        int readyCrops = cropSnap?.ReadyCount ?? 0;
        int wateredCrops = cropSnap?.WateredCount ?? 0;

        var fields = new List<DiscordEmbedField>
        {
            new("Weather Today", todayWeather, true),
            new("Weather Tomorrow", tomorrowWeather, true),
            new("Crops Status", $"Total: {totalCrops} | Ready to Harvest: {readyCrops} | Watered: {wateredCrops}", false)
        };

        SendEmbed(
            title: $"[MORNING REPORT] Year {Game1.year}, {Game1.currentSeason} Day {Game1.dayOfMonth}",
            description: "A new morning has begun in the valley.",
            color: 0xF59E0B, // Amber
            fields: fields
        );
    }

    private string GetCurrentWeather()
    {
        try
        {
            if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason))
            {
                return "Festival";
            }
            if (Game1.isLightningHere())
            {
                return "Thunderstorm";
            }
            if (Game1.isRainingHere())
            {
                return "Rain";
            }
            if (Game1.isSnowingHere())
            {
                return "Snow";
            }
            if (Game1.isDebrisWeatherHere())
            {
                return "Windy";
            }
            return "Sunny";
        }
        catch
        {
            return "Sunny";
        }
    }

    private string GetTomorrowWeather()
    {
        try
        {
            var raw = Game1.weatherForTomorrow?.ToLowerInvariant() ?? "";
            if (raw.Contains("green"))
            {
                return "Green Rain";
            }
            if (raw.Contains("storm") || raw.Contains("lightning"))
            {
                return "Thunderstorm";
            }
            if (raw.Contains("rain"))
            {
                return "Rain";
            }
            if (raw.Contains("snow"))
            {
                return "Snow";
            }
            if (raw.Contains("debris") || raw.Contains("wind"))
            {
                return "Windy";
            }
            if (raw.Contains("festival"))
            {
                return "Festival";
            }
            if (raw.Contains("sun"))
            {
                return "Sunny";
            }
            return string.IsNullOrEmpty(Game1.weatherForTomorrow) ? "Sunny" : Game1.weatherForTomorrow;
        }
        catch
        {
            return "Unknown";
        }
    }

    private void SendEmbed(string title, string description, int color, List<DiscordEmbedField> fields)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    username = "Junimo Server",
                    embeds = new[]
                    {
                        new
                        {
                            title,
                            description,
                            color,
                            fields,
                            footer = new
                            {
                                text = "JunimoServer Automated Notifications"
                            },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await HttpClient.PostAsync(Env.DiscordWebhookUrl, content);
                if (!res.IsSuccessStatusCode)
                {
                    _monitor.Log($"[Webhook] Discord webhook post failed: {(int)res.StatusCode} {res.ReasonPhrase}", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"[Webhook] Error sending Discord notification: {ex.Message}", LogLevel.Warn);
            }
        });
    }

    public class DiscordEmbedField
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("inline")]
        public bool Inline { get; set; }

        public DiscordEmbedField(string name, string value, bool @inline)
        {
            Name = name;
            Value = value;
            Inline = @inline;
        }
    }
}
