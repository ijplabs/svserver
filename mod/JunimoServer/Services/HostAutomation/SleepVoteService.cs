using System;
using System.Collections.Generic;
using System.Linq;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Lobby;
using JunimoServer.Util;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace JunimoServer.Services.HostAutomation;

/// <summary>
/// Service implementing sleep voting (!votesleep / !vs).
/// Allows the server day to advance when a configurable majority of players agrees to sleep,
/// preventing AFK or lagging players from stalling the server until 2:00 AM.
/// </summary>
public class SleepVoteService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly LobbyService _lobbyService;
    private readonly ChatCommandsService _chatCommandsService;

    private readonly HashSet<long> _voters = new();
    private bool _votePassedForTonight = false;

    public SleepVoteService(
        IModHelper helper,
        IMonitor monitor,
        LobbyService lobbyService,
        ChatCommandsService chatCommandsService
    ) : base(helper, monitor)
    {
        _helper = helper;
        _monitor = monitor;
        _lobbyService = lobbyService;
        _chatCommandsService = chatCommandsService;

        _helper.Events.GameLoop.DayStarted += OnDayStarted;
        _helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;

        RegisterCommands();
    }

    private void RegisterCommands()
    {
        if (!Env.SleepVoteEnabled)
        {
            _monitor.Log("[SleepVote] Sleep voting is disabled by configuration (SLEEP_VOTE_ENABLED=false)", LogLevel.Info);
            return;
        }

        _chatCommandsService.RegisterCommand(
            "votesleep",
            "Vote to advance to the next day (!votesleep, !votesleep status, !votesleep cancel)",
            HandleVoteSleep
        );

        _chatCommandsService.RegisterCommand(
            "vs",
            "Alias for !votesleep",
            HandleVoteSleep
        );

        _chatCommandsService.RegisterCommand(
            "sleepvote",
            "Alias for !votesleep",
            HandleVoteSleep
        );

        _monitor.Log($"[SleepVote] SleepVoteService loaded (Threshold: {Env.SleepVoteThresholdPercent}%)", LogLevel.Info);
    }

    private void HandleVoteSleep(string[] args, ReceivedMessage msg)
    {
        if (!Game1.hasLoadedGame || Game1.CurrentEvent != null)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[SleepVote] Game is not currently active.");
            return;
        }

        if (_lobbyService.IsPlayerUnauthenticated(msg.SourceFarmer))
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[SleepVote] You must authenticate first before voting.");
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

        if (Game1.timeOfDay < 1800)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[SleepVote] Sleep voting is only allowed in the evening (after 6:00 PM).");
            return;
        }

        if (_votePassedForTonight)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[SleepVote] Sleep vote has already passed for tonight! Transitioning soon.");
            return;
        }

        var voterFarmer = Game1.getFarmerMaybeOffline(msg.SourceFarmer);
        var voterName = voterFarmer?.Name ?? "Player";

        if (_voters.Contains(msg.SourceFarmer))
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, $"[SleepVote] You have already voted to sleep ({_voters.Count}/{GetRequiredVotes()}).");
            return;
        }

        _voters.Add(msg.SourceFarmer);
        int currentVotes = _voters.Count;
        int requiredVotes = GetRequiredVotes();

        _monitor.Log($"[SleepVote] {voterName} ({msg.SourceFarmer}) voted to sleep ({currentVotes}/{requiredVotes})", LogLevel.Info);
        _helper.SendPublicMessage($"[SleepVote] {voterName} voted to sleep ({currentVotes}/{requiredVotes}). Type !votesleep to agree.");

        CheckVoteThreshold();
    }

    private void CancelVote(long farmerId)
    {
        if (_votePassedForTonight)
        {
            _helper.SendPrivateMessage(farmerId, "[SleepVote] Cannot cancel vote; night transition is already underway.");
            return;
        }

        if (_voters.Remove(farmerId))
        {
            var farmer = Game1.getFarmerMaybeOffline(farmerId);
            var name = farmer?.Name ?? "Player";
            _helper.SendPublicMessage($"[SleepVote] {name} cancelled their sleep vote ({_voters.Count}/{GetRequiredVotes()}).");
        }
        else
        {
            _helper.SendPrivateMessage(farmerId, "[SleepVote] You haven't voted yet.");
        }
    }

    private void ShowStatus(long farmerId)
    {
        int required = GetRequiredVotes();
        var voterNames = _voters
            .Select(id => Game1.getFarmerMaybeOffline(id)?.Name ?? id.ToString())
            .ToList();

        string voterList = voterNames.Count > 0 ? string.Join(", ", voterNames) : "none";
        _helper.SendPrivateMessage(
            farmerId,
            $"[SleepVote] Status: {_voters.Count}/{required} votes (Threshold: {Env.SleepVoteThresholdPercent}%). Voters: {voterList}."
        );
    }

    private List<Farmer> GetEligibleFarmers()
    {
        var excluded = _lobbyService.GetExcludedPlayerIds();
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

        return Math.Max(1, (int)Math.Ceiling(eligible.Count * (Env.SleepVoteThresholdPercent / 100.0f)));
    }

    private void CheckVoteThreshold()
    {
        if (_votePassedForTonight)
        {
            return;
        }

        int required = GetRequiredVotes();
        if (_voters.Count >= required)
        {
            ExecuteSleepPass();
        }
    }

    private void ExecuteSleepPass()
    {
        _votePassedForTonight = true;
        int required = GetRequiredVotes();

        // Count how many players are in bed right now
        int inBedCount = Game1.otherFarmers.Values.Count(f => f.isInBed.Value);

        _monitor.Log($"[SleepVote] Vote passed ({_voters.Count}/{required}). In bed: {inBedCount}. Applying sleep exclusions.", LogLevel.Info);

        // Exclude all remote players who have NOT voted and are NOT in bed
        int excludedCount = 0;
        foreach (var farmer in Game1.otherFarmers.Values)
        {
            if (farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
                continue;

            if (!_voters.Contains(farmer.UniqueMultiplayerID) && !farmer.isInBed.Value)
            {
                _lobbyService.AddSleepVoteExclusion(farmer.UniqueMultiplayerID);
                excludedCount++;
            }
        }

        if (inBedCount > 0)
        {
            _helper.SendPublicMessage($"[SleepVote] Sleep vote passed ({_voters.Count}/{required})! Advancing to the next day...");
        }
        else
        {
            _helper.SendPublicMessage($"[SleepVote] Sleep vote passed ({_voters.Count}/{required})! Get into bed to advance the night.");
        }
    }

    private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Game1.hasLoadedGame)
        {
            return;
        }

        // Clean up disconnected farmers from voters
        _voters.RemoveWhere(id => !Game1.otherFarmers.ContainsKey(id));

        // When evening comes (>= 18:00), players who hop into bed implicitly vote for sleep
        if (Game1.timeOfDay >= 1800 && !_votePassedForTonight)
        {
            var inBedFarmers = Game1.otherFarmers.Values
                .Where(f => f.isInBed.Value && !_voters.Contains(f.UniqueMultiplayerID))
                .ToList();

            if (inBedFarmers.Count > 0)
            {
                foreach (var f in inBedFarmers)
                {
                    _voters.Add(f.UniqueMultiplayerID);
                    _monitor.Log($"[SleepVote] Registered in-bed farmer {f.Name} ({f.UniqueMultiplayerID}) as sleep voter", LogLevel.Trace);
                }
                CheckVoteThreshold();
            }
        }
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        _voters.Clear();
        _votePassedForTonight = false;
        _lobbyService.ClearSleepVoteExclusions();
        _monitor.Log("[SleepVote] New day started. Reset sleep votes and exclusions.", LogLevel.Trace);
    }
}
