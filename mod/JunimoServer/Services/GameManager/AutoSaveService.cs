using System;
using System.Collections.Generic;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Roles;
using JunimoServer.Util;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace JunimoServer.Services.GameManager;

/// <summary>
/// Service providing periodic mid-day world snapshots and graceful shutdown saves.
/// Prevents loss of progress (characters created, professions chosen, farm changes)
/// when the server container is redeployed or restarted before the in-game night sleep.
/// </summary>
public class AutoSaveService : ModService
{
    private static AutoSaveService? _instance;
    public static AutoSaveService? Instance => _instance;

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ChatCommandsService _chatCommandsService;
    private readonly RoleService _roleService;

    private readonly HashSet<long> _savedCustomizedFarmers = new();
    private DateTime _lastAutoSaveUtc = DateTime.UtcNow;
    private bool _isSaving = false;
    private bool _pendingSave = false;
    private string _pendingReason = "";

    public AutoSaveService(
        IModHelper helper,
        IMonitor monitor,
        ChatCommandsService chatCommandsService,
        RoleService roleService
    ) : base(helper, monitor)
    {
        _instance = this;
        _helper = helper;
        _monitor = monitor;
        _chatCommandsService = chatCommandsService;
        _roleService = roleService;

        _helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        _helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        _helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;

        if (Env.AutoSaveOnShutdown)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        RegisterCommands();
        _monitor.Log(
            $"[AutoSave] AutoSaveService initialized (Interval: {Env.AutoSaveIntervalMinutes}m, OnDisconnect: {Env.AutoSaveOnDisconnect}, OnShutdown: {Env.AutoSaveOnShutdown})",
            LogLevel.Info
        );
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "save",
            "Menyimpan snapshot dunia game ke disk secara instan (!save)",
            (args, msg) =>
            {
                if (!_roleService.IsPlayerAdmin(msg.SourceFarmer))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "[red][AutoSave] Perintah ini membutuhkan akses Admin.");
                    return;
                }

                var senderFarmer = Game1.GetPlayer(msg.SourceFarmer);
                string senderName = senderFarmer?.Name ?? msg.SourceFarmer.ToString();
                _helper.SendPublicMessage("[yellow][AutoSave] Menyimpan snapshot permainan ke disk...");
                TriggerSave($"Chat command !save dari {senderName}");
            },
            requiresAdmin: true
        );

        _helper.ConsoleCommands.Add(
            "world-save",
            "Saves an immediate mid-day game snapshot to disk.",
            (command, args) =>
            {
                if (Game1.gameMode != 3 || !Game1.IsMasterGame)
                {
                    _monitor.Log("[AutoSave] Cannot save: not in an active master game.", LogLevel.Warn);
                    return;
                }

                _monitor.Log("[AutoSave] Manual console world-save requested...", LogLevel.Info);
                ExecuteSave("Console command 'world-save'");
            }
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _lastAutoSaveUtc = DateTime.UtcNow;
        _savedCustomizedFarmers.Clear();

        var farmhandData = Game1.netWorldState?.Value?.farmhandData;
        if (farmhandData != null)
        {
            foreach (var kvp in farmhandData.FieldDict)
            {
                var farmhand = kvp.Value.Value;
                if (farmhand != null && farmhand.isCustomized.Value)
                {
                    _savedCustomizedFarmers.Add(farmhand.UniqueMultiplayerID);
                }
            }
        }

        _monitor.Log($"[AutoSave] Initialized tracking with {_savedCustomizedFarmers.Count} existing customized farmer(s).", LogLevel.Info);
    }

    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (Game1.gameMode != 3 || !Game1.IsMasterGame)
        {
            return;
        }

        // 1. Detect new customized farmers
        if (Env.AutoSaveOnCustomization)
        {
            var farmhandData = Game1.netWorldState?.Value?.farmhandData;
            if (farmhandData != null)
            {
                foreach (var kvp in farmhandData.FieldDict)
                {
                    var farmhand = kvp.Value.Value;
                    if (farmhand != null && farmhand.isCustomized.Value && !_savedCustomizedFarmers.Contains(farmhand.UniqueMultiplayerID))
                    {
                        var name = farmhand.Name?.Trim();
                        bool isPlaceholder = string.IsNullOrEmpty(name)
                            || name.Equals("New Farmer", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("Farmhand", StringComparison.OrdinalIgnoreCase);

                        if (!isPlaceholder)
                        {
                            _savedCustomizedFarmers.Add(farmhand.UniqueMultiplayerID);
                            TriggerSave($"Petani baru selesai kustomisasi: '{farmhand.Name}' ({farmhand.UniqueMultiplayerID})");
                            break;
                        }
                    }
                }
            }
        }

        // 2. Periodic interval check
        if (Env.AutoSaveIntervalMinutes > 0)
        {
            if (DateTime.UtcNow - _lastAutoSaveUtc >= TimeSpan.FromMinutes(Env.AutoSaveIntervalMinutes))
            {
                _lastAutoSaveUtc = DateTime.UtcNow;
                TriggerSave($"Interval periodik ({Env.AutoSaveIntervalMinutes} menit)");
            }
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (_pendingSave && !_isSaving)
        {
            _pendingSave = false;
            string reason = _pendingReason;
            _pendingReason = "";
            ExecuteSave(reason);
        }
    }

    private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        if (!Env.AutoSaveOnDisconnect)
        {
            return;
        }

        if (Game1.gameMode != 3 || !Game1.IsMasterGame)
        {
            return;
        }

        TriggerSave($"Pemain disconnect ({e.Peer.PlayerID})");
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (!Env.AutoSaveOnShutdown)
        {
            return;
        }

        try
        {
            if (Game1.gameMode == 3 && Game1.IsMasterGame)
            {
                _monitor.Log("[AutoSave] Graceful shutdown dipicu (SIGTERM / ProcessExit). Menyimpan snapshot ke disk...", LogLevel.Info);
                if (SaveNow.TrySave(_helper, out var error))
                {
                    _monitor.Log("[AutoSave] Graceful shutdown auto-save BERHASIL.", LogLevel.Info);
                }
                else
                {
                    _monitor.Log($"[AutoSave] Graceful shutdown auto-save gagal: {error}", LogLevel.Warn);
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[AutoSave] Exception saat graceful shutdown save: {ex.Message}", LogLevel.Error);
        }
    }

    public void TriggerSave(string reason)
    {
        _pendingSave = true;
        _pendingReason = reason;
        _monitor.Log($"[AutoSave] World save dijadwalkan pada tick berikutnya. Alasan: {reason}", LogLevel.Info);
    }

    private void ExecuteSave(string reason)
    {
        if (_isSaving)
        {
            return;
        }

        if (Game1.gameMode != 3 || !Game1.IsMasterGame)
        {
            return;
        }

        _isSaving = true;
        try
        {
            if (SaveNow.TrySave(_helper, out var error))
            {
                _lastAutoSaveUtc = DateTime.UtcNow;
                _monitor.Log(
                    $"[AutoSave] Snapshot dunia berhasil disimpan ke disk! [Tahun {Game1.year}, {Game1.currentSeason} Tanggal {Game1.dayOfMonth}, Jam {Game1.timeOfDay}]. Alasan: {reason}",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor.Log($"[AutoSave] Gagal menyimpan snapshot dunia: {error}", LogLevel.Warn);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[AutoSave] Error saat menjalankan auto-save: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}
