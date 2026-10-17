using System;
using System.Collections.Generic;
using System.Linq;
using JunimoServer.Util;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace JunimoServer.Services.HostAutomation;

/// <summary>
/// Monitors connected farmers for inactivity (AFK).
/// Warns and automatically kicks idle players during the late evening (>= 22:00)
/// or when other players are waiting in bed, ensuring the server clock and day transitions are never stalled.
/// </summary>
public class AfkWatchdogService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    private class PlayerActivityState
    {
        public Vector2 LastPosition;
        public string LastLocation = "";
        public DateTime LastActiveTime = DateTime.UtcNow;
        public bool WarningSent = false;
    }

    private readonly Dictionary<long, PlayerActivityState> _activity = new();

    public AfkWatchdogService(IModHelper helper, IMonitor monitor) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;

        _helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
        _monitor.Log($"[AfkWatchdog] AfkWatchdogService loaded (Enabled: {Env.AfkKickEnabled}, Timeout: {Env.AfkTimeoutMinutes}m, NightOnly: {Env.AfkNightOnly})", LogLevel.Info);
    }

    private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Env.AfkKickEnabled || !Game1.hasLoadedGame || Game1.server == null)
        {
            return;
        }

        // Freeze AFK watchdog while the world is paused so players on break are never penalized
        if (Game1.netWorldState?.Value?.IsPaused == true)
        {
            foreach (var state in _activity.Values)
            {
                state.LastActiveTime = DateTime.UtcNow;
            }
            return;
        }

        var now = DateTime.UtcNow;
        var activePlayerIds = new HashSet<long>();

        // Enforce conditions: either not night-only, or time >= 22:00, or someone is waiting in bed
        bool isNightOrBedWaiting = !Env.AfkNightOnly
            || Game1.timeOfDay >= 2200
            || (Game1.netReady != null && Game1.netReady.GetNumberReady("sleep") > 0);

        foreach (var farmer in Game1.otherFarmers.Values)
        {
            long id = farmer.UniqueMultiplayerID;
            if (id == Game1.player.UniqueMultiplayerID)
            {
                continue;
            }

            activePlayerIds.Add(id);

            if (!_activity.TryGetValue(id, out var state))
            {
                state = new PlayerActivityState
                {
                    LastPosition = farmer.Position,
                    LastLocation = farmer.currentLocation?.NameOrUniqueName ?? "",
                    LastActiveTime = now,
                    WarningSent = false
                };
                _activity[id] = state;
                continue;
            }

            string currentLocation = farmer.currentLocation?.NameOrUniqueName ?? "";
            float moveDistance = Vector2.Distance(farmer.Position, state.LastPosition);

            // Player moved or changed maps: update activity timestamp and clear warnings
            if (moveDistance > 4f || currentLocation != state.LastLocation)
            {
                state.LastPosition = farmer.Position;
                state.LastLocation = currentLocation;
                state.LastActiveTime = now;
                state.WarningSent = false;
                continue;
            }

            // Only enforce kick and warnings when night/waiting conditions are met
            if (!isNightOrBedWaiting)
            {
                continue;
            }

            var idleDuration = now - state.LastActiveTime;

            // Warning stage
            if (idleDuration >= TimeSpan.FromMinutes(Env.AfkWarningMinutes) && !state.WarningSent)
            {
                state.WarningSent = true;
                int remainingMinutes = Math.Max(1, Env.AfkTimeoutMinutes - Env.AfkWarningMinutes);
                _helper.SendPrivateMessage(
                    id,
                    $"[Server] Peringatan AFK: Anda tidak aktif selama {Env.AfkWarningMinutes} menit. Anda akan dikeluarkan dalam {remainingMinutes} menit agar waktu malam dapat berganti."
                );
                _monitor.Log($"[AfkWatchdog] Sent inactivity warning to {farmer.Name} ({id}). Idle for {idleDuration.TotalMinutes:F1}m", LogLevel.Info);
            }

            // Kick stage
            if (idleDuration >= TimeSpan.FromMinutes(Env.AfkTimeoutMinutes))
            {
                string farmerName = farmer.Name;
                _monitor.Log($"[AfkWatchdog] Kicking {farmerName} ({id}) for inactivity ({idleDuration.TotalMinutes:F1}m AFK)", LogLevel.Warn);
                _helper.SendPublicMessage($"[Server] {farmerName} dikeluarkan dari server karena tidak aktif ({Env.AfkTimeoutMinutes} menit AFK).");

                try
                {
                    Game1.server.kick(id);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[AfkWatchdog] Failed to kick {farmerName}: {ex.Message}", LogLevel.Error);
                }

                _activity.Remove(id);
            }
        }

        // Clean up disconnected players
        var toRemove = _activity.Keys.Where(id => !activePlayerIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            _activity.Remove(id);
        }
    }

    public object GetStatus()
    {
        var now = DateTime.UtcNow;
        var players = new List<object>();

        if (Game1.hasLoadedGame)
        {
            foreach (var kvp in _activity)
            {
                var farmer = Game1.getFarmerMaybeOffline(kvp.Key);
                var idleSeconds = (now - kvp.Value.LastActiveTime).TotalSeconds;
                players.Add(new
                {
                    farmerId = kvp.Key,
                    farmerName = farmer?.Name ?? "Unknown",
                    idleMinutes = Math.Round(idleSeconds / 60.0, 1),
                    warningSent = kvp.Value.WarningSent,
                    location = kvp.Value.LastLocation
                });
            }
        }

        return new
        {
            enabled = Env.AfkKickEnabled,
            warningMinutes = Env.AfkWarningMinutes,
            timeoutMinutes = Env.AfkTimeoutMinutes,
            nightOnly = Env.AfkNightOnly,
            trackedPlayers = players
        };
    }
}
