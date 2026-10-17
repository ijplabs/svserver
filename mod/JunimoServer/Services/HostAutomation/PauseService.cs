using System;
using System.Collections.Generic;
using System.Linq;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Lobby;
using JunimoServer.Services.Roles;
using JunimoServer.Util;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace JunimoServer.Services.HostAutomation;

/// <summary>
/// Service implementing manual pausing and vote-pausing (!pause, !resume, !unpause).
/// Allows players to pause the world when taking a break (e.g., restroom, urgent matter)
/// without the game clock running, day passing, or AFK watchdog kicking them.
/// </summary>
public class PauseService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ChatCommandsService _chatCommandsService;
    private readonly RoleService _roleService;
    private readonly LobbyService _lobbyService;

    public event Action<bool, string, string>? OnPauseStateChanged;

    public bool IsManuallyPaused { get; private set; }
    public string PauseReason { get; private set; } = string.Empty;
    public string PausedBy { get; private set; } = string.Empty;
    public DateTime? PausedAtUtc { get; private set; }
    public TimeSpan AutoResumeTimeout { get; set; } = TimeSpan.FromMinutes(15);

    // Vote state
    private bool _voteInProgress;
    private DateTime _voteStartTime;
    private string _currentVoteReason = string.Empty;
    private string _voteInitiatorName = string.Empty;
    private long _voteInitiatorId;
    private readonly HashSet<long> _pauseVoters = new();
    private readonly HashSet<long> _resumeVoters = new();
    private bool _warnedAutoResume;

    public PauseService(
        IModHelper helper,
        IMonitor monitor,
        ChatCommandsService chatCommandsService,
        RoleService roleService,
        LobbyService lobbyService
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _chatCommandsService = chatCommandsService;
        _roleService = roleService;
        _lobbyService = lobbyService;

        _helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;

        RegisterCommands();
        _monitor.Log("[Pause] PauseService initialized (AutoResumeTimeout: 15m)", LogLevel.Info);
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "pause",
            "Pause the game world (!pause [alasan], !pause status, !pause cancel)",
            HandlePauseChatCommand
        );

        _chatCommandsService.RegisterCommand(
            "resume",
            "Resume the paused game world (!resume)",
            HandleResumeChatCommand
        );

        _chatCommandsService.RegisterCommand(
            "unpause",
            "Alias for !resume",
            HandleResumeChatCommand
        );
    }

    private List<Farmer> GetEligibleFarmers()
    {
        var excluded = _lobbyService.GetUnauthenticatedPlayerIds();
        return Game1.otherFarmers.Values
            .Where(f => !excluded.Contains(f.UniqueMultiplayerID) && f.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
            .ToList();
    }

    private int GetRequiredVotes()
    {
        var eligible = GetEligibleFarmers();
        if (eligible.Count <= 1)
        {
            return 1;
        }

        // 50% majority
        return Math.Max(1, (eligible.Count + 1) / 2);
    }

    private void HandlePauseChatCommand(string[] args, ReceivedMessage msg)
    {
        if (!Game1.hasLoadedGame)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Pause] Game save belum dimuat.");
            return;
        }

        if (_lobbyService.IsPlayerUnauthenticated(msg.SourceFarmer))
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Pause] Anda harus login terlebih dahulu.");
            return;
        }

        if (args.Length > 0 && args[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ShowStatus(msg.SourceFarmer);
            return;
        }

        if (args.Length > 0 && args[0].Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            CancelVote(msg.SourceFarmer);
            return;
        }

        if (IsManuallyPaused)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, $"[Pause] Server sudah dalam kondisi dijeda oleh {PausedBy} (Alasan: \"{PauseReason}\"). Ketik !resume untuk lanjut.");
            return;
        }

        var farmer = Game1.getFarmerMaybeOffline(msg.SourceFarmer);
        string farmerName = farmer?.Name ?? "Player";
        bool isAdmin = _roleService.IsPlayerAdmin(msg.SourceFarmer);

        string reason = args.Length > 0 ? string.Join(" ", args) : "AFK / Keperluan Singkat";

        RequestPause(farmerName, msg.SourceFarmer, reason, isAdmin);
    }

    private void HandleResumeChatCommand(string[] args, ReceivedMessage msg)
    {
        if (!Game1.hasLoadedGame)
        {
            return;
        }

        if (_lobbyService.IsPlayerUnauthenticated(msg.SourceFarmer))
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Pause] Anda harus login terlebih dahulu.");
            return;
        }

        if (!IsManuallyPaused)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Pause] Permainan saat ini tidak sedang dijeda.");
            return;
        }

        var farmer = Game1.getFarmerMaybeOffline(msg.SourceFarmer);
        string farmerName = farmer?.Name ?? "Player";
        bool isAdmin = _roleService.IsPlayerAdmin(msg.SourceFarmer);

        RequestResume(farmerName, msg.SourceFarmer, isAdmin);
    }

    public bool RequestPause(string requesterName, long requesterId, string reason = "AFK / Keperluan Singkat", bool isAdmin = false)
    {
        if (IsManuallyPaused)
        {
            return false;
        }

        var eligible = GetEligibleFarmers();

        // Admin override or solo player in server -> instant pause!
        if (isAdmin || eligible.Count <= 1)
        {
            PauseGame(requesterName, reason);
            return true;
        }

        // Multiplayer vote pause
        if (_voteInProgress)
        {
            if (_pauseVoters.Contains(requesterId))
            {
                _helper.SendPrivateMessage(requesterId, $"[Pause] Anda sudah memberikan suara jeda ({_pauseVoters.Count}/{GetRequiredVotes()}).");
                return true;
            }

            _pauseVoters.Add(requesterId);
            int needed = GetRequiredVotes();

            if (_pauseVoters.Count >= needed)
            {
                _voteInProgress = false;
                PauseGame(_voteInitiatorName, _currentVoteReason);
            }
            else
            {
                _helper.SendPublicMessage($"[Pause] {requesterName} menyetujui jeda permainan ({_pauseVoters.Count}/{needed} suara). Ketik !pause untuk setuju.");
            }
            return true;
        }

        // Start new vote
        _voteInProgress = true;
        _voteStartTime = DateTime.UtcNow;
        _currentVoteReason = reason;
        _voteInitiatorName = requesterName;
        _voteInitiatorId = requesterId;
        _pauseVoters.Clear();
        _pauseVoters.Add(requesterId);

        int requiredVotes = GetRequiredVotes();
        if (_pauseVoters.Count >= requiredVotes)
        {
            _voteInProgress = false;
            PauseGame(requesterName, reason);
        }
        else
        {
            _helper.SendPublicMessage($"[Pause] {requesterName} meminta jeda permainan: \"{reason}\". Ketik !pause untuk menyetujui ({_pauseVoters.Count}/{requiredVotes} suara dibutuhkan). Batas voting 60 detik.");
        }

        return true;
    }

    public bool RequestResume(string requesterName, long requesterId, bool isAdmin = false)
    {
        if (!IsManuallyPaused)
        {
            return false;
        }

        var eligible = GetEligibleFarmers();

        // Admin, initiator, or solo player -> instant resume!
        if (isAdmin || requesterName.Equals(PausedBy, StringComparison.OrdinalIgnoreCase) || eligible.Count <= 1)
        {
            ResumeGame(requesterName);
            return true;
        }

        // Multiplayer resume voting
        _resumeVoters.Add(requesterId);
        int needed = GetRequiredVotes();

        if (_resumeVoters.Count >= needed)
        {
            ResumeGame(requesterName);
        }
        else
        {
            _helper.SendPublicMessage($"[Pause] {requesterName} ingin melanjutkan permainan ({_resumeVoters.Count}/{needed} suara). Ketik !resume jika siap.");
        }

        return true;
    }

    public void PauseGame(string initiatedBy, string reason)
    {
        IsManuallyPaused = true;
        PausedBy = initiatedBy;
        PauseReason = reason;
        PausedAtUtc = DateTime.UtcNow;
        _warnedAutoResume = false;
        _voteInProgress = false;
        _pauseVoters.Clear();
        _resumeVoters.Clear();

        if (Game1.netWorldState?.Value != null)
        {
            Game1.netWorldState.Value.IsPaused = true;
        }

        _helper.SendPublicMessage($"[Pause] Permainan DIJEDA oleh {initiatedBy}: \"{reason}\". Waktu game berhenti (Batas maks: 15 menit). Ketik !resume jika sudah kembali.");
        _monitor.Log($"[Pause] World manually paused by {initiatedBy} (Reason: '{reason}')", LogLevel.Info);
        OnPauseStateChanged?.Invoke(true, initiatedBy, reason);
    }

    public void ResumeGame(string initiatedBy)
    {
        IsManuallyPaused = false;
        string prevBy = PausedBy;
        PausedBy = string.Empty;
        PauseReason = string.Empty;
        PausedAtUtc = null;
        _warnedAutoResume = false;
        _voteInProgress = false;
        _pauseVoters.Clear();
        _resumeVoters.Clear();

        if (Game1.netWorldState?.Value != null)
        {
            Game1.netWorldState.Value.IsPaused = false;
        }

        _helper.SendPublicMessage($"[Pause] Permainan DILANJUTKAN kembali oleh {initiatedBy}. Waktu game berjalan normal kembali!");
        _monitor.Log($"[Pause] World resumed by {initiatedBy} (previously paused by {prevBy})", LogLevel.Info);
        OnPauseStateChanged?.Invoke(false, initiatedBy, string.Empty);
    }

    private void CancelVote(long farmerId)
    {
        if (!_voteInProgress)
        {
            _helper.SendPrivateMessage(farmerId, "[Pause] Tidak ada pemungutan suara jeda yang sedang berlangsung.");
            return;
        }

        bool isAdmin = _roleService.IsPlayerAdmin(farmerId);
        if (!isAdmin && farmerId != _voteInitiatorId)
        {
            _helper.SendPrivateMessage(farmerId, "[Pause] Hanya pembuat vote atau Admin yang dapat membatalkan voting.");
            return;
        }

        _voteInProgress = false;
        _pauseVoters.Clear();
        _helper.SendPublicMessage($"[Pause] Pemungutan suara jeda dibatalkan.");
    }

    private void ShowStatus(long farmerId)
    {
        if (IsManuallyPaused)
        {
            int remainingSecs = GetRemainingSeconds();
            int mins = remainingSecs / 60;
            int secs = remainingSecs % 60;
            _helper.SendPrivateMessage(farmerId, $"[Pause] Status: DIJEDA oleh {PausedBy} (Alasan: \"{PauseReason}\"). Sisa waktu aman: {mins}m {secs}s. Ketik !resume untuk lanjut.");
        }
        else if (_voteInProgress)
        {
            _helper.SendPrivateMessage(farmerId, $"[Pause] Status: VOTING AKTIF oleh {_voteInitiatorName} ({_pauseVoters.Count}/{GetRequiredVotes()} suara). Alasan: \"{_currentVoteReason}\". Ketik !pause untuk setuju.");
        }
        else
        {
            _helper.SendPrivateMessage(farmerId, "[Pause] Status: Permainan sedang berjalan normal. Ketik !pause [alasan] untuk meminta jeda.");
        }
    }

    public int GetRemainingSeconds()
    {
        if (!IsManuallyPaused || !PausedAtUtc.HasValue) return 0;
        var elapsed = DateTime.UtcNow - PausedAtUtc.Value;
        var remaining = AutoResumeTimeout - elapsed;
        return (int)Math.Max(0, remaining.TotalSeconds);
    }

    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Game1.hasLoadedGame) return;

        // While manually paused, keep the netWorldState pause flag asserted
        if (IsManuallyPaused)
        {
            if (Game1.netWorldState?.Value != null && !Game1.netWorldState.Value.IsPaused)
            {
                Game1.netWorldState.Value.IsPaused = true;
            }

            // Check auto-resume safety timeout
            if (PausedAtUtc.HasValue)
            {
                var elapsed = DateTime.UtcNow - PausedAtUtc.Value;
                if (elapsed >= AutoResumeTimeout)
                {
                    _helper.SendPublicMessage("[Pause] Batas waktu jeda permainan (15 menit) telah tercapai. Melanjutkan game secara otomatis.");
                    ResumeGame("System (Auto-Resume Timeout)");
                }
                else if (!_warnedAutoResume && (AutoResumeTimeout - elapsed).TotalSeconds <= 60)
                {
                    _warnedAutoResume = true;
                    _helper.SendPublicMessage("[Pause] Peringatan: Batas waktu jeda tersisa 1 menit. Server akan otomatis melanjutkan permainan.");
                }
            }
            return;
        }

        // Handle vote timeout
        if (_voteInProgress)
        {
            if ((DateTime.UtcNow - _voteStartTime).TotalSeconds >= 60)
            {
                _voteInProgress = false;
                _pauseVoters.Clear();
                _helper.SendPublicMessage("[Pause] Batas waktu 60 detik pemungutan suara jeda telah habis. Permainan tetap dilanjutkan.");
            }
        }
    }
}
