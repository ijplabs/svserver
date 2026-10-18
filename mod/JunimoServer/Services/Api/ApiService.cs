using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JunimoServer.Services.Auth;
using JunimoServer.Services.CabinManager;
using JunimoServer.Services.Commands;
using JunimoServer.Services.GameCreator;
using JunimoServer.Services.GameManager;
using JunimoServer.Services.GameThread;
using JunimoServer.Services.PasswordProtection;
using System.IO;
using JunimoServer.Services.Lobby;
using JunimoServer.Services.PersistentOption;
using JunimoServer.Services.Roles;
using JunimoServer.Services.ServerOptim;
using JunimoServer.Services.Settings;
using JunimoServer.Services.SteamGameServer;
using JunimoServer.Shared;
using JunimoServer.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;

namespace JunimoServer.Services.Api;

#region API Models

/// <summary>
/// Server status including player count and game state.
/// </summary>
public class ServerStatus
{
    /// <summary>Number of connected players.</summary>
    public int PlayerCount { get; set; }

    /// <summary>Maximum allowed players.</summary>
    public int MaxPlayers { get; set; }

    /// <summary>
    /// The invite code to hand to players (S-prefixed, universal). Present whenever a Galaxy lobby
    /// exists — GOG clients join it immediately; Steam clients join once <see cref="SteamRelayReady"/>
    /// is true. Null when no lobby exists yet. The G-code is never exposed.
    /// </summary>
    public string? InviteCode { get; set; }

    /// <summary>
    /// Whether Steam clients can join using the code yet (the Galaxy lobby carries the relay stamp).
    /// Does NOT gate whether the code is shown.
    /// </summary>
    public bool SteamRelayReady { get; set; }

    /// <summary>Galaxy lobby state: "connected" | "recovering" | "down". Null when Steam auth is not configured.</summary>
    public string? GalaxyLobbyState { get; set; }

    /// <summary>Steam GameServer session state: "connected" | "lost".</summary>
    public string SteamSessionState { get; set; } = "lost";

    /// <summary>Sidecar auth/token health: "unknown" (no poll has reached the sidecar yet) | "ok" | "expiring" | "unavailable". Null when Steam auth is not configured.</summary>
    public string? AuthReadiness { get; set; }

    /// <summary>
    /// Whether <see cref="InviteCode"/> is usable and by whom, derived from the signals above:
    /// "ready" | "steamRelayPending" | "reconnecting" | "starting" | "steamSessionDown" |
    /// "inviteUnavailable". Displays map this one field to text; the raw signals stay for diagnostics.
    /// </summary>
    public string ConnectionStatusCode { get; set; } = "starting";

    /// <summary>Server mod version.</summary>
    public string ServerVersion { get; set; } = "";

    /// <summary>Stardew Valley game version (Game1.version, e.g. "1.6.15").</summary>
    public string GameVersion { get; set; } = "";

    /// <summary>Whether the server is online and hosting.</summary>
    public bool IsOnline { get; set; }

    /// <summary>Whether the server is ready to accept new connections (false during day transitions, saving, etc.).</summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Whether the day-transition machinery (newDaySync barrier + save + map load) has finished and
    /// the host is back in normal play — UNLIKE <see cref="IsReady"/>, this ignores post-transition
    /// festival/wedding activity. Lets a post-day-change settle proceed once the transition itself is
    /// done instead of dead-waiting through a festival or wedding. Equals <see cref="IsReady"/> on an
    /// ordinary day. See GameManagerService.IsDayTransitionComplete.
    /// </summary>
    public bool DayTransitionComplete { get; set; }

    /// <summary>ISO 8601 timestamp of last update.</summary>
    public string LastUpdated { get; set; } = "";

    /// <summary>Name of the farm.</summary>
    public string FarmName { get; set; } = "";

    /// <summary>Display name of this deployment (SERVER_NAME). Empty when unset.</summary>
    public string ServerName { get; set; } = "";

    /// <summary>ISO 8601 UTC time the server process started, or null if not yet known.</summary>
    public string? StartedAtUtc { get; set; }

    /// <summary>Current day of the month (1-28).</summary>
    public int Day { get; set; }

    /// <summary>Current season (spring, summer, fall, winter).</summary>
    public string Season { get; set; } = "";

    /// <summary>Current year.</summary>
    public int Year { get; set; }

    /// <summary>Today's weather condition (e.g. Sunny, Rain, Thunderstorm, Snow, Windy, Festival).</summary>
    public string Weather { get; set; } = "Sunny";

    /// <summary>Tomorrow's weather forecast.</summary>
    public string WeatherTomorrow { get; set; } = "Sunny";

    /// <summary>Current time in 24-hour format (e.g., 1430 = 2:30 PM).</summary>
    public int TimeOfDay { get; set; }

    /// <summary>Farm type key as returned by Game1.GetFarmTypeKey() (a vanilla name, or the farm Id for Data/AdditionalFarms farms).</summary>
    public string FarmTypeKey { get; set; } = "";

    /// <summary>Whether the game clock is currently paused (no players connected, or time not passing).</summary>
    public bool IsPaused { get; set; }

    /// <summary>Whether the world was paused manually by players or sysadmin.</summary>
    public bool IsManuallyPaused { get; set; }

    /// <summary>Reason for manual pause.</summary>
    public string PauseReason { get; set; } = "";

    /// <summary>Player or admin who initiated manual pause.</summary>
    public string PausedBy { get; set; } = "";

    /// <summary>Remaining seconds before automatic resume timeout.</summary>
    public int PauseRemainingSeconds { get; set; }

    /// <summary>Measured game ticks per second over at least the last 30 seconds (see /stats for the instant value and target).</summary>
    public double Tps { get; set; }

    /// <summary>
    /// Monotonic snapshot sequence used by /wait/* long-poll endpoints to
    /// detect a changed snapshot. Test clients pass this back as
    /// <c>?since=N</c> so the server only returns when a newer snapshot
    /// satisfies the requested filters.
    /// </summary>
    public long Sequence { get; set; }
}

/// <summary>
/// Weather information for the current day and tomorrow's forecast.
/// </summary>
public class WeatherResponse
{
    public string Today { get; set; } = "Sunny";
    public string Tomorrow { get; set; } = "Sunny";
    public int Day { get; set; }
    public string Season { get; set; } = "spring";
    public int Year { get; set; }
    public bool IsRaining { get; set; }
    public bool IsLightning { get; set; }
    public bool IsSnowing { get; set; }
    public bool IsDebris { get; set; }
    public bool IsFestival { get; set; }
}

/// <summary>
/// Information about a connected player.
/// </summary>
public class PlayerInfo
{
    /// <summary>Unique player ID.</summary>
    public long Id { get; set; }

    /// <summary>Player's display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether the player is currently online.</summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Unique name of the location the player is in, or is arriving in (the replicated
    /// destination, ahead of the netcode interpolation window). Empty when unresolved.
    /// </summary>
    public string Location { get; set; } = "";
}

/// <summary>
/// Response containing list of connected players.
/// </summary>
public class PlayersResponse
{
    /// <summary>List of connected players.</summary>
    public List<PlayerInfo> Players { get; set; } = new();

    /// <summary>
    /// Monotonic snapshot sequence (same field as <see cref="ServerStatus.Sequence"/>).
    /// Test clients pass this back as <c>?since=N</c> on /wait/players to wait
    /// for a newer snapshot.
    /// </summary>
    public long Sequence { get; set; }
}

/// <summary>
/// Ground-truth server state for E2E test failure diagnosis. Dumps the
/// internal fields that the periodic <c>GameStateSnapshot</c> aggregates
/// from, plus the game-engine state usually hidden behind reflection
/// (<c>netReady</c>, <c>newDaySync</c>, <c>activeClickableMenu</c>).
/// Every sub-field is best-effort: if a reflection read fails, its entry
/// becomes a <c>{"error":"..."}</c> object and the rest of the response
/// still returns 200. Callers use this from failure-path handlers so a
/// timeout becomes a diagnostic signal rather than a mystery.
/// </summary>
public class DiagnosticsStateResponse
{
    public string CapturedAt { get; set; } = "";
    public long[] OtherFarmerUids { get; set; } = System.Array.Empty<long>();
    public int OnlineFarmerCount { get; set; }
    public List<ReadyCheckState> NetReady { get; set; } = new();
    public NewDaySyncState NewDaySync { get; set; } = new();
    public string? ActiveClickableMenu { get; set; }
    public int TimeOfDay { get; set; }
    public int DayOfMonth { get; set; }
    public string Season { get; set; } = "";
    public int Year { get; set; }
    public int GameMode { get; set; }
    public bool? IsGameAvailable { get; set; }
    public long? LastTickMs { get; set; }
    public double AvgGameThreadWaitMs { get; set; }

    /// <summary>Live cabin ownership read from <c>Game1.getFarm().buildings</c>.</summary>
    public List<DiagnosticsCabinState> Cabins { get; set; } = new();

    /// <summary>Farmhand slots from <c>Game1.netWorldState.Value.farmhandData</c>.</summary>
    public List<DiagnosticsFarmhandState> FarmhandData { get; set; } = new();

    /// <summary>UMIs in <c>Multiplayer.disconnectingFarmers</c> (mid-disconnect).</summary>
    public long[] DisconnectingFarmers { get; set; } = System.Array.Empty<long>();

    // ── Save-import assertion probes ──
    // The post-swap Server-host FarmHouse must hold nothing but the default bed after the
    // contents/NPC move.
    /// <summary>Placed-object count in the host FarmHouse (must be 0 after a swap import).</summary>
    public int FarmHouseObjectCount { get; set; }

    /// <summary>Furniture count in the host FarmHouse (must be 1, the default bed, after a swap import).</summary>
    public int FarmHouseFurnitureCount { get; set; }

    /// <summary>Whether <c>FarmHouse.GetPlayerBed()</c> finds a bed in the host FarmHouse.</summary>
    public bool FarmHouseHasPlayerBed { get; set; }

    /// <summary>The host's current location (<c>Game1.currentLocation.NameOrUniqueName</c>).</summary>
    public string HostLocation { get; set; } = "";

    /// <summary>Fridge-item count in the host FarmHouse (must be 0 after a swap import).</summary>
    public int FarmHouseFridgeItemCount { get; set; }

    /// <summary>Object count in the master's "Cellar"-1 (must be 0 after a swap import moved the
    /// former owner's casks into their reassigned cellar).</summary>
    public int MasterCellarObjectCount { get; set; }

    // Master-gated world-state probes: prove the blank Server master carries the copied
    // master-gated fields (caveChoice/friendshipData/stats/mail) yet NOT the cleared relationship
    // field (spouse). MasterPlayer IS Game1.player on this server (multiplayerMode=2).
    /// <summary>Whether <c>MasterPlayer.mailReceived</c> contains the flag named in the
    /// <c>?masterFlag=</c> query (null if no flag was queried). Boolean, not the flag list.</summary>
    public bool? MasterHasFlag { get; set; }

    /// <summary>Whether <c>MasterPlayer.eventsSeen</c> contains the id named in the <c>?masterEvent=</c>
    /// query (null if none queried). eventsSeen is a distinct collection from mailReceived.</summary>
    public bool? MasterHasEvent { get; set; }

    /// <summary><c>MasterPlayer.caveChoice</c> (Test 7b — non-mail master-gated field carry).</summary>
    public int MasterCaveChoice { get; set; }

    /// <summary><c>MasterPlayer.friendshipData[?masterFriendKey].Points</c> — the SPECIFIC keyed NPC's
    /// points (Test 7c — shadow-pacifism gate carry), or null when no <c>?masterFriendKey=</c> was
    /// queried (nullable for parity with <see cref="MasterHasFlag"/>: "not queried" ≠ "genuinely 0").</summary>
    public int? MasterShadowFriendshipPoints { get; set; }

    /// <summary><c>MasterPlayer.stats.DaysPlayed</c> (Test 7c — same-day-reconnect gate carry).</summary>
    public int MasterDaysPlayed { get; set; }

    /// <summary>Whether the blank Server master has an NPC spouse (Test 8 — must be FALSE after a
    /// swap; the clone-blank clears <c>&lt;spouse&gt;</c> on the master only).</summary>
    public bool MasterHasSpouse { get; set; }

    /// <summary>The master farmer's name. After a swap it is "Server" (the fresh blank host); after an
    /// as-is import it is the save's original owner name — the as-is test asserts the latter, so it can
    /// distinguish "owner preserved as host" from "a blank Server was wrongly installed."</summary>
    public string MasterName { get; set; } = "";

    /// <summary>Count of save-import finalize success-path runs since process start. The single-shot
    /// test asserts this stays 1 across two reloads (a re-fire would bump it), proving the finalizer
    /// cleared its intent and did NOT re-run.</summary>
    public int SaveImportFinalizeCount { get; set; }

    /// <summary>Fields that failed to be read. Empty on fully-successful dumps.</summary>
    public List<string> FailedFields { get; set; } = new();
}

public class DiagnosticsCabinState
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string IndoorsName { get; set; } = "";
    public long OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public bool OwnerIsCustomized { get; set; }

    /// <summary>Whether the owner has a platform ID (Steam/GOG) stamped; true with
    /// OwnerIsCustomized=false is the abandoned-claim state. Resolved via cabin.owner, which
    /// yields the live otherFarmers copy while the owner is connected (so the in-flight userID
    /// stamp is visible here before disconnect persists it to farmhandData).</summary>
    public bool OwnerHasUserId { get; set; }

    /// <summary>Whether the cabin owner (its farmhand) has a server-side ownership record
    /// (recorded at the join gate's approve moment or by a save-import bind).</summary>
    public bool OwnerHasOwner { get; set; }

    /// <summary>Platform tag of the cabin owner's ownership record ("steam"/"galaxy"), or ""
    /// when unowned.</summary>
    public string OwnerPlatform { get; set; } = "";

    public string HomeLocationOfOwner { get; set; } = "";
    public bool FarmhandReferenceDefined { get; set; }
    public long FarmhandReferenceUid { get; set; }

    /// <summary>Count of placed objects (chests/machines) in the cabin interior. Used by the
    /// save-import contents-move test to assert the owner's chests moved into the cabin. Count
    /// only, not contents.</summary>
    public int ObjectCount { get; set; }

    /// <summary>Count of items in the cabin fridge (save-import contents move).</summary>
    public int FridgeItemCount { get; set; }

    /// <summary>Count of <c>Pet</c> NPCs in the cabin interior, so the save-import pet-relocation test
    /// can assert the PET reached the cabin, not merely "some NPC."</summary>
    public int PetCount { get; set; }

    /// <summary>Object count in the cellar the engine assigned to this cabin's owner (resolved via
    /// <c>Cabin.GetCellarName()</c>), so the save-import cellar-move test can assert the former owner's
    /// casks reached their cellar. 0 when the owner has no assigned cellar.</summary>
    public int CellarObjectCount { get; set; }
}

public class DiagnosticsFarmhandState
{
    public long UniqueMultiplayerId { get; set; }
    public string Name { get; set; } = "";
    public bool IsCustomized { get; set; }
    public string HomeLocation { get; set; } = "";
    public string LastSleepLocation { get; set; } = "";

    /// <summary>Whether a platform ID (Steam/GOG) is stamped on this slot; true with
    /// IsCustomized=false is the abandoned-claim state.</summary>
    public bool HasUserId { get; set; }

    /// <summary>Whether this slot has a server-side ownership record (recorded at the join
    /// gate's approve moment or by a save-import bind).</summary>
    public bool HasOwner { get; set; }

    /// <summary>Platform tag of the ownership record ("steam"/"galaxy"), or "" when unowned.</summary>
    public string OwnerPlatform { get; set; } = "";

    /// <summary>Whether the slot is operator-released: claimable by any transport, first
    /// successful claim becomes the owner.</summary>
    public bool Released { get; set; }
}

public class ReadyCheckState
{
    public string Id { get; set; } = "";
    public int NumberReady { get; set; }
    public int NumberRequired { get; set; }
    public bool IsReady { get; set; }
    public bool IsLocked { get; set; }
}

public class NewDaySyncState
{
    public bool HasStarted { get; set; }
    public bool HasFinished { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Health check response.
/// </summary>
public class HealthResponse
{
    /// <summary>Health status ("ok" or "degraded").</summary>
    public string Status { get; set; } = "ok";

    /// <summary>ISO 8601 timestamp.</summary>
    public string Timestamp { get; set; } = "";

    /// <summary>Milliseconds since the last game thread tick, or null if no tick recorded yet.</summary>
    public long? LastTickMs { get; set; }

    /// <summary>Number of actions queued for execution on the game thread.</summary>
    public int PendingActions { get; set; }

    /// <summary>Whether the game server reports itself as available for connections.</summary>
    public bool? GameAvailable { get; set; }

    /// <summary>
    /// Cumulative game-thread tick count since process start. Used by the
    /// test harness's WaitUntilGameReady wait strategy to verify the game
    /// loop is actually running before declaring the container ready.
    /// </summary>
    public long TickCount { get; set; }

    /// <summary>
    /// True when the game thread hasn't ticked recently (lastTickMs above
    /// <c>HealthFrozenThresholdMs</c>). Mirrors the existing
    /// <see cref="Status"/> "degraded" semantic in a boolean form so tests
    /// don't have to string-compare. Used by WaitUntilGameReady to reject
    /// a server whose HTTP listener is up but whose game loop has stalled.
    /// </summary>
    public bool IsFrozen { get; set; }

    // ── Connectivity / joinability summary (body-only) ──
    // These let a monitor tell "alive" from "actually joinable". They do NOT affect Status or the
    // HTTP status code: /health must stay 200 for an alive server, or the Docker HEALTHCHECK +
    // restart:unless-stopped + the deploy `grep unhealthy` gate would restart it on a transient
    // Galaxy/Steam blip.

    /// <summary>Steam GameServer session state: "connected" | "lost".</summary>
    public string SteamSessionState { get; set; } = "lost";

    /// <summary>Galaxy lobby state: "connected" | "recovering" | "down". Null when Steam auth is not configured.</summary>
    public string? GalaxyLobbyState { get; set; }

    /// <summary>Whether Steam clients can join using the code yet (the Galaxy lobby carries the relay stamp).</summary>
    public bool SteamRelayReady { get; set; }

    /// <summary>Whether an invite code is currently exposed (a Galaxy lobby exists).</summary>
    public bool InviteCodePresent { get; set; }

    /// <summary>Sidecar auth/token health: "unknown" (no poll has reached the sidecar yet) | "ok" | "expiring" | "unavailable". Null when Steam auth is not configured.</summary>
    public string? AuthReadiness { get; set; }
}

/// <summary>
/// Performance stats for the game process.
/// </summary>
public class StatsResponse
{
    /// <summary>Current frames per second.</summary>
    public double Fps { get; set; }

    /// <summary>Current game ticks per second.</summary>
    public double Tps { get; set; }

    /// <summary>Target TPS for the server.</summary>
    public int TargetTps { get; set; } = Env.ServerTps;

    /// <summary>Average tick duration in milliseconds (rolling 60-tick window).</summary>
    public double AvgTickMs { get; set; }

    /// <summary>Process memory usage in MB.</summary>
    public double MemoryMb { get; set; }

    /// <summary>Cumulative GC generation 0 collection count.</summary>
    public int GcGen0 { get; set; }

    /// <summary>Cumulative GC generation 1 collection count.</summary>
    public int GcGen1 { get; set; }

    /// <summary>Cumulative GC generation 2 collection count.</summary>
    public int GcGen2 { get; set; }

    /// <summary>Number of actions queued for the game thread.</summary>
    public int PendingActions { get; set; }

    /// <summary>Rolling average game thread wait time in milliseconds (60-sample window).</summary>
    public double GameThreadWaitMs { get; set; }

    /// <summary>ISO 8601 UTC time the mod started, or null if not yet available.</summary>
    public string? StartedAtUtc { get; set; }

    /// <summary>Seconds the mod has been running, or null if the start time isn't available.</summary>
    public long? UptimeSeconds { get; set; }
}

/// <summary>
/// Response for farmhand operations.
/// </summary>
public class FarmhandResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Information about a farmhand slot.
/// </summary>
public class FarmhandInfo
{
    /// <summary>Unique multiplayer ID.</summary>
    public long Id { get; set; }

    /// <summary>Farmhand's display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether the farmhand has been customized.</summary>
    public bool IsCustomized { get; set; }
}

/// <summary>
/// Response containing list of farmhands.
/// </summary>
public class FarmhandsResponse
{
    /// <summary>List of farmhand slots.</summary>
    public List<FarmhandInfo> Farmhands { get; set; } = new();

    /// <summary>
    /// Monotonic snapshot sequence (same field as <see cref="ServerStatus.Sequence"/>).
    /// Test clients pass this back as <c>?since=N</c> on /wait/farmhands to wait
    /// for a newer snapshot.
    /// </summary>
    public long Sequence { get; set; }
}

/// <summary>
/// Current rendering status.
/// </summary>
public class RenderingStatus
{
    /// <summary>Render rate: 0 = disabled, N &gt; 0 = enabled at N fps.</summary>
    public int Fps { get; set; }
}

/// <summary>
/// Response from screenshot capture.
/// </summary>
public class ScreenshotResponse
{
    /// <summary>Whether the capture succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Base64-encoded PNG image data.</summary>
    public string? Base64Png { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Error message if capture failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response from a set-render-rate operation.
/// </summary>
public class RenderingSetResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Render rate after the operation: 0 = disabled, N &gt; 0 = at N fps.</summary>
    public int Fps { get; set; }

    /// <summary>Render rate before the operation.</summary>
    public int PreviousFps { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response from time set operation.
/// </summary>
public class TimeSetResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Current time of day after the operation.</summary>
    public int TimeOfDay { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response from clock speed adjustment.
/// </summary>
public class ClockSpeedResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>The applied multiplier.</summary>
    public double Multiplier { get; set; }

    /// <summary>The effective milliseconds per game minute after adjustment.</summary>
    public int EffectiveMs { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response from role grant operation.
/// </summary>
public class RoleGrantResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>The player's unique multiplayer ID.</summary>
    public long PlayerId { get; set; }

    /// <summary>The player's display name.</summary>
    public string? PlayerName { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

public class CharacterSkillInfo
{
    public int Level { get; set; }
    public int Experience { get; set; }
}

public class InventoryItemInfo
{
    public int SlotIndex { get; set; }
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Stack { get; set; }
    public int Quality { get; set; }
    public string CategoryName { get; set; } = "";
}

public class EquipmentItemInfo
{
    public string Slot { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class FarmerProfileResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string FarmName { get; set; } = "";
    public string FavoriteThing { get; set; } = "";
    public bool IsMale { get; set; }
    public int Money { get; set; }
    public int ClubCoins { get; set; }
    public int QiGems { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public float Stamina { get; set; }
    public int MaxStamina { get; set; }
    public string CurrentLocation { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public Dictionary<string, CharacterSkillInfo> Skills { get; set; } = new();
    public List<InventoryItemInfo> Inventory { get; set; } = new();
    public List<EquipmentItemInfo> Equipment { get; set; } = new();
}

public class VillagerFriendshipDto
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Points { get; set; }
    public int Hearts { get; set; }
    public int MaxHearts { get; set; } = 10;
    public int GiftsThisWeek { get; set; }
    public int MaxGiftsPerWeek { get; set; } = 2;
    public bool GiftGivenToday { get; set; }
    public bool TalkedToToday { get; set; }
    public string Status { get; set; } = "Friendly";
    public string BirthdaySeason { get; set; } = "";
    public int BirthdayDay { get; set; }
    public bool IsBirthdayToday { get; set; }
    public bool IsDatable { get; set; }
    public string Gender { get; set; } = "";
}

public class FarmerSocialResponse
{
    public long FarmerId { get; set; }
    public string FarmerName { get; set; } = "";
    public int TotalVillagersMet { get; set; }
    public int MaxHeartsAchieved { get; set; }
    public List<VillagerFriendshipDto> Villagers { get; set; } = new();
}

public class QuestObjectiveDto
{
    public string Description { get; set; } = "";
    public int CurrentCount { get; set; }
    public int MaxCount { get; set; }
    public bool IsComplete { get; set; }
}

public class QuestDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public int RewardMoney { get; set; }
    public int DaysLeft { get; set; }
    public bool IsDailyQuest { get; set; }
    public bool IsComplete { get; set; }
    public string QuestType { get; set; } = "Standard";
}

public class SpecialOrderDto
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string OrderType { get; set; } = "";
    public string Requester { get; set; } = "";
    public int DaysLeft { get; set; }
    public List<QuestObjectiveDto> Objectives { get; set; } = new();
    public bool IsComplete { get; set; }
}

public class FarmerQuestsResponse
{
    public long FarmerId { get; set; }
    public string FarmerName { get; set; } = "";
    public List<QuestDto> ActiveQuests { get; set; } = new();
    public List<SpecialOrderDto> SpecialOrders { get; set; } = new();
}

public class BundleIngredientDto
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int Quality { get; set; }
    public bool IsFulfilled { get; set; }
}

public class BundleDto
{
    public int BundleIndex { get; set; }
    public string Name { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int RoomIndex { get; set; }
    public int RequiredItemsCount { get; set; }
    public int FulfilledCount { get; set; }
    public bool IsComplete { get; set; }
    public string RewardName { get; set; } = "";
    public int RewardCount { get; set; } = 1;
    public List<BundleIngredientDto> Ingredients { get; set; } = new();
}

public class CommunityRoomDto
{
    public int RoomIndex { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Reward { get; set; } = "";
    public bool IsComplete { get; set; }
    public int TotalBundles { get; set; }
    public int CompletedBundles { get; set; }
    public List<BundleDto> Bundles { get; set; } = new();
}

public class JojaProjectDto
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; }
    public bool IsComplete { get; set; }
}

public class CommunityCenterResponse
{
    public bool IsComplete { get; set; }
    public bool IsJojaRoute { get; set; }
    public int TotalBundles { get; set; }
    public int CompletedBundles { get; set; }
    public List<CommunityRoomDto> Rooms { get; set; } = new();
    public List<JojaProjectDto> JojaProjects { get; set; } = new();
}

public class PlayerMapPin
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Zone { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float PixelX { get; set; }
    public float PixelY { get; set; }
    public int FacingDirection { get; set; }
    public bool IsOnline { get; set; }
}

public class FarmBuildingPin
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int TilesWide { get; set; }
    public int TilesHigh { get; set; }
    public bool IsCabin { get; set; }
    public string? OwnerName { get; set; }
    public long? OwnerId { get; set; }
    public bool IsLobby { get; set; }
    public int AnimalCount { get; set; }
}

public class MapTrackingResponse
{
    public string FarmType { get; set; } = "Standard";
    public int FarmWidth { get; set; } = 80;
    public int FarmHeight { get; set; } = 65;
    public List<PlayerMapPin> Players { get; set; } = new();
    public List<FarmBuildingPin> Buildings { get; set; } = new();
    public Dictionary<string, int> CropCountsByLocation { get; set; } = new();
}


/// <summary>
/// Current server settings (from server-settings.json).
/// </summary>
public class SettingsResponse
{
    /// <summary>Game creation settings (immutable after game created).</summary>
    public GameSettingsInfo Game { get; set; } = new();

    /// <summary>Server runtime settings (applied on every startup).</summary>
    public ServerRuntimeSettingsInfo Server { get; set; } = new();
}

/// <summary>
/// Game creation settings.
/// </summary>
public class GameSettingsInfo
{
    /// <summary>Farm name.</summary>
    public string FarmName { get; set; } = "";

    public FarmTypeSetting FarmType { get; set; } = FarmTypeSetting.Default;

    /// <summary>Profit margin multiplier (1.0 = normal).</summary>
    public float ProfitMargin { get; set; }

    /// <summary>Number of starting cabins.</summary>
    public int StartingCabins { get; set; }

    /// <summary>Spawn monsters at night: "true", "false", or "auto".</summary>
    public string SpawnMonstersAtNight { get; set; } = "";
}

/// <summary>
/// Server runtime settings.
/// </summary>
public class ServerRuntimeSettingsInfo
{
    /// <summary>Maximum number of players allowed.</summary>
    public int MaxPlayers { get; set; }

    /// <summary>Cabin strategy: CabinStack, FarmhouseStack, or None.</summary>
    public string CabinStrategy { get; set; } = "";

    /// <summary>Whether each player has a separate wallet.</summary>
    public bool SeparateWallets { get; set; }

    /// <summary>How to handle existing visible cabins: KeepExisting or MoveToStack.</summary>
    public string ExistingCabinBehavior { get; set; } = "";

    /// <summary>Whether players may relocate their cabin via the !cabin command.</summary>
    public bool AllowCabinRelocation { get; set; }
}

/// <summary>
/// Cabin state snapshot.
/// </summary>
public class CabinsResponse
{
    /// <summary>Active cabin strategy.</summary>
    public string Strategy { get; set; } = "";

    /// <summary>Total number of cabins on the farm.</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of cabins assigned to players.</summary>
    public int AssignedCount { get; set; }

    /// <summary>Number of cabins available for new players.</summary>
    public int AvailableCount { get; set; }

    /// <summary>Individual cabin details.</summary>
    public List<CabinInfo> Cabins { get; set; } = new();

    /// <summary>
    /// UniqueMultiplayerIDs of players who have an explicitly-placed cabin position
    /// recorded (via the cabin command). These cabins are exempt from the
    /// MoveToStack / strategy-migration sweep. Cleared when the farmhand is deleted.
    /// </summary>
    public List<long> SavedPositionPlayerIds { get; set; } = new();

    /// <summary>
    /// Active staged strategy migration ('cabins migrate'), or null when none is staged.
    /// </summary>
    public CabinMigrationInfo? Migration { get; set; }

    /// <summary>
    /// The CabinStack shared stack spot (the tile each player's ghost cabin renders at),
    /// or null when the active strategy is not CabinStack.
    /// </summary>
    public StackSpotInfo? StackSpot { get; set; }

    /// <summary>
    /// The main farmhouse's front-door entry tile (<c>Game1.getFarm().GetMainFarmHouseEntry()</c>);
    /// null when no game is loaded. Under FarmhouseStack a stacked cabin's exit warp lands here, so
    /// tests can assert the exit-warp repoint against the authoritative tile, not just any on-map
    /// target.
    /// </summary>
    public TilePoint? MainFarmHouseEntry { get; set; }
}

/// <summary>A tile coordinate on <see cref="CabinsResponse"/>.</summary>
public class TilePoint
{
    /// <summary>Tile X.</summary>
    public int X { get; set; }

    /// <summary>Tile Y.</summary>
    public int Y { get; set; }
}

/// <summary>
/// The CabinStack shared stack spot on <see cref="CabinsResponse"/>.
/// </summary>
public class StackSpotInfo
{
    /// <summary>Tile X of the spot.</summary>
    public int TileX { get; set; }

    /// <summary>Tile Y of the spot.</summary>
    public int TileY { get; set; }

    /// <summary>True when an admin-set override is active (vs the map default).</summary>
    public bool IsOverride { get; set; }

    /// <summary>True when a cabin footprint at the spot currently fails placement validation.</summary>
    public bool IsObstructed { get; set; }
}

/// <summary>
/// Staged strategy-migration status. Present on <see cref="CabinsResponse"/> only while a
/// 'cabins migrate' staging is active; the old strategy stays live until commit.
/// </summary>
public class CabinMigrationInfo
{
    /// <summary>The strategy the migration started from (still live during staging).</summary>
    public string FromStrategy { get; set; } = "";

    /// <summary>The strategy the migration commits to.</summary>
    public string ToStrategy { get; set; } = "";

    /// <summary>Number of staging placements done so far (auto + manual).</summary>
    public int PlacedCount { get; set; }

    /// <summary>
    /// Placements still required before commit, computed live (hidden non-lobby cabins for
    /// a migration to None; the unresolved shared-stack spot for FarmhouseStack→CabinStack).
    /// </summary>
    public int RemainingCount { get; set; }
}

/// <summary>
/// Information about a single cabin.
/// </summary>
public class CabinInfo
{
    /// <summary>Tile X position on the farm.</summary>
    public int TileX { get; set; }

    /// <summary>Tile Y position on the farm.</summary>
    public int TileY { get; set; }

    /// <summary>Whether the cabin is at a hidden off-map location.</summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// The cabin type indicating its purpose/management mode.
    /// Values: "Normal", "CabinStack", "FarmhouseStack", "Lobby"
    /// </summary>
    public string Type { get; set; } = "Normal";

    /// <summary>Owner's multiplayer ID (0 if unassigned).</summary>
    public long OwnerId { get; set; }

    /// <summary>Owner's display name (empty if unassigned).</summary>
    public string OwnerName { get; set; } = "";

    /// <summary>Whether the cabin has an assigned owner.</summary>
    public bool IsAssigned { get; set; }
}

/// <summary>
/// Authentication/password protection status.
/// </summary>
public class AuthStatusResponse
{
    /// <summary>Whether password protection is enabled on the server.</summary>
    public bool Enabled { get; set; }

    /// <summary>Number of players currently authenticated.</summary>
    public int AuthenticatedCount { get; set; }

    /// <summary>Number of players waiting to authenticate (in lobby).</summary>
    public int PendingCount { get; set; }

    /// <summary>Authentication timeout in seconds (0 = disabled).</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>Maximum failed login attempts before kick.</summary>
    public int MaxAttempts { get; set; }
}

/// <summary>
/// Response from auth timeout update operation.
/// </summary>
public class AuthTimeoutResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Current timeout in seconds after the operation.</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>Previous timeout in seconds before the change.</summary>
    public int PreviousTimeoutSeconds { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Request body for POST /newgame.
/// </summary>
public class NewGameRequest
{
    /// <summary>Absent = use the server's configured farm type.</summary>
    public FarmTypeSetting? FarmType { get; set; }

    /// <summary>Farm name.</summary>
    public string? FarmName { get; set; }

    /// <summary>Number of starting cabins.</summary>
    public int? StartingCabins { get; set; }

    /// <summary>Cabin strategy: CabinStack, FarmhouseStack, or None.</summary>
    public string? CabinStrategy { get; set; }

    /// <summary>Maximum number of players.</summary>
    public int? MaxPlayers { get; set; }

    /// <summary>Whether to allow direct IP connections.</summary>
    public bool? AllowIpConnections { get; set; }

    /// <summary>Profit margin multiplier (1.0 = normal).</summary>
    public float? ProfitMargin { get; set; }

    /// <summary>Whether each player has a separate wallet.</summary>
    public bool? SeparateWallets { get; set; }

    /// <summary>Whether players may relocate their cabin via the !cabin command.</summary>
    public bool? AllowCabinRelocation { get; set; }
}

/// <summary>
/// Response from POST /newgame.
/// </summary>
public class NewGameResponse
{
    /// <summary>Whether the new game was created successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Response from POST /reload.
/// </summary>
public class ReloadResponse
{
    /// <summary>Whether the world reloaded successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string? Message { get; set; }

    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Request body for POST /api/auth/register.
/// </summary>
public class RegisterRequest
{
    /// <summary>6-digit OTP code generated in-game via !webpass.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Desired web dashboard password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Request body for POST /api/auth/login.
/// </summary>
public class LoginRequest
{
    /// <summary>Farmer character name or superadmin username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password.</summary>
    public string Password { get; set; } = string.Empty;
}

#endregion

/// <summary>
/// HTTP API service using HttpListener with NSwag OpenAPI support.
/// </summary>
public partial class ApiService : ModService
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private bool _isRunning;
    private string? _openApiSpec;

    private readonly ServerSettingsLoader _settings;
    private readonly PersistentOptions _persistentOptions;
    private readonly CabinManagerService _cabinManager;
    private readonly RoleService _roleService;
    private readonly PasswordProtectionService? _passwordProtectionService;
    private readonly SaveImport.SaveImportService _saveImportService;
    private readonly NpcIntegrity.NpcSpriteIntegrityService _npcSpriteIntegrity;
    private readonly Auth.FarmhandOwnershipService _farmhandOwnership;

    // WebSocket client management
    private readonly List<WebSocket> _wsClients = new();
    private readonly object _wsLock = new();
    private Timer? _wsCleanupTimer;

    /// <summary>
    /// Game-thread marshaling for mutating handlers (see <see cref="RunOnGameThreadAsync"/>);
    /// the game-state snapshot is refreshed after every drain that ran an action.
    /// </summary>
    private readonly GameThreadDispatcher _dispatcher;

    /// <summary>
    /// Ticks timestamp of the last OnUpdateTicked call, used by /health to detect game thread stalls.
    /// Updated via Interlocked.Exchange so it can be read from HTTP threads without locking.
    /// </summary>
    private long _lastTickTimestamp;

    // TCS that /wait/health long-poll waiters await. Rotated on every
    // OnUnvalidatedUpdateTicked, after the _lastTickTimestamp write.
    // Distinct from _snapshotChanged: snapshot publishes are gated to 1 Hz
    // and skipped during Game1.newDay, both of which /health must remain
    // immune to (it reports game-thread liveness, not game-state freshness).
    // Rotation order matches PublishSnapshot at :1012-1016 — install fresh
    // TCS first, then signal the old one — to avoid the missed-wakeup race.
    private TaskCompletionSource _lastTickChanged = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    /// <summary>
    /// Cumulative game-thread tick counter. Used by /health (and the test-side
    /// WaitUntilGameReady wait strategy) to verify the game loop is actually
    /// running, not just that the HTTP listener accepts connections. Distinct
    /// from <see cref="_tickCount"/> (resets every second for TPS calculation).
    /// </summary>
    private long _totalTickCount;

    // ── Performance metrics for /stats endpoint ──
    private double _lastTickMs;
    private readonly RollingAverage _avgTickMs = new(60);
    private readonly Stopwatch _tickStopwatch = new();
    private int _frameCount;
    private double _currentFps;
    private DateTime _lastFpsUpdate = DateTime.UtcNow;
    private int _tickCount;
    private double _currentTps;
    private DateTime _lastTpsUpdate = DateTime.UtcNow;

    // For /status; the window matches the public widget's 30-second poll.
    /// <summary>
    /// Ticks and seconds per sample interval, averaged over the same window: their ratio is the
    /// tick rate over the last 30 intervals (30+ seconds), so a long stall weighs by its duration.
    /// </summary>
    private readonly RollingAverage _tpsTicks = new(30);
    private readonly RollingAverage _tpsSeconds = new(30);

    // ── Game thread wait time tracking for /stats endpoint ──
    private readonly RollingAverage _avgGameThreadWaitMs = new(60);
    private readonly ConcurrentQueue<double> _completedWaitTimes = new();

    // ── Game state snapshot for read-only endpoints ──
    // Populated once per second on the game thread (UnvalidatedUpdateTicked),
    // read by HTTP threads without blocking. This decouples /status, /players,
    // /farmhands, /cabins, and /auth from game thread availability, so they
    // respond instantly even during long ticks, saves, or thread starvation.
    private volatile GameStateSnapshot _snapshot = new();
    private DateTime _lastSnapshotUpdate;

    // Set in the constructor (DI singleton — single construction) so GameManagerService
    // can republish the snapshot at /reload and /newgame completion via the static
    // RefreshSnapshotAtLoadCompletion hook, the same static-access shape as
    // GameManagerService.Instance.
    private static ApiService? _instance;

    // Monotonic snapshot sequence. Incremented every time a new snapshot is
    // published; used by /wait/* long-poll endpoints to let clients block
    // until the snapshot they care about has changed.
    private long _snapshotSequence;

    // TCS that long-poll waiters await. Rotated atomically on every
    // snapshot publish: capture the old TCS, allocate a new one, publish
    // the new snapshot, then signal the old TCS so any waiters wake up
    // and re-evaluate. Lock-free read for waiters.
    private TaskCompletionSource _snapshotChanged = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    // Previous snapshot's farmer uids (everyone except the host), used to
    // emit otherfarmers_changed events only on actual set diff. Keeping this
    // a HashSet makes the symmetric-diff cheap; bounded by MaxPlayers.
    private HashSet<long>? _previousOtherFarmerUids;

    // Previous snapshot's cabin ownership map, keyed by (tileX, tileY) so
    // cabin position (which is stable after placement) identifies the slot,
    // and (ownerId, ownerIsCustomized) captures the binding state. Used to
    // emit cabin_owner_changed events on ownership transitions only —
    // without this, post-mortem analysis has no deterministic signal for
    // "when did cabin.owner.UniqueMultiplayerID flip from 0 to X", which is
    // the exact race PasswordProtection / CabinManager integration can
    // expose under concurrent joins.
    private Dictionary<
        (int TileX, int TileY),
        (long OwnerId, bool IsCustomized)
    >? _previousCabinOwners;

    // Latches so we only emit newDay-skip and recurring errors once per
    // transition, not every tick.
    private bool _snapshotNewDayLatched;
    private string? _lastSnapshotErrorKey;
    private DateTime _lastSnapshotErrorEmittedAt;

    private sealed class GameStateSnapshot
    {
        // /status
        public bool IsOnline;
        public bool IsReady;

        // "The day transition (newDaySync barrier + save + map load) is complete and we're back in
        // normal play" — the transition-only subset of GameServer.isGameAvailable, EXCLUDING the
        // wedding / festival / demolish-lock factors. IsReady (= isGameAvailable) stays false through
        // an active festival or a queued/running wedding even after the transition itself finished, so
        // a post-day-change settle that waits on IsReady dead-waits for the whole festival/wedding.
        // This field flips true the moment the transition barrier clears, regardless of those
        // post-transition activities, so the settle can proceed and the test's own
        // festival/wedding polling takes over. On an ordinary day (no festival/wedding) this is
        // identical to IsReady. See DayChangeWaiter post-transition settle.
        public bool DayTransitionComplete;
        public int PlayerCount;
        public int MaxPlayers = 4;
        public string FarmName = "";
        public int Day;
        public string Season = "";
        public int Year;
        public int TimeOfDay;
        public string FarmTypeKey = "";
        public string GameVersion = "";
        public bool IsPaused;
        public string Weather = "Sunny";
        public string WeatherTomorrow = "Sunny";

        // /players
        public List<PlayerInfo> Players = new();

        // /farmhands
        public List<FarmhandInfo> Farmhands = new();

        // /cabins
        public int CabinTotalCount;
        public int CabinAssignedCount;
        public int CabinAvailableCount;
        public List<CabinInfo> Cabins = new();
        public CabinMigrationInfo? CabinMigration;
        public StackSpotInfo? CabinStackSpot;
        public TilePoint? CabinMainFarmHouseEntry;

        // /auth
        public int AuthenticatedCount;
        public int PendingCount;

        public string CapturedAt = "";

        // Monotonic wall-clock capture time used to compute X-Snapshot-Age-Ms on
        // response. Kept alongside the existing ISO-string CapturedAt so readers
        // of the string field are unaffected. Nullable because snapshots built
        // for the offline branch (line ~779) are never served.
        public DateTime? CapturedAtUtc;

        /// <summary>
        /// Monotonic version number used by /wait/* long-poll endpoints to
        /// detect a changed snapshot since the client's last observed value.
        /// Stamped at publish time so a waiter can compare against an
        /// inbound `?since=N` parameter.
        /// </summary>
        public long Sequence;

        // ── Per-field change-time tracking ────────────────────────────────
        //
        // Each <c>FieldChangedAtUtc</c> is the wall-clock instant when that
        // field last *changed value* across consecutive snapshots. Carried
        // forward unchanged when the value is stable; reset to <c>now</c>
        // when the value differs from the previous published snapshot.
        // Computed once in <see cref="TakeGameStateSnapshot"/> by diffing
        // against the previously published snapshot.
        //
        // Used by <c>/wait/*</c> handlers to report the predicate-transition
        // time on the response (header <c>X-Predicate-Changed-At-Ms-Ago</c>),
        // which the test harness uses as producer-time on <c>wait_matched</c>.
        // The snapshot's <see cref="CapturedAtUtc"/> answers "when was this
        // data sampled?" — coarser than "when did this predicate become
        // true?" because snapshot publication is gated to 1Hz. The per-field
        // change-time is sub-tick-accurate because it's captured the moment
        // the diffing snapshot is built (which IS on a tick).
        //
        // For collection membership (Players, Farmhands), per-element
        // first-seen times are tracked on the elements themselves
        // (<see cref="PlayerInfoTracked.FirstSeenAtUtc"/> etc.) — see below.
        public DateTime IsReadyChangedAtUtc;
        public DateTime DayTransitionCompleteChangedAtUtc;
        public DateTime IsPausedChangedAtUtc;
        public DateTime PlayerCountChangedAtUtc;
        public DateTime FarmNameChangedAtUtc;
        public DateTime DayChangedAtUtc;
        public DateTime SeasonChangedAtUtc;
        public DateTime YearChangedAtUtc;
        public DateTime TimeOfDayChangedAtUtc;
        public DateTime IsOnlineChangedAtUtc;

        /// <summary>
        /// Per-player first-seen times, keyed by player ID. Parallel to
        /// <see cref="Players"/>. Kept off the wire-serialized
        /// <see cref="PlayerInfo"/> so the public <c>/players</c> response
        /// shape doesn't change.
        /// </summary>
        public Dictionary<long, DateTime> PlayerFirstSeenAtUtc = new();

        /// <summary>
        /// Per-farmhand-id tracking, keyed by farmhand ID. Stores when the
        /// farmhand was first seen, and when its <c>IsCustomized</c> flag
        /// last changed (predicates like
        /// <c>/wait/farmhands?hasFarmhand=X&amp;requireCustomized=true</c>
        /// need the latter).
        /// </summary>
        public Dictionary<long, FarmhandChangeTrack> FarmhandChangeTracks = new();
    }

    /// <summary>
    /// Per-farmhand change-time data, kept on the snapshot but not
    /// serialized to the wire. See
    /// <see cref="GameStateSnapshot.FarmhandChangeTracks"/>.
    /// </summary>
    private sealed class FarmhandChangeTrack
    {
        public DateTime FirstSeenAtUtc;
        public DateTime IsCustomizedChangedAtUtc;
        public bool IsCustomizedLastValue;
    }

    /// <summary>
    /// Default value of Game1.realMilliSecondsPerGameMinute, captured on first clock-speed call.
    /// </summary>
    private int _defaultRealMsPerGameMinute = -1;

    /// <summary>
    /// Event fired when a chat message is received from a WebSocket client (e.g., Discord bot).
    /// Parameters: author, message
    /// </summary>
    public event Action<string, string>? OnExternalChatMessage;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Whether API key authentication is enabled.
    /// </summary>
    private static readonly bool _authEnabled = !string.IsNullOrEmpty(Env.ApiKey);

    private static readonly HashSet<(string Method, string Path)> _publicEndpoints =
        typeof(ApiService)
            .GetMethods(
                BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.NonPublic
                    | BindingFlags.Public
            )
            .Select(m => m.GetCustomAttribute<ApiEndpointAttribute>())
            .Where(ep => ep is { Public: true } && !IsTestPath(ep.Path))
            .Select(ep => (ep!.Method, ep.Path))
            .ToHashSet();

    private readonly AccountLinkingService? _accountLinkingService;
    private readonly CropTracker.CropTrackerService? _cropTrackerService;
    private readonly Backup.BackupService? _backupService;
    private readonly Economy.BankService? _bankService;
    private readonly HostAutomation.SleepVoteService? _sleepVoteService;
    private readonly HostAutomation.AfkWatchdogService? _afkWatchdogService;
    private readonly ModManagement.ModEnforcementService? _modEnforcementService;
    private readonly ModManagement.ModDistributionService? _modDistributionService;
    private readonly ChatCommands.ChatCommandsService? _chatCommandsService;
    private readonly HostAutomation.PauseService? _pauseService;

    public ApiService(
        IModHelper helper,
        IMonitor monitor,
        ServerSettingsLoader settings,
        PersistentOptions persistentOptions,
        CabinManagerService cabinManager,
        RoleService roleService,
        SaveImport.SaveImportService saveImportService,
        NpcIntegrity.NpcSpriteIntegrityService npcSpriteIntegrity,
        Auth.FarmhandOwnershipService farmhandOwnership,
        GameThreadDispatcher dispatcher,
        PasswordProtectionService? passwordProtectionService = null,
        AccountLinkingService? accountLinkingService = null,
        CropTracker.CropTrackerService? cropTrackerService = null,
        Backup.BackupService? backupService = null,
        Economy.BankService? bankService = null,
        HostAutomation.SleepVoteService? sleepVoteService = null,
        HostAutomation.AfkWatchdogService? afkWatchdogService = null,
        ModManagement.ModEnforcementService? modEnforcementService = null,
        ModManagement.ModDistributionService? modDistributionService = null,
        ChatCommands.ChatCommandsService? chatCommandsService = null,
        HostAutomation.PauseService? pauseService = null
    )
        : base(helper, monitor)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _persistentOptions = persistentOptions;
        _cabinManager = cabinManager;
        _roleService = roleService;
        _saveImportService = saveImportService;
        _npcSpriteIntegrity = npcSpriteIntegrity;
        _farmhandOwnership = farmhandOwnership;
        _passwordProtectionService = passwordProtectionService;
        _accountLinkingService = accountLinkingService;
        _cropTrackerService = cropTrackerService;
        _backupService = backupService;
        _bankService = bankService;
        _sleepVoteService = sleepVoteService;
        _afkWatchdogService = afkWatchdogService;
        _modEnforcementService = modEnforcementService;
        _modDistributionService = modDistributionService;
        _chatCommandsService = chatCommandsService;
        _pauseService = pauseService;

        if (_chatCommandsService != null)
        {
            _chatCommandsService.OnPlayerChatMessage += (player, msg) => BroadcastChatMessage(player, msg);
            OnExternalChatMessage += _chatCommandsService.SendExternalMessage;
        }

        if (_pauseService != null)
        {
            _pauseService.OnPauseStateChanged += (isPaused, by, reason) =>
            {
                BroadcastToAllClients(new
                {
                    type = "pause_changed",
                    isPaused,
                    pausedBy = by,
                    reason,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            };
        }

        _instance = this;
    }

    public override void Entry()
    {
        if (!Env.ApiEnabled)
        {
            Monitor.Log("API service is disabled (API_ENABLED=false)", LogLevel.Info);
            return;
        }

        Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        // Mutations (DELETE /farmhands, POST /time, etc.) change game state that read-only
        // endpoints serve from the snapshot. Refresh immediately after a drain so subsequent
        // reads see the updated state without waiting up to 1 second.
        _dispatcher.Drained += TakeGameStateSnapshot;
        Helper.Events.Specialized.UnvalidatedUpdateTicking += OnUnvalidatedUpdateTicking;
        Helper.Events.Specialized.UnvalidatedUpdateTicked += OnUnvalidatedUpdateTicked;
        Helper.Events.Display.Rendered += OnRendered;

        // Server-side mirror of the test-client's HealthWatchdog. Polls
        // _lastTickTimestamp at 1 Hz on a dedicated thread so transient
        // stalls between /health polls don't hide. Fires exactly two
        // events per stall: _started and _recovered.
        StartGameThreadStallWatchdog();
    }

    private CancellationTokenSource? _stallWatchdogCts;

    /// <summary>Threshold above which a gap between ticks counts as a stall.</summary>
    private static readonly TimeSpan StallThreshold = TimeSpan.FromSeconds(3);

    private void StartGameThreadStallWatchdog()
    {
        _stallWatchdogCts = new CancellationTokenSource();
        var ct = _stallWatchdogCts.Token;
        var thread = new System.Threading.Thread(() =>
        {
            var stalled = false;
            var stallStartUtc = default(DateTime);
            long stallStartLastTick = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tickTicks = Interlocked.Read(ref _lastTickTimestamp);
                    if (tickTicks > 0)
                    {
                        var last = new DateTime(tickTicks, DateTimeKind.Utc);
                        var gap = DateTime.UtcNow - last;
                        if (!stalled && gap > StallThreshold)
                        {
                            stalled = true;
                            stallStartUtc = DateTime.UtcNow;
                            stallStartLastTick = tickTicks;
                            Diagnostics.ModEventLog.Emit(
                                "game_thread_stall_started",
                                new { lastTickMs = (long)gap.TotalMilliseconds }
                            );
                        }
                        else if (stalled && gap <= StallThreshold)
                        {
                            var totalStallMs = (long)
                                (DateTime.UtcNow - stallStartUtc).TotalMilliseconds;
                            stalled = false;
                            Diagnostics.ModEventLog.Emit(
                                "game_thread_stall_recovered",
                                new { durationMs = totalStallMs }
                            );
                            // Suppress unused warning; stallStartLastTick is
                            // kept for future diagnostics expansion.
                            _ = stallStartLastTick;
                        }
                    }
                }
                catch
                {
                    // Watchdog must never crash; swallow and keep polling.
                }

                try
                {
                    System.Threading.Thread.Sleep(1000);
                }
                catch
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "ApiService.StallWatchdog",
        };
        thread.Start();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        while (_completedWaitTimes.TryDequeue(out var waitMs))
        {
            _avgGameThreadWaitMs.Add(waitMs);
        }
    }

    private void OnUnvalidatedUpdateTicking(object? sender, UnvalidatedUpdateTickingEventArgs e)
    {
        _tickStopwatch.Restart();
    }

    private void OnUnvalidatedUpdateTicked(object? sender, UnvalidatedUpdateTickedEventArgs e)
    {
        // Record that the game thread is alive. Read by /health without RunOnGameThreadAsync.
        // Uses UnvalidatedUpdateTicked so /health stays accurate during saves.
        Interlocked.Exchange(ref _lastTickTimestamp, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _totalTickCount);

        // Wake any /wait/health waiters. Install a fresh TCS for future
        // waiters BEFORE signaling the old one — same ordering rule as
        // PublishSnapshot. RunContinuationsAsynchronously keeps continuations
        // off the game thread.
        var oldTickTcs = Interlocked.Exchange(
            ref _lastTickChanged,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        );
        oldTickTcs.TrySetResult();

        // Record tick timing for /stats
        _tickStopwatch.Stop();
        var tickMs = _tickStopwatch.Elapsed.TotalMilliseconds;
        Volatile.Write(ref _lastTickMs, tickMs);
        _avgTickMs.Add(tickMs);

        // TPS tracking (same pattern as FPS but for game ticks)
        _tickCount++;
        var tpsNow = DateTime.UtcNow;
        var tpsElapsed = (tpsNow - _lastTpsUpdate).TotalSeconds;
        if (tpsElapsed >= 1.0)
        {
            var sample = _tickCount / tpsElapsed;
            Volatile.Write(ref _currentTps, sample);
            _tpsTicks.Add(_tickCount);
            _tpsSeconds.Add(tpsElapsed);
            _tickCount = 0;
            _lastTpsUpdate = tpsNow;
        }

        // Refresh game state snapshot once per second for read-only endpoints.
        // Uses wall-clock time (not tick count) because TPS can drop well below 60
        // during startup or resource contention, making IsMultipleOf(60) unreliable.
        // Skip during day transitions: _newDayAfterFade runs saveFarmhands() on
        // a thread pool thread, which mutates farmhandData and cabin state. Reading
        // those collections here would risk corrupted state. The pre-transition
        // snapshot remains valid until UpdateTicked resumes and refreshes it.
        var now = DateTime.UtcNow;
        if ((now - _lastSnapshotUpdate).TotalSeconds >= 1.0)
        {
            if (Game1.newDay)
            {
                // Latch — one event per newDay streak, not per tick. Reset
                // by TakeGameStateSnapshot's success path once newDay ends.
                if (!_snapshotNewDayLatched)
                {
                    _snapshotNewDayLatched = true;
                    Diagnostics.ModEventLog.Emit("snapshot_skipped_newday");
                }
            }
            else
            {
                _lastSnapshotUpdate = now;
                TakeGameStateSnapshot();
            }
        }
    }

    /// <summary>
    /// Publishes a new snapshot atomically: stamps the next version number,
    /// rotates the snapshot-changed TCS so any /wait/* long-poll waiters
    /// wake up, and assigns to <see cref="_snapshot"/>. Lock-free reads for
    /// waiters; concurrent publishers are serialized by the call site
    /// (<see cref="OnUnvalidatedUpdateTicked"/> runs on the game thread).
    /// </summary>
    private void PublishSnapshot(GameStateSnapshot snap)
    {
        snap.Sequence = Interlocked.Increment(ref _snapshotSequence);
        // Rotate the TCS: install a fresh one for future waiters BEFORE
        // signaling the old one. Otherwise a fast waiter that completes
        // its post-resume snapshot read could observe the new snapshot
        // version, return early, then re-await — only to await the
        // already-completed TCS we hadn't yet rotated.
        var oldTcs = Interlocked.Exchange(
            ref _snapshotChanged,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        );
        _snapshot = snap;
        oldTcs.TrySetResult();
    }

    /// <summary>
    /// Builds a fresh <see cref="GameStateSnapshot"/> from current game state and
    /// publishes it atomically via <see cref="_snapshot"/>. Called from two places:
    /// <list type="bullet">
    /// <item><see cref="OnUnvalidatedUpdateTicked"/> (periodic, 1/sec) -- fires even during
    /// game thread stalls but skipped during day transitions to avoid concurrent access
    /// with the save task's farmhand/cabin mutations.</item>
    /// <item><see cref="GameThreadDispatcher.Drained"/> (after mutations) -- ensures changes from
    /// mutating endpoints are immediately visible to subsequent reads.</item>
    /// </list>
    /// HTTP threads read the published snapshot without blocking.
    /// </summary>
    private void TakeGameStateSnapshot()
    {
        try
        {
            var capturedAt = DateTime.UtcNow;
            // Read the previously-published snapshot under volatile semantics;
            // it's the diff baseline for per-field change-time tracking.
            var prevSnap = _snapshot;
            var snap = new GameStateSnapshot
            {
                IsOnline = Game1.IsServer && Game1.gameMode == 3,
                CapturedAt = capturedAt.ToString("o"),
                CapturedAtUtc = capturedAt,
            };

            // Game version is a process-wide engine constant (Game1.version), available before a
            // save loads — set it before the offline early-return so /status reports it even offline.
            snap.GameVersion = Game1.version ?? "";

            // IsOnline always tracked (its change is observable independently
            // of the early-return branch below).
            snap.IsOnlineChangedAtUtc =
                prevSnap.IsOnline == snap.IsOnline
                    ? (
                        prevSnap.IsOnlineChangedAtUtc == default
                            ? capturedAt
                            : prevSnap.IsOnlineChangedAtUtc
                    )
                    : capturedAt;

            if (!snap.IsOnline)
            {
                PublishSnapshot(snap);
                return;
            }

            // /status fields
            var onlineFarmers = Game1.getOnlineFarmers().ToList();
            snap.PlayerCount = onlineFarmers.Count(f =>
                f.UniqueMultiplayerID != Game1.player?.UniqueMultiplayerID
            );
            snap.MaxPlayers = Game1.netWorldState.Value?.CurrentPlayerLimit ?? 4;
            snap.IsReady = Game1.server?.isGameAvailable() ?? false;
            snap.DayTransitionComplete = GameManagerService.IsDayTransitionComplete();
            snap.FarmName = Game1.player?.farmName.Value ?? "";
            snap.Day = Game1.dayOfMonth;
            snap.Season = Game1.currentSeason ?? "";
            snap.Year = Game1.year;
            snap.TimeOfDay = Game1.timeOfDay;
            snap.FarmTypeKey = Game1.GetFarmTypeKey();
            snap.IsPaused = Game1.netWorldState.Value?.IsPaused ?? false;
            snap.Weather = GetCurrentWeatherSummary();
            snap.WeatherTomorrow = GetTomorrowWeatherForecast();

            // Per-field change-time tracking. The local helper compares the
            // previous-snapshot's value to the new value: same → carry over
            // the prior change-time (or stamp `capturedAt` if the prior had
            // none); different → stamp `capturedAt`. This makes the
            // change-time sharp to whichever tick the field actually flipped,
            // not gated by the 1Hz snapshot publish cadence.
            DateTime CarryOrStamp<T>(T newVal, T prevVal, DateTime prevChangedAt) =>
                EqualityComparer<T>.Default.Equals(newVal, prevVal)
                    ? (prevChangedAt == default ? capturedAt : prevChangedAt)
                    : capturedAt;

            snap.IsReadyChangedAtUtc = CarryOrStamp(
                snap.IsReady,
                prevSnap.IsReady,
                prevSnap.IsReadyChangedAtUtc
            );
            snap.DayTransitionCompleteChangedAtUtc = CarryOrStamp(
                snap.DayTransitionComplete,
                prevSnap.DayTransitionComplete,
                prevSnap.DayTransitionCompleteChangedAtUtc
            );
            snap.IsPausedChangedAtUtc = CarryOrStamp(
                snap.IsPaused,
                prevSnap.IsPaused,
                prevSnap.IsPausedChangedAtUtc
            );
            snap.PlayerCountChangedAtUtc = CarryOrStamp(
                snap.PlayerCount,
                prevSnap.PlayerCount,
                prevSnap.PlayerCountChangedAtUtc
            );
            snap.FarmNameChangedAtUtc = CarryOrStamp(
                snap.FarmName,
                prevSnap.FarmName,
                prevSnap.FarmNameChangedAtUtc
            );
            snap.DayChangedAtUtc = CarryOrStamp(snap.Day, prevSnap.Day, prevSnap.DayChangedAtUtc);
            snap.SeasonChangedAtUtc = CarryOrStamp(
                snap.Season,
                prevSnap.Season,
                prevSnap.SeasonChangedAtUtc
            );
            snap.YearChangedAtUtc = CarryOrStamp(
                snap.Year,
                prevSnap.Year,
                prevSnap.YearChangedAtUtc
            );
            snap.TimeOfDayChangedAtUtc = CarryOrStamp(
                snap.TimeOfDay,
                prevSnap.TimeOfDay,
                prevSnap.TimeOfDayChangedAtUtc
            );

            // /players
            foreach (var farmer in onlineFarmers)
            {
                if (farmer.UniqueMultiplayerID == Game1.player?.UniqueMultiplayerID)
                {
                    continue;
                }

                snap.Players.Add(
                    new PlayerInfo
                    {
                        Id = farmer.UniqueMultiplayerID,
                        Name = farmer.Name ?? farmer.displayName ?? "Unknown",
                        IsOnline = true,
                        Location = farmer.GetTargetLocation()?.NameOrUniqueName ?? "",
                    }
                );
            }

            // Per-player first-seen times. Carry over from the prior
            // snapshot's tracking dict for IDs still present; new IDs get
            // `capturedAt`. Stale IDs (player left) are dropped, so a
            // subsequent rejoin counts as a fresh first-seen.
            foreach (var p in snap.Players)
            {
                snap.PlayerFirstSeenAtUtc[p.Id] = prevSnap.PlayerFirstSeenAtUtc.TryGetValue(
                    p.Id,
                    out var existing
                )
                    ? existing
                    : capturedAt;
            }

            // /farmhands
            foreach (var farmer in Game1.getAllFarmhands())
            {
                try
                {
                    snap.Farmhands.Add(
                        new FarmhandInfo
                        {
                            Id = farmer.UniqueMultiplayerID,
                            Name = farmer.Name ?? "",
                            IsCustomized = farmer.isCustomized.Value,
                        }
                    );
                }
                catch (Exception ex)
                {
                    Monitor.Log(
                        $"[Snapshot] Error reading farmhand {farmer.UniqueMultiplayerID}: {ex.Message}",
                        LogLevel.Debug
                    );
                }
            }

            // Per-farmhand change-tracking. Carry forward `FirstSeenAtUtc`
            // and compare `IsCustomized` to detect customization transitions.
            foreach (var f in snap.Farmhands)
            {
                if (prevSnap.FarmhandChangeTracks.TryGetValue(f.Id, out var prevTrack))
                {
                    snap.FarmhandChangeTracks[f.Id] = new FarmhandChangeTrack
                    {
                        FirstSeenAtUtc = prevTrack.FirstSeenAtUtc,
                        IsCustomizedChangedAtUtc =
                            f.IsCustomized == prevTrack.IsCustomizedLastValue
                                ? prevTrack.IsCustomizedChangedAtUtc
                                : capturedAt,
                        IsCustomizedLastValue = f.IsCustomized,
                    };
                }
                else
                {
                    snap.FarmhandChangeTracks[f.Id] = new FarmhandChangeTrack
                    {
                        FirstSeenAtUtc = capturedAt,
                        IsCustomizedChangedAtUtc = capturedAt,
                        IsCustomizedLastValue = f.IsCustomized,
                    };
                }
            }

            // /cabins
            SnapshotCabins(snap);

            // /auth
            if (_passwordProtectionService?.IsEnabled == true)
            {
                foreach (var farmer in onlineFarmers)
                {
                    if (farmer.UniqueMultiplayerID == Game1.player?.UniqueMultiplayerID)
                    {
                        continue;
                    }

                    if (
                        _passwordProtectionService.IsPlayerAuthenticated(farmer.UniqueMultiplayerID)
                    )
                    {
                        snap.AuthenticatedCount++;
                    }
                    else
                    {
                        snap.PendingCount++;
                    }
                }
            }

            PublishSnapshot(snap);

            // Reset the newDay skip latch once we've completed a real
            // snapshot after the transition.
            _snapshotNewDayLatched = false;

            // Emit otherfarmers_changed on set diff only. Cheap set-diff
            // using HashSet<long>; bounded by MaxPlayers.
            var currentUids = new HashSet<long>();
            foreach (var p in snap.Players)
            {
                currentUids.Add(p.Id);
            }

            if (_previousOtherFarmerUids == null)
            {
                // First snapshot after boot. If any players are visible,
                // record them as additions; otherwise stay silent.
                if (currentUids.Count > 0)
                {
                    Diagnostics.ModEventLog.Emit(
                        "otherfarmers_changed",
                        new
                        {
                            added = currentUids.ToArray(),
                            removed = System.Array.Empty<long>(),
                            total = currentUids.Count,
                        }
                    );
                }
            }
            else if (!currentUids.SetEquals(_previousOtherFarmerUids))
            {
                var added = currentUids.Except(_previousOtherFarmerUids).ToArray();
                var removed = _previousOtherFarmerUids.Except(currentUids).ToArray();
                Diagnostics.ModEventLog.Emit(
                    "otherfarmers_changed",
                    new
                    {
                        added,
                        removed,
                        total = currentUids.Count,
                    }
                );
            }
            _previousOtherFarmerUids = currentUids;

            // Cabin-ownership diff. Only emit on actual change. Captures
            // the (oldOwnerId → newOwnerId) transition plus names so the
            // timeline shows exactly when a cabin's owner binding flipped.
            var currentCabinOwners = new Dictionary<(int, int), (long, bool)>();
            var currentCabinMeta = new Dictionary<(int, int), (string OwnerName, string Type)>();
            foreach (var c in snap.Cabins)
            {
                var key = (c.TileX, c.TileY);
                currentCabinOwners[key] = (c.OwnerId, c.IsAssigned);
                currentCabinMeta[key] = (c.OwnerName, c.Type);
            }

            if (_previousCabinOwners != null)
            {
                foreach (var kv in currentCabinOwners)
                {
                    _previousCabinOwners.TryGetValue(kv.Key, out var prev);
                    if (prev == kv.Value)
                    {
                        continue;
                    }

                    currentCabinMeta.TryGetValue(kv.Key, out var meta);
                    Diagnostics.ModEventLog.Emit(
                        "cabin_owner_changed",
                        new
                        {
                            tileX = kv.Key.Item1,
                            tileY = kv.Key.Item2,
                            oldOwnerId = prev.Item1,
                            newOwnerId = kv.Value.Item1,
                            newOwnerName = meta.OwnerName,
                            newOwnerIsCustomized = kv.Value.Item2,
                            cabinType = meta.Type,
                        }
                    );
                }
            }
            _previousCabinOwners = currentCabinOwners;
        }
        catch (Exception ex)
        {
            // Never crash the game thread. The stale snapshot remains valid.
            // Promote to Warn and emit a structured event so silent snapshot
            // breakage can be detected — the old Debug-level log masked
            // exactly this class of bug. Throttle by exception-type+top-frame
            // so a recurring error doesn't flood the log.
            var topFrame = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
            var key = $"{ex.GetType().Name}|{topFrame}";
            var now = DateTime.UtcNow;
            if (
                _lastSnapshotErrorKey != key
                || (now - _lastSnapshotErrorEmittedAt).TotalSeconds >= 1.0
            )
            {
                _lastSnapshotErrorKey = key;
                _lastSnapshotErrorEmittedAt = now;
                Monitor.Log(
                    $"[Snapshot] Error building game state snapshot: {ex.Message}",
                    LogLevel.Warn
                );
                Diagnostics.ModEventLog.Emit(
                    "snapshot_error",
                    new
                    {
                        exception = ex.GetType().Name,
                        message = ex.Message,
                        stackTop = topFrame,
                    }
                );
            }
        }
    }

    /// <summary>
    /// Republishes the read-only-endpoint snapshot at /reload and /newgame completion, called by
    /// <see cref="GameManager.GameManagerService"/> on the resolve tick — the point the completion
    /// contract already guarantees the SaveLoaded chain (cabin migration/sync/sweep,
    /// EnsureAtLeastXCabins) has run. Runs on the game thread (same serialization as the two other
    /// <c>TakeGameStateSnapshot</c> call sites) and doesn't reset the 1 Hz timer, so a test's first
    /// post-reload read sees the migrated world instead of a snapshot captured mid-load.
    /// With API disabled it still republishes (<c>_instance</c> is ctor-set, not gated by
    /// <c>Entry</c>), which is harmless — a snapshot with no HTTP listener is never read.
    /// A build failure inside <c>TakeGameStateSnapshot</c> is swallowed there (keeping the prior
    /// snapshot) as at every call site; completion resolves regardless, by that shared contract.
    /// </summary>
    public static void RefreshSnapshotAtLoadCompletion() => _instance?.TakeGameStateSnapshot();

    /// <summary>
    /// Populates cabin data in the snapshot. Reads cabin ownership from farmhandReference,
    /// which is set correctly during connection by PasswordProtectionService.EnsureRealCabinAssignment.
    ///
    /// Note: <c>IsAssigned</c> requires <c>cabin.owner.isCustomized.Value == true</c>, so
    /// it stays false for several seconds after a fresh-customized join while the
    /// customize XML round-trips. Test code that polls for ownership of a brand-new
    /// player should match by <c>OwnerId</c> alone, not by <c>IsAssigned</c>.
    /// </summary>
    private void SnapshotCabins(GameStateSnapshot snap)
    {
        try
        {
            var farm = Game1.getFarm();
            if (farm == null)
            {
                return;
            }

            var cabinBuildings = farm.buildings.Where(b => b.isCabin).ToList();
            var strategy = _persistentOptions.Data.CabinStrategy;

            foreach (var building in cabinBuildings)
            {
                try
                {
                    var cabin = building.GetIndoors<Cabin>();
                    var ownerId = cabin?.owner?.UniqueMultiplayerID ?? 0;
                    var ownerName = cabin?.owner?.Name ?? "";
                    var isAssigned = ownerId != 0 && cabin?.owner?.isCustomized.Value == true;

                    var role = building.GetCabinRole();
                    string cabinType = role switch
                    {
                        CabinRole.SharedLobby => "SharedLobby",
                        CabinRole.IndividualLobby => "IndividualLobby",
                        CabinRole.Editing => "Editing",
                        _ when strategy == CabinStrategy.FarmhouseStack => "FarmhouseStack",
                        _ when strategy == CabinStrategy.CabinStack => "CabinStack",
                        _ => "Normal",
                    };
                    var isHidden = role != CabinRole.Player || building.IsInHiddenStack();

                    snap.Cabins.Add(
                        new CabinInfo
                        {
                            TileX = building.tileX.Value,
                            TileY = building.tileY.Value,
                            IsHidden = isHidden,
                            Type = cabinType,
                            OwnerId = ownerId,
                            OwnerName = ownerName,
                            IsAssigned = isAssigned,
                        }
                    );
                }
                catch (Exception ex)
                {
                    Monitor.Log(
                        $"[Snapshot] Error reading cabin at ({building.tileX.Value},{building.tileY.Value}): {ex.Message}",
                        LogLevel.Debug
                    );
                }
            }

            snap.CabinTotalCount = snap.Cabins.Count;
            snap.CabinAssignedCount = snap.Cabins.Count(c => c.IsAssigned);
            snap.CabinAvailableCount = snap.CabinTotalCount - snap.CabinAssignedCount;

            // Authoritative main-farmhouse door tile — the target a stacked FarmhouseStack cabin's
            // exit warp is repointed to. E2E asserts the repoint against this exact tile.
            var entry = farm.GetMainFarmHouseEntry();
            snap.CabinMainFarmHouseEntry = new TilePoint { X = entry.X, Y = entry.Y };

            // CabinStack shared stack spot (null under other strategies). E2E surface for
            // the 'cabins stackspot' / '!stackspot' commands.
            var stackSpot = _cabinManager.GetStackSpotStatus();
            if (stackSpot != null)
            {
                var spot = stackSpot.Value;
                snap.CabinStackSpot = new StackSpotInfo
                {
                    TileX = spot.Spot.X,
                    TileY = spot.Spot.Y,
                    IsOverride = spot.IsOverride,
                    IsObstructed = spot.IsObstructed,
                };
            }

            // Staged strategy migration (null when none is active). E2E gates staging
            // state on this — mod events are diagnostics only.
            var migration = _cabinManager.GetMigrationStatus();
            if (migration != null)
            {
                var m = migration.Value;
                snap.CabinMigration = new CabinMigrationInfo
                {
                    FromStrategy = m.FromStrategy.ToString(),
                    ToStrategy = m.ToStrategy.ToString(),
                    PlacedCount = m.PlacedCount,
                    RemainingCount = m.RemainingCount,
                };
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"[Snapshot] Error reading cabins: {ex.Message}", LogLevel.Debug);
        }
    }

    private void OnRendered(object? sender, RenderedEventArgs e)
    {
        _frameCount++;
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;
        if (elapsed >= 1.0)
        {
            Volatile.Write(ref _currentFps, _frameCount / elapsed);
            _frameCount = 0;
            _lastFpsUpdate = now;
        }
    }

    /// <summary>
    /// Queues an action to run on the main game thread and waits for it to complete.
    /// Use this for game state modifications from async HTTP handlers. Throws
    /// <see cref="TaskCanceledException"/> if the action is still queued after
    /// <paramref name="timeoutMs"/>. The measured wait feeds the /stats rolling average;
    /// a timed-out or faulted call is not recorded (not representative of normal wait times).
    /// </summary>
    private async Task RunOnGameThreadAsync(Action action, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        await _dispatcher
            .RunAsync(action, TimeSpan.FromMilliseconds(timeoutMs))
            .ConfigureAwait(false);
        sw.Stop();
        _completedWaitTimes.Enqueue(sw.Elapsed.TotalMilliseconds);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        StartServer();
    }

    /// <summary>
    /// Validates authentication from the Authorization header.
    /// Supports both direct master API_KEY and user session tokens.
    /// Expects format: "Bearer &lt;token&gt;"
    /// </summary>
    private async Task<bool> ValidateAuthAsync(HttpListenerRequest request)
    {
        if (!_authEnabled)
        {
            return true;
        }

        string? providedKey = null;
        var authHeader = request.Headers["Authorization"];
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            providedKey = authHeader.Substring(7).Trim();
        }
        else if (!string.IsNullOrEmpty(request.Headers["X-API-Key"]))
        {
            providedKey = request.Headers["X-API-Key"]?.Trim();
        }
        else if (!string.IsNullOrEmpty(request.QueryString["token"]))
        {
            providedKey = request.QueryString["token"]?.Trim();
        }
        else if (!string.IsNullOrEmpty(request.QueryString["key"]))
        {
            providedKey = request.QueryString["key"]?.Trim();
        }

        if (string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        // Master API Key check
        if (providedKey == Env.ApiKey)
        {
            return true;
        }

        // Web user session check
        if (_accountLinkingService != null)
        {
            var session = await _accountLinkingService.ValidateSessionAsync(providedKey);
            if (session != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes a 401 Unauthorized response.
    /// </summary>
    private static async Task WriteUnauthorizedAsync(HttpListenerResponse response)
    {
        response.StatusCode = 401;
        response.Headers.Add("WWW-Authenticate", "Bearer");
        await WriteJsonAsync(
            response,
            new { error = "Unauthorized. Provide a valid Authorization header: Bearer <api-key>" }
        );
    }

    /// <summary>
    /// Writes the standard 404 Not Found response. The single source of the 404 body so
    /// every dispatcher arm (and the production /test/* gate) returns byte-identical output.
    /// The requested path is echoed to match the test-client mirror's 404 shape
    /// (tests/test-client/HttpServer/TestApiServer.cs).
    /// </summary>
    private static async Task WriteNotFoundAsync(HttpListenerResponse response, string path)
    {
        response.StatusCode = 404;
        await WriteJsonAsync(response, new { error = "Not found", path });
    }

    private void StartServer()
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            // Generate OpenAPI spec. Test-only endpoints are spec-visible iff they are
            // runtime-reachable (Env.IsTest) — the same gate the dispatcher uses, so the
            // published contract and the routing cannot drift.
            var document = OpenApiGenerator.Generate(
                typeof(ApiService),
                "Stardew Dedicated Server API",
                "v1",
                "HTTP API for monitoring and interacting with the Stardew Valley dedicated server",
                includeMethod: m =>
                {
                    var ep = m.GetCustomAttribute<ApiEndpointAttribute>();
                    return ep == null || !IsTestPath(ep.Path) || Env.IsTest;
                }
            );
            _openApiSpec = document.ToJson();

            // Take the port from the startup script's phase responder right before binding, so
            // it is never unanswered in between.
            ApiPortHandoff.TakeOver(Env.ApiHandoffPidFile, Env.ApiPort, Monitor);
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{Env.ApiPort}/");
            _listener.Start();

            // Start processing requests
            _cts = new CancellationTokenSource();
            _serverTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));

            // Start WebSocket cleanup timer (every 30 seconds)
            _wsCleanupTimer = new Timer(
                _ => CleanupDeadWebSocketClients(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30)
            );

            _isRunning = true;

            Monitor.Log(
                $"API server listening on port {Env.ApiPort} (docs: http://localhost:{Env.ApiPort}/docs)",
                LogLevel.Info
            );
            if (_authEnabled)
            {
                Monitor.Log("API authentication enabled", LogLevel.Info);
            }
            else
            {
                Monitor.Log(
                    "***********************************************************************",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "*                                                                     *",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "*    WARNING: API authentication is disabled!                         *",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "*    All endpoints are publicly accessible.                           *",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "*    Set the API_KEY environment variable for production use.         *",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "*                                                                     *",
                    LogLevel.Warn
                );
                Monitor.Log(
                    "***********************************************************************",
                    LogLevel.Warn
                );
            }
        }
        catch (Exception ex)
        {
            // Clean up partially-initialized listener to prevent background accept
            // callbacks from firing on thread pool threads (which would trigger
            // FAIL_FAST via AppDomain.UnhandledException).
            try
            {
                _listener?.Close();
            }
            catch (Exception closeEx)
            {
                Diagnostics.ModEventLog.Emit(
                    "exception_swallowed",
                    new
                    {
                        location = "ApiService.StartServer.listenerClose",
                        exceptionType = closeEx.GetType().Name,
                        message = closeEx.Message,
                    }
                );
            }
            _listener = null;
            Monitor.Log($"Failed to start API server: {ex}", LogLevel.Error);
        }
    }

    private async Task ProcessRequestsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed
                break;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error accepting request: {ex}", LogLevel.Warn);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Extract the correlation header sent by the test harness. This
        // flows to mod-side structured events so a single requestId
        // stitches logs across containers. Safe to be null in production
        // (the mod runs outside tests too).
        var requestId = request.Headers["X-Request-Id"];

        // Sent by the harness on every request regardless of tracing level
        // (unlike X-Request-Id), so every server event during the request is
        // attributable to its test. Null in production.
        var testId = request.Headers["X-Test-Id"];

        // Echo the same id on the response so the client-side
        // TracingHandler can confirm the round-trip and the mod's view
        // of the id matches the caller's. Header writes must happen
        // before the body is streamed — set it first.
        if (!string.IsNullOrEmpty(requestId))
        {
            try
            {
                response.Headers["X-Request-Id"] = requestId;
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Failed to echo X-Request-Id: {ex.Message}", LogLevel.Debug);
            }
        }

        // Snapshot age — tells the test harness how stale the data it's about
        // to receive actually is. Critical for diagnosing "I polled /players 10x
        // in 10s and the uid never appeared": without this the test cannot
        // distinguish "the snapshot was fresh and the uid really wasn't there"
        // from "the snapshot was 9.8s stale". Computed from CapturedAtUtc so
        // only real online snapshots carry a value (offline-branch snapshots
        // leave it null and the header is omitted).
        try
        {
            var snapForHeader = _snapshot;
            if (snapForHeader.CapturedAtUtc is DateTime cap)
            {
                var ageMs = (long)(DateTime.UtcNow - cap).TotalMilliseconds;
                if (ageMs < 0)
                {
                    ageMs = 0; // clock skew paranoia
                }

                response.Headers["X-Snapshot-Age-Ms"] = ageMs.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                );
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"[API] Failed to emit X-Snapshot-Age-Ms: {ex.Message}", LogLevel.Debug);
        }

        using var _correlationScope = RequestContext.Bind(requestId, testId);

        // Per-request stopwatch fed into the http_served event in the
        // finally block. Captured here so early-return paths (WebSocket
        // upgrade, 404/405, auth reject) are still measured.
        var _httpServedStopwatch = System.Diagnostics.Stopwatch.StartNew();
        string _servedPath = request.Url?.AbsolutePath ?? "/";
        string _servedMethod = request.HttpMethod;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            // Handle WebSocket upgrade
            if (path == "/ws" && request.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(context);
                return;
            }

            // Handle CORS preflight
            if (method == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-API-Key, X-Request-Id, X-File-Name");
                response.Close();
                return;
            }

            if (!_publicEndpoints.Contains((method, path)) && !IsPublicSyncPath(path) && !await ValidateAuthAsync(request))
            {
                await WriteUnauthorizedAsync(response);
                return;
            }

            // After auth on purpose: in production a /test/* path must look like any other
            // unknown route (401 without a key, 404 with one).
            if (IsTestPath(path))
            {
                if (!Env.IsTest)
                {
                    await WriteNotFoundAsync(response, path);
                    return;
                }
                await DispatchTestEndpointAsync(method, path, request, response);
                return;
            }

            // Route request
            if (method == "GET")
            {
                if (path.StartsWith("/api/mods/sync/download/"))
                {
                    var folderName = Uri.UnescapeDataString(path.Substring("/api/mods/sync/download/".Length));
                    await HandleGetModZipDownloadAsync(request, response, folderName);
                    return;
                }

                switch (path)
                {
                    case "/status":
                        await WriteJsonAsync(response, HandleGetStatus());
                        break;
                    case "/players":
                        await WriteJsonAsync(response, HandleGetPlayers());
                        break;
                    case "/farmhands":
                        await ProfileFarmhandsAsync(response);
                        break;
                    case "/health":
                        await WriteJsonAsync(response, HandleGetHealth());
                        break;
                    case "/wait/status":
                        await HandleWaitStatusAsync(request, response);
                        break;
                    case "/wait/players":
                        await HandleWaitPlayersAsync(request, response);
                        break;
                    case "/wait/health":
                        await HandleWaitHealthAsync(request, response);
                        break;
                    case "/wait/farmhands":
                        await HandleWaitFarmhandsAsync(request, response);
                        break;
                    case "/diagnostics/state":
                        await WriteJsonAsync(
                            response,
                            await HandleGetDiagnosticsStateAsync(request)
                        );
                        break;
                    case "/diagnostics/handler-timing":
                        await WriteJsonAsync(response, HandleGetHandlerTiming());
                        break;
                    case "/stats":
                        await WriteJsonAsync(response, HandleGetStats());
                        break;
                    case "/settings":
                        await WriteJsonAsync(response, HandleGetSettings());
                        break;
                    case "/cabins":
                        await WriteJsonAsync(response, HandleGetCabins());
                        break;
                    case "/rendering":
                        await WriteJsonAsync(response, HandleGetRendering());
                        break;
                    case "/screenshot":
                        await WriteJsonAsync(response, await HandleGetScreenshotAsync());
                        break;
                    case "/auth":
                        await WriteJsonAsync(response, HandleGetAuthStatus());
                        break;
                    case "/swagger/v1/swagger.json":
                        await WriteJsonRawAsync(response, HandleGetOpenApiSpec());
                        break;
                    case "/docs":
                        await WriteHtmlAsync(response, HandleGetDocs());
                        break;
                    case "/api/auth/me":
                        await HandleGetAuthMeAsync(request, response);
                        break;
                    case "/farm/crops":
                        await WriteJsonAsync(response, HandleGetFarmCrops());
                        break;
                    case "/weather":
                    case "/farm/weather":
                        await WriteJsonAsync(response, HandleGetWeather());
                        break;
                    case "/api/backups":
                        await WriteJsonAsync(response, _backupService?.GetBackups() ?? new List<Backup.BackupEntry>());
                        break;
                    case "/api/mods/installed":
                        await WriteJsonAsync(response, _modEnforcementService?.GetInstalledServerMods() ?? new List<ModManagement.ServerInstalledModInfo>());
                        break;
                    case "/api/mods/policy":
                        await WriteJsonAsync(response, new
                        {
                            policy = _modEnforcementService?.GetPolicy() ?? new ModManagement.ModPolicyData(),
                            installedCount = _modEnforcementService?.GetInstalledServerMods().Count ?? 0,
                            rejections = _modEnforcementService?.GetRecentRejections() ?? new List<ModManagement.ModRejectionLog>(),
                            connectedPeers = _modEnforcementService?.GetConnectedPeerAudits() ?? new List<ModManagement.PeerAuditInfo>()
                        });
                        break;
                    case "/api/mods/custom/list":
                        await WriteJsonAsync(response, _modDistributionService?.GetCustomModsList() ?? new List<ModManagement.SyncModInfo>());
                        break;
                    case "/api/mods/sync/manifest":
                        await WriteJsonAsync(response, _modDistributionService?.GetSyncManifest() ?? new ModManagement.SyncManifestResponse());
                        break;
                    case "/api/mods/sync/bundle":
                        await HandleGetModBundleZipAsync(response);
                        break;
                    case "/api/mods/sync/client-mod":
                        await HandleGetClientModZipAsync(response);
                        break;
                    case "/api/mods/sync/download":
                        await HandleGetModZipDownloadAsync(request, response);
                        break;
                    case "/api/backup/download":
                        var downloadFile = request.QueryString["file"];
                        if (string.IsNullOrEmpty(downloadFile))
                        {
                            response.StatusCode = 400;
                            await WriteJsonAsync(response, new { error = "Parameter 'file' is required" });
                            break;
                        }
                        var filePath = _backupService?.GetBackupFilePath(downloadFile);
                        if (filePath == null || !File.Exists(filePath))
                        {
                            response.StatusCode = 404;
                            await WriteJsonAsync(response, new { error = "Backup archive not found" });
                            break;
                        }
                        response.ContentType = "application/zip";
                        response.Headers["Content-Disposition"] = $"attachment; filename=\"{Path.GetFileName(filePath)}\"";
                        using (var fs = File.OpenRead(filePath))
                        {
                            response.ContentLength64 = fs.Length;
                            await fs.CopyToAsync(response.OutputStream);
                        }
                        break;
                    case "/api/economy/overview":
                        var economyData = _bankService != null
                            ? await _bankService.GetEconomyOverviewAsync()
                            : new { useSeparateWallets = false, sharedFarmMoney = 0, accounts = new object[0] };
                        await WriteJsonAsync(response, economyData);
                        break;
                    case "/api/automation/status":
                        await WriteJsonAsync(response, new
                        {
                            sleepVote = _sleepVoteService?.GetStatus(),
                            afkWatchdog = _afkWatchdogService?.GetStatus(),
                            pause = new
                            {
                                isPaused = _pauseService?.IsManuallyPaused ?? false,
                                reason = _pauseService?.PauseReason ?? "",
                                pausedBy = _pauseService?.PausedBy ?? "",
                                remainingSeconds = _pauseService?.GetRemainingSeconds() ?? 0
                            }
                        });
                        break;
                    case "/api/server/pause/status":
                        await WriteJsonAsync(response, new
                        {
                            isPaused = _pauseService?.IsManuallyPaused ?? false,
                            reason = _pauseService?.PauseReason ?? "",
                            pausedBy = _pauseService?.PausedBy ?? "",
                            remainingSeconds = _pauseService?.GetRemainingSeconds() ?? 0
                        });
                        break;
                    case "/api/chat/history":
                        await WriteJsonAsync(response, GetChatHistory());
                        break;
                    case "/api/farm/animals":
                        await WriteJsonAsync(response, await HandleGetFarmAnimalsAsync());
                        break;
                    case "/api/admin/pending-players":
                        var pendingUser = await GetUserFromRequestAsync(request);
                        if (pendingUser == null || (pendingUser.Value.Role != "SUPERADMIN" && pendingUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        var pendingPlayers = _passwordProtectionService?.GetPendingPlayers() ?? new List<PasswordProtectionService.PendingPlayerDto>();
                        await WriteJsonAsync(response, pendingPlayers);
                        break;
                    case "/api/admin/users":
                        var listUser = await GetUserFromRequestAsync(request);
                        if (listUser == null || (listUser.Value.Role != "SUPERADMIN" && listUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        if (_accountLinkingService == null)
                        {
                            await WriteJsonAsync(response, new List<object>());
                            break;
                        }
                        var registeredUsers = await _accountLinkingService.GetRegisteredUsersAsync();
                        var sanitizedUsers = registeredUsers.Select(u => new
                        {
                            farmerId = u.FarmerId,
                            username = u.Username,
                            role = u.Role,
                            createdAtUtc = u.CreatedAtUtc,
                            lastLoginUtc = u.LastLoginUtc
                        }).ToList();
                        await WriteJsonAsync(response, sanitizedUsers);
                        break;
                    case "/api/admin/available-farmers":
                        var availUser = await GetUserFromRequestAsync(request);
                        if (availUser == null || (availUser.Value.Role != "SUPERADMIN" && availUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        if (_accountLinkingService == null)
                        {
                            await WriteJsonAsync(response, new List<AvailableFarmerDto>());
                            break;
                        }
                        var available = await _accountLinkingService.GetAvailableFarmersAsync();
                        await WriteJsonAsync(response, available);
                        break;
                    case "/api/farmer/me":
                        await HandleGetFarmerProfileAsync(request, response);
                        break;
                    case "/api/farmer/social":
                        await HandleGetFarmerSocialAsync(request, response);
                        break;
                    case "/api/farmer/quests":
                        await HandleGetFarmerQuestsAsync(request, response);
                        break;
                    case "/api/farmer/community-center":
                        await HandleGetCommunityCenterAsync(request, response);
                        break;
                    case "/api/map/tracking":
                        await HandleGetMapTrackingAsync(request, response);
                        break;
                    default:
                        await WriteNotFoundAsync(response, path);
                        break;
                }
            }
            else if (method == "POST")
            {
                switch (path)
                {
                    case "/rendering":
                        var fpsParam = request.QueryString["fps"];
                        await WriteJsonAsync(response, await HandlePostRenderingAsync(fpsParam));
                        break;
                    case "/time":
                        var timeParam = request.QueryString["value"];
                        await WriteJsonAsync(response, await HandlePostTimeAsync(timeParam));
                        break;
                    case "/clock-speed":
                        var multiplierParam = request.QueryString["multiplier"];
                        await WriteJsonAsync(
                            response,
                            await HandlePostClockSpeedAsync(multiplierParam)
                        );
                        break;
                    case "/roles/admin":
                        var adminNameParam = request.QueryString["name"];
                        var adminIdParam = request.QueryString["playerId"];
                        await WriteJsonAsync(
                            response,
                            await HandlePostGrantAdminAsync(adminNameParam, adminIdParam)
                        );
                        break;
                    case "/auth/timeout":
                        var timeoutParam = request.QueryString["value"];
                        await WriteJsonAsync(response, HandlePostAuthTimeout(timeoutParam));
                        break;
                    case "/newgame":
                        await HandlePostNewGameAsync(request, response);
                        break;
                    case "/reload":
                        await HandlePostReloadAsync(response);
                        break;
                    case "/api/auth/register":
                        await HandlePostAuthRegisterAsync(request, response);
                        break;
                    case "/api/auth/login":
                        await HandlePostAuthLoginAsync(request, response);
                        break;
                    case "/api/backup/create":
                        var newBackup = _backupService?.CreateBackup();
                        if (newBackup != null)
                        {
                            await WriteJsonAsync(response, newBackup);
                        }
                        else
                        {
                            response.StatusCode = 500;
                            await WriteJsonAsync(response, new { error = "Backup creation failed or no active save loaded" });
                        }
                        break;
                    case "/api/chat/send":
                        await HandlePostChatSendAsync(request, response);
                        break;
                    case "/api/console/execute":
                        await HandlePostConsoleExecuteAsync(request, response);
                        break;
                    case "/api/mods/policy":
                        await HandlePostModPolicyAsync(request, response);
                        break;
                    case "/api/mods/policy/sync-sve":
                        await HandlePostModPolicySyncSveAsync(request, response);
                        break;
                    case "/api/mods/upload":
                        await HandlePostModUploadAsync(request, response);
                        break;
                    case "/api/server/pause":
                    case "/api/admin/pause":
                        await HandlePostServerPauseAsync(request, response);
                        break;
                    case "/api/server/resume":
                    case "/api/admin/resume":
                        await HandlePostServerResumeAsync(request, response);
                        break;
                    case "/api/admin/approve-player":
                        var approveUser = await GetUserFromRequestAsync(request);
                        if (approveUser == null || (approveUser.Value.Role != "SUPERADMIN" && approveUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        var approveIdStr = request.QueryString["playerId"];
                        if (string.IsNullOrEmpty(approveIdStr) || !long.TryParse(approveIdStr, out long approveId))
                        {
                            response.StatusCode = 400;
                            await WriteJsonAsync(response, new { error = "Valid 'playerId' query parameter is required." });
                            break;
                        }
                        if (_passwordProtectionService == null)
                        {
                            response.StatusCode = 503;
                            await WriteJsonAsync(response, new { error = "Password protection service not active." });
                            break;
                        }
                        bool approved = false;
                        await RunOnGameThreadAsync(() =>
                        {
                            approved = _passwordProtectionService.ManuallyApprovePlayer(approveId);
                        });
                        if (approved)
                        {
                            await WriteJsonAsync(response, new { success = true, playerId = approveId, message = $"Player {approveId} manually approved and admitted to farm." });
                        }
                        else
                        {
                            response.StatusCode = 404;
                            await WriteJsonAsync(response, new { error = $"Player {approveId} not found in pending authentication list or already authenticated." });
                        }
                        break;
                    case "/api/admin/users/create":
                        var createUser = await GetUserFromRequestAsync(request);
                        if (createUser == null || (createUser.Value.Role != "SUPERADMIN" && createUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        if (_accountLinkingService == null)
                        {
                            response.StatusCode = 503;
                            await WriteJsonAsync(response, new { error = "Account linking service unavailable." });
                            break;
                        }
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            var bodyStr = await reader.ReadToEndAsync();
                            var bodyObj = JObject.Parse(bodyStr);
                            long fId = bodyObj["farmerId"]?.Value<long>() ?? 0;
                            string uName = bodyObj["username"]?.ToString()?.Trim() ?? "";
                            string uPass = bodyObj["password"]?.ToString()?.Trim() ?? "";
                            string uRole = bodyObj["role"]?.ToString()?.Trim() ?? "PLAYER";

                            var (createdOk, createdMsg) = await _accountLinkingService.CreateOrUpdateUserManualAsync(fId, uName, uPass, uRole);
                            if (createdOk)
                            {
                                await WriteJsonAsync(response, new { success = true, message = createdMsg });
                            }
                            else
                            {
                                response.StatusCode = 400;
                                await WriteJsonAsync(response, new { error = createdMsg });
                            }
                        }
                        break;
                    default:
                        await WriteNotFoundAsync(response, path);
                        break;
                }
            }
            else if (method == "DELETE")
            {
                if (path.StartsWith("/api/mods/custom/"))
                {
                    await HandleDeleteCustomModAsync(request, response);
                    return;
                }

                switch (path)
                {
                    case "/api/mods/custom":
                    case "/api/mods/custom/delete":
                        await HandleDeleteCustomModAsync(request, response);
                        break;
                    case "/farmhands":
                        var nameParam = request.QueryString["name"];
                        var farmhandIdParam = request.QueryString["playerId"];
                        await WriteJsonAsync(
                            response,
                            await HandleDeleteFarmhandAsync(nameParam, farmhandIdParam)
                        );
                        break;
                    case "/api/backup/delete":
                        var fileToDelete = request.QueryString["file"];
                        if (string.IsNullOrEmpty(fileToDelete))
                        {
                            response.StatusCode = 400;
                            await WriteJsonAsync(response, new { error = "Parameter 'file' is required" });
                            break;
                        }
                        bool deleted = _backupService?.DeleteBackup(fileToDelete) ?? false;
                        if (deleted)
                        {
                            await WriteJsonAsync(response, new { success = true, deleted = fileToDelete });
                        }
                        else
                        {
                            response.StatusCode = 404;
                            await WriteJsonAsync(response, new { error = "Backup file not found or could not be deleted" });
                        }
                        break;
                    case "/api/admin/users/delete":
                        var delUser = await GetUserFromRequestAsync(request);
                        if (delUser == null || (delUser.Value.Role != "SUPERADMIN" && delUser.Value.Role != "ADMIN"))
                        {
                            response.StatusCode = 403;
                            await WriteJsonAsync(response, new { error = "Administrator role required." });
                            break;
                        }
                        var targetIdStr = request.QueryString["farmerId"];
                        if (string.IsNullOrEmpty(targetIdStr) || !long.TryParse(targetIdStr, out long targetId))
                        {
                            response.StatusCode = 400;
                            await WriteJsonAsync(response, new { error = "Valid 'farmerId' parameter is required." });
                            break;
                        }
                        if (_accountLinkingService == null)
                        {
                            response.StatusCode = 503;
                            await WriteJsonAsync(response, new { error = "Account linking service unavailable." });
                            break;
                        }
                        var (delOk, delMsg) = await _accountLinkingService.DeleteUserAsync(targetId);
                        if (delOk)
                        {
                            await WriteJsonAsync(response, new { success = true, message = delMsg });
                        }
                        else
                        {
                            response.StatusCode = 404;
                            await WriteJsonAsync(response, new { error = delMsg });
                        }
                        break;
                    default:
                        await WriteNotFoundAsync(response, path);
                        break;
                }
            }
            else
            {
                response.StatusCode = 405;
                await WriteJsonAsync(response, new { error = "Method not allowed" });
            }
        }
        catch (TaskCanceledException)
        {
            // Game thread busy (RunOnGameThreadAsync timed out). Transient, not an error.
            Monitor.Log(
                $"Request to {request.Url?.AbsolutePath} timed out waiting for game thread",
                LogLevel.Debug
            );
            try
            {
                response.StatusCode = 503;
                await WriteJsonAsync(
                    response,
                    new
                    {
                        error = "Game thread is blocked (likely a day transition or save sync) and cannot process requests right now. Retry after a few seconds.",
                        retry = true,
                    }
                );
            }
            catch (Exception writeEx)
            {
                Monitor.Log($"Failed to write error response: {writeEx}", LogLevel.Warn);
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"Error handling request: {ex}", LogLevel.Warn);
            try
            {
                response.StatusCode = 500;
                await WriteJsonAsync(response, new { error = "Internal server error" });
            }
            catch (Exception writeEx)
            {
                Monitor.Log($"Failed to write error response: {writeEx}", LogLevel.Warn);
            }
        }
        finally
        {
            // Server-side mirror of the client's http_request event. Lets
            // test-side analysis compare "time the request spent on the
            // server" against "total time observed by the client" — a
            // large delta fingerprints network/container latency.
            try
            {
                _httpServedStopwatch.Stop();
                int statusCode;
                try
                {
                    statusCode = response.StatusCode;
                }
                catch
                {
                    statusCode = 0;
                } // response already closed/disposed
                Diagnostics.ModEventLog.Emit(
                    "http_served",
                    new
                    {
                        method = _servedMethod,
                        path = _servedPath,
                        status = statusCode,
                        durationMs = _httpServedStopwatch.ElapsedMilliseconds,
                    }
                );
            }
            catch
            { /* never let instrumentation fail a request close */
            }

            // TODO: Don't close/write to the response for WebSocket requests (here and above)
            //       It's already been consumed/disposed by the WebSocket upgrade handshake.
            try
            {
                response.Close();
            }
            catch (Exception closeEx)
            {
                Monitor.Log($"Failed to close response: {closeEx}", LogLevel.Debug);
            }
        }
    }

    #region WebSocket

    /// <summary>
    /// Broadcasts a chat message to all connected WebSocket clients.
    /// </summary>
    public void BroadcastChatMessage(string playerName, string message, string source = "in-game")
    {
        AddChatMessage(playerName, message, source);

        var wsMessage = new WebSocketMessage
        {
            Type = "chat",
            Payload = JObject.FromObject(
                new ChatEventPayload
                {
                    PlayerName = playerName,
                    Message = message,
                    Timestamp = DateTime.UtcNow.ToString("o"),
                }
            ),
        };

        BroadcastToAllClients(wsMessage);
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        WebSocket? ws = null;
        var isAuthenticated = false;
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            ws = wsContext.WebSocket;

            Monitor.Log(
                "[API] WebSocket client connected, awaiting authentication",
                LogLevel.Debug
            );

            // If auth is disabled, auto-authenticate and add to clients
            if (!_authEnabled)
            {
                isAuthenticated = true;
                lock (_wsLock)
                {
                    _wsClients.Add(ws);
                }
                Monitor.Log("[API] WebSocket client authenticated (auth disabled)", LogLevel.Debug);
            }

            isAuthenticated = await ProcessWebSocketAsync(ws, isAuthenticated);
        }
        catch (Exception ex)
        {
            Monitor.Log($"[API] WebSocket error: {ex.Message}", LogLevel.Warn);
        }
        finally
        {
            if (ws != null)
            {
                if (isAuthenticated)
                {
                    lock (_wsLock)
                    {
                        _wsClients.Remove(ws);
                    }
                }
                try
                {
                    ws.Dispose();
                }
                catch (Exception disposeEx)
                {
                    Diagnostics.ModEventLog.Emit(
                        "exception_swallowed",
                        new
                        {
                            location = "ApiService.HandleWebSocketAsync.dispose",
                            exceptionType = disposeEx.GetType().Name,
                            message = disposeEx.Message,
                        }
                    );
                }
                Monitor.Log("[API] WebSocket client disconnected", LogLevel.Debug);
            }
        }
    }

    private async Task<bool> ProcessWebSocketAsync(WebSocket ws, bool isAuthenticated)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();
        const int maxMessageSize = 16384; // 16KB max message size
        const int authTimeoutMs = 10000; // 10 seconds to authenticate

        // If auth is required, wait for auth message with timeout
        if (_authEnabled && !isAuthenticated)
        {
            using var authCts = new CancellationTokenSource(authTimeoutMs);
            try
            {
                var authResult = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    authCts.Token
                );
                if (authResult.MessageType == WebSocketMessageType.Text && authResult.EndOfMessage)
                {
                    var authJson = Encoding.UTF8.GetString(buffer, 0, authResult.Count);
                    var authMsg = JsonConvert.DeserializeObject<WebSocketMessage>(authJson);

                    if (
                        authMsg?.Type == "auth"
                        && authMsg.Payload?["token"]?.ToString() == Env.ApiKey
                    )
                    {
                        isAuthenticated = true;
                        lock (_wsLock)
                        {
                            _wsClients.Add(ws);
                        }
                        await SendWebSocketMessageAsync(ws, new { type = "auth_success" });
                        Monitor.Log("[API] WebSocket client authenticated", LogLevel.Debug);
                    }
                    else
                    {
                        await SendWebSocketMessageAsync(
                            ws,
                            new { type = "auth_failed", error = "Invalid token" }
                        );
                        await ws.CloseAsync(
                            WebSocketCloseStatus.PolicyViolation,
                            "Authentication failed",
                            CancellationToken.None
                        );
                        Monitor.Log(
                            "[API] WebSocket client authentication failed - invalid token",
                            LogLevel.Warn
                        );
                        return false;
                    }
                }
                else
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.PolicyViolation,
                        "Expected auth message",
                        CancellationToken.None
                    );
                    Monitor.Log(
                        "[API] WebSocket client authentication failed - unexpected message type",
                        LogLevel.Warn
                    );
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                await SendWebSocketMessageAsync(
                    ws,
                    new { type = "auth_failed", error = "Authentication timeout" }
                );
                try
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.PolicyViolation,
                        "Authentication timeout",
                        CancellationToken.None
                    );
                }
                catch (Exception closeEx)
                {
                    Diagnostics.ModEventLog.Emit(
                        "exception_swallowed",
                        new
                        {
                            location = "ApiService.ProcessWebSocketAsync.authTimeoutClose",
                            exceptionType = closeEx.GetType().Name,
                            message = closeEx.Message,
                        }
                    );
                }
                Monitor.Log("[API] WebSocket client authentication timeout", LogLevel.Warn);
                return false;
            }
        }

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                    );
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    // Check if message is too large
                    if (messageBuilder.Length > maxMessageSize)
                    {
                        Monitor.Log("[API] WebSocket message too large, discarding", LogLevel.Warn);
                        messageBuilder.Clear();
                        continue;
                    }

                    // Process complete messages only
                    if (result.EndOfMessage)
                    {
                        var json = messageBuilder.ToString();
                        messageBuilder.Clear();
                        await HandleWebSocketMessageAsync(ws, json);
                    }
                }
            }
            catch (WebSocketException)
            {
                // Client disconnected
                break;
            }
        }

        return isAuthenticated;
    }

    private async Task HandleWebSocketMessageAsync(WebSocket ws, string json)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<WebSocketMessage>(json);
            if (msg == null)
            {
                return;
            }

            switch (msg.Type)
            {
                case "ping":
                    await SendWebSocketMessageAsync(ws, new { type = "pong" });
                    break;

                case "chat_send":
                    if (msg.Payload != null)
                    {
                        var chatPayload = msg.Payload.ToObject<ChatSendPayload>();
                        if (
                            chatPayload != null
                            && !string.IsNullOrWhiteSpace(chatPayload.Author)
                            && !string.IsNullOrWhiteSpace(chatPayload.Message)
                        )
                        {
                            // Queue on game thread - SendPublicMessage iterates Game1.otherFarmers
                            // which could throw if a player connects/disconnects during iteration
                            var author = chatPayload.Author;
                            var message = chatPayload.Message;
                            await RunOnGameThreadAsync(() =>
                                OnExternalChatMessage?.Invoke(author, message)
                            );
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"[API] Invalid WebSocket message: {ex.Message}", LogLevel.Debug);
        }
    }

    private async Task SendWebSocketMessageAsync(WebSocket ws, object message)
    {
        if (ws.State != WebSocketState.Open)
        {
            return;
        }

        var json = JsonConvert.SerializeObject(message, JsonSettings);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        try
        {
            await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // Client disconnected
        }
    }

    private void BroadcastToAllClients(object message)
    {
        var json = JsonConvert.SerializeObject(message, JsonSettings);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        List<WebSocket> clients;
        lock (_wsLock)
        {
            clients = _wsClients.ToList();
        }

        foreach (var ws in clients)
        {
            if (ws.State == WebSocketState.Open)
            {
                // Fire and forget - don't block on individual sends
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ws.SendAsync(
                            segment,
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                    catch (WebSocketException)
                    {
                        // Client disconnected, will be cleaned up by periodic cleanup
                    }
                });
            }
        }
    }

    private void CleanupDeadWebSocketClients()
    {
        List<WebSocket> deadClients;
        lock (_wsLock)
        {
            deadClients = _wsClients.Where(ws => ws.State != WebSocketState.Open).ToList();

            foreach (var ws in deadClients)
            {
                _wsClients.Remove(ws);
            }
        }

        if (deadClients.Count > 0)
        {
            Monitor.Log(
                $"[API] Cleaned up {deadClients.Count} dead WebSocket client(s)",
                LogLevel.Debug
            );
        }

        foreach (var ws in deadClients)
        {
            try
            {
                ws.Dispose();
            }
            catch (Exception disposeEx)
            {
                Diagnostics.ModEventLog.Emit(
                    "exception_swallowed",
                    new
                    {
                        location = "ApiService.BroadcastToAllClients.deadClientDispose",
                        exceptionType = disposeEx.GetType().Name,
                        message = disposeEx.Message,
                    }
                );
            }
        }
    }

    #endregion

    #region Endpoint Handlers

    /// <summary>
    /// Returns server status from the periodic snapshot. Never blocks on the game thread.
    /// Invite code derivation and version are computed per-request (both thread-safe).
    /// Game state fields come from <see cref="TakeGameStateSnapshot"/>.
    /// </summary>
    [ApiEndpoint("GET", "/status", Summary = "Get server status", Tag = "Server", Public = true)]
    [ApiResponse(typeof(ServerStatus), 200, Description = "Server status and game state")]
    private ServerStatus HandleGetStatus() => HandleGetStatus(_snapshot);

    /// <summary>
    /// Builds the <c>/status</c> response from <paramref name="snap"/>. The wait endpoints pass
    /// the snapshot that satisfied their filter so the body never reflects a later publication
    /// that no longer matches.
    /// </summary>
    private ServerStatus HandleGetStatus(GameStateSnapshot snap)
    {
        var modInfo = Helper.ModRegistry.Get("JunimoHost.Server");
        var version = modInfo?.Manifest?.Version?.ToString() ?? "unknown";
        var tpsSeconds = _tpsSeconds.Average;
        var tps = tpsSeconds > 0 ? Math.Round(_tpsTicks.Average / tpsSeconds, 1) : 0;

        // The invite code mirrors the live Galaxy lobby (the mod's static properties, not the file).
        // It is shown whenever a lobby exists; SteamRelayReady reports whether Steam clients can use it
        // right now. The G-code is never exposed.
        // One snapshot of the signals feeds both the raw fields and the derived code, so a response
        // never carries a code that disagrees with its own signals.
        var inviteCode = InviteCodes.Joinable;
        var steamRelayReady = GalaxyAuthService.SteamLobbyPublished;
        var galaxyLobby = GalaxyAuthService.GalaxyLobby;
        var steamSession = SteamGameServerService.SteamSession;
        var galaxyLobbyState = galaxyLobby.ToWire();
        var authReadiness = GalaxyAuthService.AuthTokenHealth.ToWire();
        var steamSessionState = steamSession.ToWire();
        var connectionStatusCode = ConnectionStatus
            .Compute(inviteCode != null, steamRelayReady, galaxyLobby, steamSession)
            .ToWire();

        if (!snap.IsOnline)
        {
            return new ServerStatus
            {
                PlayerCount = 0,
                MaxPlayers = 4,
                ServerVersion = version,
                GameVersion = snap.GameVersion,
                IsOnline = false,
                IsReady = false,
                DayTransitionComplete = false,
                LastUpdated = snap.CapturedAt,
                ServerName = Env.ServerName,
                StartedAtUtc = ServerCommand.StartTimeUtc?.ToString("o"),
                Tps = tps,
                Sequence = snap.Sequence,
                InviteCode = inviteCode,
                SteamRelayReady = steamRelayReady,
                GalaxyLobbyState = galaxyLobbyState,
                SteamSessionState = steamSessionState,
                AuthReadiness = authReadiness,
                ConnectionStatusCode = connectionStatusCode,
            };
        }

        return new ServerStatus
        {
            PlayerCount = snap.PlayerCount,
            MaxPlayers = snap.MaxPlayers,
            InviteCode = inviteCode,
            SteamRelayReady = steamRelayReady,
            GalaxyLobbyState = galaxyLobbyState,
            SteamSessionState = steamSessionState,
            AuthReadiness = authReadiness,
            ConnectionStatusCode = connectionStatusCode,
            ServerVersion = version,
            GameVersion = snap.GameVersion,
            IsOnline = true,
            IsReady = snap.IsReady,
            DayTransitionComplete = snap.DayTransitionComplete,
            LastUpdated = snap.CapturedAt,
            FarmName = snap.FarmName,
            ServerName = Env.ServerName,
            StartedAtUtc = ServerCommand.StartTimeUtc?.ToString("o"),
            Day = snap.Day,
            Season = snap.Season,
            Year = snap.Year,
            TimeOfDay = snap.TimeOfDay,
            FarmTypeKey = snap.FarmTypeKey,
            IsPaused = snap.IsPaused,
            IsManuallyPaused = _pauseService?.IsManuallyPaused ?? false,
            PauseReason = _pauseService?.PauseReason ?? "",
            PausedBy = _pauseService?.PausedBy ?? "",
            PauseRemainingSeconds = _pauseService?.GetRemainingSeconds() ?? 0,
            Weather = snap.Weather,
            WeatherTomorrow = snap.WeatherTomorrow,
            Tps = tps,
            Sequence = snap.Sequence,
        };
    }

    /// <summary>Reads from periodic snapshot. See <see cref="TakeGameStateSnapshot"/>.</summary>
    [ApiEndpoint("GET", "/players", Summary = "Get connected players", Tag = "Server")]
    [ApiResponse(typeof(PlayersResponse), 200, Description = "List of connected players")]
    private PlayersResponse HandleGetPlayers() => HandleGetPlayers(_snapshot);

    private static PlayersResponse HandleGetPlayers(GameStateSnapshot snap)
    {
        return new PlayersResponse { Players = snap.Players, Sequence = snap.Sequence };
    }

    /// <summary>
    /// Ground-truth dump of live game-engine state for test failure analysis.
    /// Reads every field under an individual try/catch so one bad reflection
    /// site does not fail the whole response. Always returns 200 with
    /// <c>failedFields</c> listing any sub-reads that threw.
    ///
    /// Unlike <c>/status</c> / <c>/players</c> / <c>/farmhands</c> this is
    /// NOT served from the periodic snapshot — it reads directly from
    /// <c>Game1</c> so the caller sees the instantaneous state, which is
    /// the whole point on a timeout. Kept cheap (&lt;1 ms) so it is safe
    /// to call from every failure path.
    /// </summary>
    [ApiEndpoint(
        "GET",
        "/diagnostics/state",
        Summary = "Live game-engine state snapshot (test harness)",
        Tag = "Diagnostics"
    )]
    [ApiResponse(typeof(DiagnosticsStateResponse), 200)]
    private async Task<DiagnosticsStateResponse> HandleGetDiagnosticsStateAsync(
        HttpListenerRequest? request = null
    )
    {
        var resp = new DiagnosticsStateResponse { CapturedAt = DateTime.UtcNow.ToString("o") };
        // Optional ?masterFlag=<id> — when present, MasterHasFlag reports whether the master's
        // mailReceived contains it (boolean, never the flag list). Used by save-import Test 7.
        var masterFlag = request?.QueryString["masterFlag"];
        // Optional ?masterEvent=<id> — MasterHasEvent reports whether the master's eventsSeen contains
        // it (a distinct collection from mailReceived). Used by save-import Test 7.
        var masterEvent = request?.QueryString["masterEvent"];
        // Optional ?masterFriendKey=<npc> — MasterShadowFriendshipPoints reports that NPC's specific
        // friendship points (not a max-over-all). Used by save-import Test 7c.
        var masterFriendKey = request?.QueryString["masterFriendKey"];

        // Phase 1 — primitive / thread-safe reads. These are value-type
        // snapshots or use cached Interlocked counters; safe from the HTTP
        // thread. Each block is independent; a failure only disables its
        // own field.
        TryRead(
            "timeOfDay",
            resp.FailedFields,
            () =>
            {
                resp.TimeOfDay = Game1.timeOfDay;
            }
        );
        TryRead(
            "dayOfMonth",
            resp.FailedFields,
            () =>
            {
                resp.DayOfMonth = Game1.dayOfMonth;
            }
        );
        TryRead(
            "season",
            resp.FailedFields,
            () =>
            {
                resp.Season = Game1.currentSeason ?? "";
            }
        );
        TryRead(
            "year",
            resp.FailedFields,
            () =>
            {
                resp.Year = Game1.year;
            }
        );
        TryRead(
            "gameMode",
            resp.FailedFields,
            () =>
            {
                resp.GameMode = Game1.gameMode;
            }
        );
        TryRead(
            "lastTickMs",
            resp.FailedFields,
            () =>
            {
                var tickTicks = Interlocked.Read(ref _lastTickTimestamp);
                resp.LastTickMs =
                    tickTicks > 0
                        ? (long)
                            (
                                DateTime.UtcNow - new DateTime(tickTicks, DateTimeKind.Utc)
                            ).TotalMilliseconds
                        : (long?)null;
            }
        );
        TryRead(
            "avgGameThreadWaitMs",
            resp.FailedFields,
            () =>
            {
                resp.AvgGameThreadWaitMs = _avgGameThreadWaitMs.Average;
            }
        );
        TryRead(
            "saveImportFinalizeCount",
            resp.FailedFields,
            () =>
            {
                resp.SaveImportFinalizeCount = CabinManagerService.SaveImportFinalizeCount;
            }
        );

        // Phase 2 — reads that touch Netcode collections or game-thread
        // mutable state. Marshalled to the game thread so we don't tear
        // enumerations that the tick loop is actively mutating. A single
        // marshal covers every unsafe block; one short hop is cheaper than
        // six. 3 s budget — tight enough to fail fast if the game thread
        // is stuck (the very condition the caller is diagnosing), loose
        // enough to absorb a legitimate save-in-progress hiccup.
        try
        {
            await RunOnGameThreadAsync(
                () =>
                {
                    TryRead(
                        "otherFarmerUids",
                        resp.FailedFields,
                        () =>
                        {
                            resp.OtherFarmerUids =
                                Game1.otherFarmers?.Keys?.ToArray() ?? System.Array.Empty<long>();
                        }
                    );

                    TryRead(
                        "onlineFarmerCount",
                        resp.FailedFields,
                        () =>
                        {
                            resp.OnlineFarmerCount = Game1.getOnlineFarmers()?.Count() ?? 0;
                        }
                    );

                    TryRead(
                        "netReady",
                        resp.FailedFields,
                        () =>
                        {
                            resp.NetReady = ReadNetReadyState();
                        }
                    );

                    TryRead(
                        "newDaySync",
                        resp.FailedFields,
                        () =>
                        {
                            resp.NewDaySync = ReadNewDaySyncState();
                        }
                    );

                    TryRead(
                        "activeClickableMenu",
                        resp.FailedFields,
                        () =>
                        {
                            resp.ActiveClickableMenu = Game1.activeClickableMenu?.GetType().Name;
                        }
                    );

                    TryRead(
                        "isGameAvailable",
                        resp.FailedFields,
                        () =>
                        {
                            resp.IsGameAvailable = Game1.server?.isGameAvailable();
                        }
                    );

                    TryRead(
                        "cabins",
                        resp.FailedFields,
                        () =>
                        {
                            resp.Cabins = ReadCabinDiagnostics();
                        }
                    );

                    TryRead(
                        "farmhandData",
                        resp.FailedFields,
                        () =>
                        {
                            resp.FarmhandData = ReadFarmhandDiagnostics();
                        }
                    );

                    TryRead(
                        "disconnectingFarmers",
                        resp.FailedFields,
                        () =>
                        {
                            resp.DisconnectingFarmers = ReadDisconnectingFarmers();
                        }
                    );

                    // Save-import probes: host FarmHouse content/NPC counts (must be 0 after a swap
                    // move) and master-gated world-state on the blank Server master.
                    TryRead(
                        "farmHouseContents",
                        resp.FailedFields,
                        () =>
                        {
                            var fh = Game1.getLocationFromName("FarmHouse") as FarmHouse;
                            resp.FarmHouseObjectCount = fh?.objects?.Count() ?? 0;
                            resp.FarmHouseFurnitureCount = fh?.furniture?.Count ?? 0;
                            resp.FarmHouseHasPlayerBed = fh?.GetPlayerBed() != null;
                            resp.FarmHouseFridgeItemCount =
                                fh?.fridge?.Value?.Items?.Count(i => i != null) ?? 0;
                            var masterCellar = Game1.getLocationFromName("Cellar");
                            resp.MasterCellarObjectCount = masterCellar?.objects?.Count() ?? 0;
                        }
                    );

                    TryRead(
                        "hostLocation",
                        resp.FailedFields,
                        () =>
                        {
                            resp.HostLocation = Game1.currentLocation?.NameOrUniqueName ?? "";
                        }
                    );

                    TryRead(
                        "masterState",
                        resp.FailedFields,
                        () =>
                        {
                            var master = Game1.MasterPlayer;
                            if (master == null)
                            {
                                return;
                            }
                            resp.MasterName = master.Name ?? "";
                            if (!string.IsNullOrEmpty(masterFlag))
                            {
                                resp.MasterHasFlag =
                                    master.mailReceived?.Contains(masterFlag) ?? false;
                            }
                            if (!string.IsNullOrEmpty(masterEvent))
                            {
                                resp.MasterHasEvent =
                                    master.eventsSeen?.Contains(masterEvent) ?? false;
                            }
                            resp.MasterCaveChoice = master.caveChoice?.Value ?? 0;
                            resp.MasterDaysPlayed = (int)(master.stats?.DaysPlayed ?? 0);
                            resp.MasterHasSpouse = !string.IsNullOrEmpty(master.spouse);
                            // Read the SPECIFIC keyed friendship the test seeded (the Dark-Shrine gate
                            // keys on a particular NPC), not a max-over-all — a max could false-pass if
                            // any other NPC happened to be high while the seeded carry was lost.
                            if (
                                !string.IsNullOrEmpty(masterFriendKey)
                                && master.friendshipData != null
                                && master.friendshipData.TryGetValue(masterFriendKey, out var keyed)
                                && keyed != null
                            )
                            {
                                resp.MasterShadowFriendshipPoints = keyed.Points;
                            }
                        }
                    );
                },
                timeoutMs: 3000
            );
        }
        catch (TaskCanceledException)
        {
            // Game thread stuck. Mark all marshalled fields as failed so
            // the caller knows *why* these blocks are empty (distinct from
            // "no data available"). Still return 200 with whatever Phase 1
            // collected — that's already a strong diagnostic signal for
            // "the game thread is unresponsive", which is the core reason
            // /diagnostics/state exists.
            resp.FailedFields.Add("gameThreadTimeout");
        }

        return resp;
    }

    private List<DiagnosticsCabinState> ReadCabinDiagnostics()
    {
        var list = new List<DiagnosticsCabinState>();
        var farm = Game1.getFarm();
        if (farm == null)
        {
            return list;
        }

        foreach (var building in farm.buildings)
        {
            try
            {
                if (!building.isCabin)
                {
                    continue;
                }

                var cabin = building.GetIndoors<StardewValley.Locations.Cabin>();
                var owner = cabin?.owner;
                Auth.FarmhandOwnerRecord ownerRecord = null;
                if (owner != null)
                {
                    _farmhandOwnership.TryGetOwner(owner.UniqueMultiplayerID, out ownerRecord);
                }
                list.Add(
                    new DiagnosticsCabinState
                    {
                        TileX = building.tileX.Value,
                        TileY = building.tileY.Value,
                        IndoorsName = cabin?.NameOrUniqueName ?? "",
                        OwnerId = owner?.UniqueMultiplayerID ?? 0,
                        OwnerName = owner?.Name ?? "",
                        OwnerIsCustomized = owner?.isCustomized?.Value ?? false,
                        OwnerHasUserId = !string.IsNullOrEmpty(owner?.userID?.Value),
                        OwnerHasOwner = ownerRecord != null,
                        OwnerPlatform = ownerRecord?.Platform ?? "",
                        HomeLocationOfOwner = owner?.homeLocation?.Value ?? "",
                        FarmhandReferenceDefined =
                            cabin?.farmhandReference?.defined?.Value ?? false,
                        FarmhandReferenceUid = cabin?.farmhandReference?.uid?.Value ?? 0,
                        ObjectCount = cabin?.objects?.Count() ?? 0,
                        FridgeItemCount = cabin?.fridge?.Value?.Items?.Count(i => i != null) ?? 0,
                        PetCount =
                            cabin?.characters?.Count(c => c is StardewValley.Characters.Pet) ?? 0,
                        CellarObjectCount = ReadCabinCellarObjectCount(cabin),
                    }
                );
            }
            catch
            { /* skip single-cabin read failures; the dump is best-effort */
            }
        }
        return list;
    }

    /// <summary>
    /// Object count in the cellar the engine assigned to <paramref name="cabin"/>'s owner, resolved
    /// the engine's own way (<c>Cabin.GetCellarName()</c> → <c>cellarAssignments</c> keyed on the
    /// owner UID). 0 when there's no owner or no assigned cellar.
    /// </summary>
    private static int ReadCabinCellarObjectCount(StardewValley.Locations.Cabin? cabin)
    {
        var cellarName = cabin?.GetCellarName();
        if (string.IsNullOrEmpty(cellarName))
        {
            return 0;
        }
        var cellar = Game1.getLocationFromName(cellarName);
        return cellar?.objects?.Count() ?? 0;
    }

    private List<DiagnosticsFarmhandState> ReadFarmhandDiagnostics()
    {
        var list = new List<DiagnosticsFarmhandState>();
        var farmhandData = Game1.netWorldState?.Value?.farmhandData;
        if (farmhandData == null)
        {
            return list;
        }

        foreach (var kv in farmhandData.Pairs)
        {
            try
            {
                var f = kv.Value;
                if (f == null)
                {
                    continue;
                }

                Auth.FarmhandOwnerRecord ownerRecord = null;
                _farmhandOwnership.TryGetOwner(f.UniqueMultiplayerID, out ownerRecord);
                list.Add(
                    new DiagnosticsFarmhandState
                    {
                        UniqueMultiplayerId = f.UniqueMultiplayerID,
                        Name = f.Name ?? "",
                        IsCustomized = f.isCustomized?.Value ?? false,
                        HomeLocation = f.homeLocation?.Value ?? "",
                        LastSleepLocation = f.lastSleepLocation?.Value ?? "",
                        HasUserId = !string.IsNullOrEmpty(f.userID?.Value),
                        HasOwner = ownerRecord != null,
                        OwnerPlatform = ownerRecord?.Platform ?? "",
                        Released = _farmhandOwnership.IsReleased(f.UniqueMultiplayerID),
                    }
                );
            }
            catch
            { /* per-entry failure tolerated */
            }
        }
        return list;
    }

    private static System.Reflection.FieldInfo? _disconnectingFarmersField;
    private static System.Reflection.FieldInfo? _game1MultiplayerField;

    private static long[] ReadDisconnectingFarmers()
    {
        // Game1.multiplayer is non-public; resolve via reflection the same way
        // ModHelperExtensions.GetMultiplayer does.
        var mpField = _game1MultiplayerField ??= typeof(Game1).GetField(
            "multiplayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        if (mpField == null)
        {
            return System.Array.Empty<long>();
        }

        var mp = mpField.GetValue(null) as Multiplayer;
        if (mp == null)
        {
            return System.Array.Empty<long>();
        }

        var fi = _disconnectingFarmersField ??= typeof(Multiplayer).GetField(
            "disconnectingFarmers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        if (fi == null)
        {
            return System.Array.Empty<long>();
        }

        if (fi.GetValue(mp) is not System.Collections.Generic.IEnumerable<long> ids)
        {
            return System.Array.Empty<long>();
        }

        return ids.ToArray();
    }

    private static void TryRead(string fieldName, List<string> failedFields, System.Action read)
    {
        try
        {
            read();
        }
        catch (Exception)
        {
            failedFields.Add(fieldName);
        }
    }

    // FieldInfo for ReadySynchronizer.ReadyChecks (private). Cached once at
    // first use; reflection lookup cost is negligible after caching.
    private static System.Reflection.FieldInfo? _readyChecksField;

    private static List<ReadyCheckState> ReadNetReadyState()
    {
        var list = new List<ReadyCheckState>();
        var netReady = Game1.netReady;
        if (netReady == null)
        {
            return list;
        }

        var fi = _readyChecksField ??=
            typeof(StardewValley.Network.NetReady.ReadySynchronizer).GetField(
                "ReadyChecks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
        if (fi == null)
        {
            return list;
        }

        if (fi.GetValue(netReady) is not System.Collections.IDictionary dict)
        {
            return list;
        }

        foreach (System.Collections.DictionaryEntry kv in dict)
        {
            var id = kv.Key?.ToString() ?? "";
            var check = kv.Value;
            if (check == null)
            {
                continue;
            }

            // BaseReadyCheck has public NumberReady/NumberRequired/IsReady/State.
            // State enum values: NotReady, Ready, Locked — we surface IsLocked
            // as State==Locked. Read each defensively.
            int numberReady = 0,
                numberRequired = 0;
            bool isReady = false,
                isLocked = false;
            try
            {
                numberReady = (int)(
                    check.GetType().GetProperty("NumberReady")?.GetValue(check) ?? 0
                );
            }
            catch { }
            try
            {
                numberRequired = (int)(
                    check.GetType().GetProperty("NumberRequired")?.GetValue(check) ?? 0
                );
            }
            catch { }
            try
            {
                isReady = (bool)(check.GetType().GetProperty("IsReady")?.GetValue(check) ?? false);
            }
            catch { }
            try
            {
                var state = check.GetType().GetProperty("State")?.GetValue(check);
                isLocked = state != null && state.ToString() == "Locked";
            }
            catch { }

            list.Add(
                new ReadyCheckState
                {
                    Id = id,
                    NumberReady = numberReady,
                    NumberRequired = numberRequired,
                    IsReady = isReady,
                    IsLocked = isLocked,
                }
            );
        }

        return list;
    }

    private static NewDaySyncState ReadNewDaySyncState()
    {
        var s = new NewDaySyncState();
        var sync = Game1.newDaySync;
        if (sync == null)
        {
            return s;
        }

        // hasInstance() / hasStarted() / hasFinished() are public on NewDaySynchronizer.
        try
        {
            s.HasStarted = sync.hasStarted();
        }
        catch { }
        try
        {
            s.HasFinished = sync.hasFinished();
        }
        catch { }
        try
        {
            s.IsActive = sync.hasInstance();
        }
        catch { }
        return s;
    }

    [ApiEndpoint("GET", "/health", Summary = "Health check", Tag = "Health", Public = true)]
    [ApiResponse(typeof(HealthResponse), 200, Description = "Health status")]
    private HealthResponse HandleGetHealth()
    {
        // Read game thread liveness without RunOnGameThreadAsync. /health must respond even when stuck.
        var tickTicks = Interlocked.Read(ref _lastTickTimestamp);
        long? lastTickMs =
            tickTicks > 0
                ? (long)
                    (DateTime.UtcNow - new DateTime(tickTicks, DateTimeKind.Utc)).TotalMilliseconds
                : null;

        var pendingActions = _dispatcher.PendingCount;

        bool? gameAvailable = null;
        try
        {
            gameAvailable = Game1.server?.isGameAvailable();
        }
        catch (Exception ex)
        {
            // This is the single most important exception swallow in the
            // mod: /health is supposed to REPORT this condition, and the
            // prior `catch { }` was masking the very failure it exists to
            // surface. Emit a structured event so a broken /health is
            // correlatable across containers, but keep the health
            // response going (with gameAvailable left null) so the
            // endpoint itself never 500s.
            Diagnostics.ModEventLog.Emit(
                "exception_swallowed",
                new
                {
                    location = "ApiService.HandleGetHealth.isGameAvailable",
                    exceptionType = ex.GetType().Name,
                    message = ex.Message,
                }
            );
        }

        var isFrozen = lastTickMs is null or > HealthFrozenThresholdMs;
        var status = isFrozen ? "degraded" : "ok";
        var totalTicks = Interlocked.Read(ref _totalTickCount);

        return new HealthResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow.ToString("o"),
            LastTickMs = lastTickMs,
            PendingActions = pendingActions,
            GameAvailable = gameAvailable,
            TickCount = totalTicks,
            IsFrozen = isFrozen,
            // Body-only joinability summary; Status above stays tick-liveness only (200 for alive).
            SteamSessionState = SteamGameServerService.SteamSession.ToWire(),
            GalaxyLobbyState = GalaxyAuthService.GalaxyLobby.ToWire(),
            SteamRelayReady = GalaxyAuthService.SteamLobbyPublished,
            InviteCodePresent = InviteCodes.Joinable != null,
            AuthReadiness = GalaxyAuthService.AuthTokenHealth.ToWire(),
        };
    }

    // Server-side hard cap on /wait/* timeouts. Long-poll handlers run on
    // the HttpListener thread pool; an unbounded await would starve under
    // load. 10s is well below any client read timeout and leaves plenty of
    // room above the snapshot's 1 Hz refresh cadence.
    private static readonly TimeSpan WaitMaxTimeout = TimeSpan.FromSeconds(10);

    // Threshold for considering the game thread frozen. /health reports
    // status="degraded" and IsFrozen=true when the gap since _lastTickTimestamp
    // exceeds this; /wait/health's ready=true predicate uses the inverse.
    private const long HealthFrozenThresholdMs = 5000;

    /// <summary>
    /// Long-poll variant of <c>/status</c>. Query params:
    /// <list type="bullet">
    /// <item><c>since=N</c> — last observed snapshot sequence. Required.</item>
    /// <item><c>isReady=true|false</c> — wait until the snapshot's IsReady matches.</item>
    /// <item><c>isPaused=true|false</c> — wait until the snapshot's IsPaused matches.</item>
    /// <item><c>day=N</c> — wait until the snapshot's Day matches.</item>
    /// <item><c>playerCount=N</c> — wait until the snapshot's PlayerCount equals N.</item>
    /// <item><c>timeout=ms</c> — bounded by <see cref="WaitMaxTimeout"/>.</item>
    /// </list>
    /// Returns 200 + current /status response when filters match a newer
    /// snapshot, or 408 if the timeout elapses with no match.
    /// </summary>
    [ApiEndpoint("GET", "/wait/status", Summary = "Long-poll server status", Tag = "Server")]
    [ApiResponse(typeof(ServerStatus), 200, Description = "Server status once the filters match")]
    [ApiResponse(typeof(void), 408, Description = "Timeout elapsed with no match")]
    private async Task HandleWaitStatusAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        var qs = request.QueryString;
        var since = ParseLong(qs["since"]) ?? 0;
        var isReadyFilter = ParseBool(qs["isReady"]);
        var dayTransitionCompleteFilter = ParseBool(qs["dayTransitionComplete"]);
        var isPausedFilter = ParseBool(qs["isPaused"]);
        var dayFilter = ParseInt(qs["day"]);
        var playerCountFilter = ParseInt(qs["playerCount"]);
        var timeout = ResolveWaitTimeout(qs["timeout"]);

        bool Matches(GameStateSnapshot snap)
        {
            if (snap.Sequence <= since)
            {
                return false;
            }

            if (isReadyFilter is bool b && snap.IsReady != b)
            {
                return false;
            }

            if (dayTransitionCompleteFilter is bool dtc && snap.DayTransitionComplete != dtc)
            {
                return false;
            }

            if (isPausedFilter is bool ip && snap.IsPaused != ip)
            {
                return false;
            }

            if (dayFilter is int d && snap.Day != d)
            {
                return false;
            }

            if (playerCountFilter is int pc && snap.PlayerCount != pc)
            {
                return false;
            }

            return true;
        }

        var matched = await WaitForSnapshotAsync(Matches, timeout);
        if (matched == null)
        {
            response.StatusCode = 408;
            return;
        }

        // Predicate-transition time: latest of the filtered fields'
        // change-times. The predicate became satisfiable the moment the
        // last-to-change contributing field flipped to its predicate-
        // satisfying value, which is the max across the filters in play.
        // Version-only / unfiltered case has no field to report; the
        // header is omitted and the harness falls back to snapshot-age.
        var changedAt = default(DateTime);
        if (isReadyFilter is not null)
        {
            changedAt = MaxUtc(changedAt, matched.IsReadyChangedAtUtc);
        }

        if (dayTransitionCompleteFilter is not null)
        {
            changedAt = MaxUtc(changedAt, matched.DayTransitionCompleteChangedAtUtc);
        }

        if (isPausedFilter is not null)
        {
            changedAt = MaxUtc(changedAt, matched.IsPausedChangedAtUtc);
        }

        if (dayFilter is not null)
        {
            changedAt = MaxUtc(changedAt, matched.DayChangedAtUtc);
        }

        if (playerCountFilter is not null)
        {
            changedAt = MaxUtc(changedAt, matched.PlayerCountChangedAtUtc);
        }

        EmitPredicateChangedAtHeader(response, changedAt);

        // Same response shape as /status (caller's deserializer doesn't need a
        // separate type), built from the snapshot the predicate matched: the
        // caller treats a 200 as "filter satisfied", so a re-read of _snapshot
        // could hand back a later publication that no longer matches.
        await WriteJsonAsync(response, HandleGetStatus(matched));
    }

    private static DateTime MaxUtc(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>
    /// Long-poll variant of <c>/players</c>. Query params:
    /// <list type="bullet">
    /// <item><c>since=N</c> — last observed snapshot sequence.</item>
    /// <item><c>playerId=N</c> — wait until the snapshot's Players list contains the id.</item>
    /// <item><c>timeout=ms</c> — bounded by <see cref="WaitMaxTimeout"/>.</item>
    /// </list>
    /// </summary>
    // Per-handler phase timer aggregator. Used by /diagnostics/handler-timing
    // to surface where /farmhands time goes when the regression cause is
    // unclear. Counts in microseconds for sub-millisecond granularity.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        HandlerTimingAccumulator
    > _handlerTimings = new();

    private sealed class HandlerTimingAccumulator
    {
        public long Calls;
        public long SnapshotReadUs;
        public long SerializeUs;
        public long EmitUs;
        public long WriteUs;
        public long TotalUs;
    }

    public sealed class HandlerTimingResponse
    {
        public string Handler { get; set; } = "";
        public long Calls { get; set; }
        public double SnapshotReadAvgMs { get; set; }
        public double SerializeAvgMs { get; set; }
        public double EmitAvgMs { get; set; }
        public double WriteAvgMs { get; set; }
        public double TotalAvgMs { get; set; }
    }

    public sealed class HandlerTimingReport
    {
        public List<HandlerTimingResponse> Handlers { get; set; } = new();
    }

    /// <summary>
    /// /farmhands handler with per-phase timing. Aggregates into
    /// <see cref="_handlerTimings"/> so a follow-up call to
    /// <c>/diagnostics/handler-timing</c> surfaces where time goes when
    /// the snapshot endpoint regresses.
    /// </summary>
    private async Task ProfileFarmhandsAsync(HttpListenerResponse response)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshotReadStart = sw.Elapsed;
        var farmhands = HandleGetFarmhands();
        var snapshotReadEnd = sw.Elapsed;

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(farmhands, JsonSettings);
        var serializeEnd = sw.Elapsed;

        // No event emitted from /farmhands; emit_ms is the gap between
        // serialize and the actual response stream write — buffer encoding
        // + content-length header writes. Cheap probe but useful when
        // chasing a regression.
        response.ContentType = "application/json";
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        var emitEnd = sw.Elapsed;

        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        var writeEnd = sw.Elapsed;

        var acc = _handlerTimings.GetOrAdd("/farmhands", _ => new HandlerTimingAccumulator());
        Interlocked.Increment(ref acc.Calls);
        Interlocked.Add(ref acc.SnapshotReadUs, ToUs(snapshotReadEnd - snapshotReadStart));
        Interlocked.Add(ref acc.SerializeUs, ToUs(serializeEnd - snapshotReadEnd));
        Interlocked.Add(ref acc.EmitUs, ToUs(emitEnd - serializeEnd));
        Interlocked.Add(ref acc.WriteUs, ToUs(writeEnd - emitEnd));
        Interlocked.Add(ref acc.TotalUs, ToUs(writeEnd));
    }

    private static long ToUs(TimeSpan ts) => (long)(ts.TotalMilliseconds * 1000.0);

    [ApiEndpoint(
        "GET",
        "/diagnostics/handler-timing",
        Summary = "Per-handler request timing averages",
        Tag = "Diagnostics"
    )]
    [ApiResponse(typeof(HandlerTimingReport), 200)]
    private HandlerTimingReport HandleGetHandlerTiming()
    {
        var report = new HandlerTimingReport();
        foreach (var (name, acc) in _handlerTimings)
        {
            var calls = Interlocked.Read(ref acc.Calls);
            if (calls == 0)
            {
                continue;
            }

            report.Handlers.Add(
                new HandlerTimingResponse
                {
                    Handler = name,
                    Calls = calls,
                    SnapshotReadAvgMs = Interlocked.Read(ref acc.SnapshotReadUs) / 1000.0 / calls,
                    SerializeAvgMs = Interlocked.Read(ref acc.SerializeUs) / 1000.0 / calls,
                    EmitAvgMs = Interlocked.Read(ref acc.EmitUs) / 1000.0 / calls,
                    WriteAvgMs = Interlocked.Read(ref acc.WriteUs) / 1000.0 / calls,
                    TotalAvgMs = Interlocked.Read(ref acc.TotalUs) / 1000.0 / calls,
                }
            );
        }
        return report;
    }

    [ApiEndpoint("GET", "/wait/players", Summary = "Long-poll connected players", Tag = "Server")]
    [ApiResponse(typeof(PlayersResponse), 200, Description = "Player list once the filters match")]
    [ApiResponse(typeof(void), 408, Description = "Timeout elapsed with no match")]
    private async Task HandleWaitPlayersAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        var qs = request.QueryString;
        var since = ParseLong(qs["since"]) ?? 0;
        var playerIdFilter = ParseLong(qs["playerId"]);
        var timeout = ResolveWaitTimeout(qs["timeout"]);

        bool Matches(GameStateSnapshot snap)
        {
            if (snap.Sequence <= since)
            {
                return false;
            }

            if (playerIdFilter is long pid)
            {
                var found = false;
                foreach (var p in snap.Players)
                {
                    if (p.Id == pid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            return true;
        }

        var matched = await WaitForSnapshotAsync(Matches, timeout);
        if (matched == null)
        {
            response.StatusCode = 408;
            return;
        }

        // Predicate-transition time: for the playerId filter, when that
        // player first appeared in any snapshot. For the sequence-only case
        // (no playerId filter) there is no single changed field to report,
        // so the snapshot's capture time is the best available.
        if (
            playerIdFilter is long matchedPid
            && matched.PlayerFirstSeenAtUtc.TryGetValue(matchedPid, out var firstSeen)
        )
        {
            EmitPredicateChangedAtHeader(response, firstSeen);
        }
        await WriteJsonAsync(response, HandleGetPlayers(matched));
    }

    /// <summary>
    /// Long-poll variant of <c>/farmhands</c>. Snapshot-backed; reuses
    /// <see cref="WaitForSnapshotAsync"/>. Query params:
    /// <list type="bullet">
    /// <item><c>since=N</c> — last observed snapshot sequence.</item>
    /// <item><c>farmhandCount=N</c> — wait until the snapshot's farmhand list
    /// contains exactly N entries.</item>
    /// <item><c>hasFarmhand=&lt;name&gt;</c> — wait until a farmhand with the
    /// given name is present (case-insensitive).</item>
    /// <item><c>requireCustomized=true|false</c> — when combined with
    /// <c>hasFarmhand</c>, requires the matching farmhand's
    /// <c>IsCustomized</c> flag.</item>
    /// <item><c>timeout=ms</c> — bounded by <see cref="WaitMaxTimeout"/>.</item>
    /// </list>
    /// </summary>
    [ApiEndpoint("GET", "/wait/farmhands", Summary = "Long-poll farmhand slots", Tag = "Farmhands")]
    [ApiResponse(
        typeof(FarmhandsResponse),
        200,
        Description = "Farmhand list once the filters match"
    )]
    [ApiResponse(typeof(void), 408, Description = "Timeout elapsed with no match")]
    private async Task HandleWaitFarmhandsAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        var qs = request.QueryString;
        var since = ParseLong(qs["since"]) ?? 0;
        var farmhandCountFilter = ParseInt(qs["farmhandCount"]);
        var hasFarmhandFilter = qs["hasFarmhand"];
        var requireCustomizedFilter = ParseBool(qs["requireCustomized"]);
        var timeout = ResolveWaitTimeout(qs["timeout"]);

        bool Matches(GameStateSnapshot snap)
        {
            if (snap.Sequence <= since)
            {
                return false;
            }

            if (farmhandCountFilter is int fc && snap.Farmhands.Count != fc)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(hasFarmhandFilter))
            {
                var found = false;
                foreach (var f in snap.Farmhands)
                {
                    if (
                        !string.Equals(
                            f.Name,
                            hasFarmhandFilter,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    if (requireCustomizedFilter is bool rc && f.IsCustomized != rc)
                    {
                        continue;
                    }

                    found = true;
                    break;
                }
                if (!found)
                {
                    return false;
                }
            }
            return true;
        }

        var matched = await WaitForSnapshotAsync(Matches, timeout);
        if (matched == null)
        {
            response.StatusCode = 408;
            return;
        }

        // Predicate-transition time: when the matching farmhand was first
        // seen (for `hasFarmhand`) and/or its customization last flipped
        // (when `requireCustomized` is involved). For `farmhandCount` we
        // take the latest first-seen across all current farmhands — that's
        // when the count last reached the current size. Each contributing
        // predicate is folded via max; the header lands on the latest
        // contributing transition.
        var changedAt = default(DateTime);
        if (!string.IsNullOrEmpty(hasFarmhandFilter))
        {
            foreach (var f in matched.Farmhands)
            {
                if (!string.Equals(f.Name, hasFarmhandFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (matched.FarmhandChangeTracks.TryGetValue(f.Id, out var track))
                {
                    changedAt = MaxUtc(changedAt, track.FirstSeenAtUtc);
                    if (requireCustomizedFilter is not null)
                    {
                        changedAt = MaxUtc(changedAt, track.IsCustomizedChangedAtUtc);
                    }
                }
                break;
            }
        }
        else if (farmhandCountFilter is not null)
        {
            foreach (var track in matched.FarmhandChangeTracks.Values)
            {
                changedAt = MaxUtc(changedAt, track.FirstSeenAtUtc);
            }
        }
        EmitPredicateChangedAtHeader(response, changedAt);

        await WriteJsonAsync(response, HandleGetFarmhands(matched));
    }

    /// <summary>
    /// Long-poll variant of <c>/health</c>. Source-of-truth is
    /// <c>_lastTickTimestamp</c>, not the snapshot — game-thread liveness
    /// updates every tick (~60 Hz) regardless of snapshot publish gating
    /// or Game1.newDay skips. Filters:
    /// <list type="bullet">
    /// <item><c>ready=true</c> — block until <c>IsFrozen == false</c>
    /// (lastTickMs non-null and ≤ 5 s).</item>
    /// <item><c>timeout=ms</c> — bounded by <see cref="WaitMaxTimeout"/>.</item>
    /// </list>
    /// Stateless predicate: no <c>since</c> cursor, just "is the server
    /// healthy now". A waiter that arrives during cold-start (before any
    /// tick) sees nothing to rotate the TCS, so the call returns 408 after
    /// <see cref="WaitMaxTimeout"/>; callers must wrap in an outer
    /// re-issue loop.
    /// </summary>
    [ApiEndpoint(
        "GET",
        "/wait/health",
        Summary = "Long-poll health",
        Tag = "Health",
        Public = true
    )]
    [ApiResponse(typeof(HealthResponse), 200, Description = "Health status once the filters match")]
    [ApiResponse(typeof(void), 408, Description = "Timeout elapsed with no match")]
    private async Task HandleWaitHealthAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        var qs = request.QueryString;
        var readyFilter = ParseBool(qs["ready"]);
        var timeout = ResolveWaitTimeout(qs["timeout"]);

        bool Matches()
        {
            if (readyFilter is not bool b || !b)
            {
                return true;
            }

            var tickTicks = Interlocked.Read(ref _lastTickTimestamp);
            if (tickTicks <= 0)
            {
                return false;
            }

            var lastTickMs = (long)
                (DateTime.UtcNow - new DateTime(tickTicks, DateTimeKind.Utc)).TotalMilliseconds;
            return lastTickMs <= HealthFrozenThresholdMs;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                response.StatusCode = 408;
                return;
            }

            // Capture TCS BEFORE evaluating the predicate. If a tick rotates
            // the TCS between these two reads, the rotation also signals
            // the TCS we just captured — the await returns immediately and
            // we re-evaluate. The reverse order has a missed-wakeup race.
            var tcs = _lastTickChanged;
            if (Matches())
            {
                await WriteJsonAsync(response, HandleGetHealth());
                return;
            }

            try
            {
                await tcs.Task.WaitAsync(remaining);
            }
            catch (TimeoutException)
            {
                response.StatusCode = 408;
                return;
            }
        }
    }

    /// <summary>
    /// Emits the <c>X-Predicate-Changed-At-Ms-Ago</c> response header so the
    /// test harness can stamp <c>wait_matched</c>'s producer-time with the
    /// moment the predicate first became satisfiable — not the snapshot's
    /// capture time, which lags by up to one snapshot publish cycle (1s).
    ///
    /// <para>
    /// The argument is the transition-time UTC instant of the field (or
    /// the latest of several fields, for compound predicates) that the
    /// matching predicate keyed on. Computed by the caller because only
    /// the wait handler knows which fields its predicate looked at.
    /// </para>
    ///
    /// <para>
    /// Skipped silently when <paramref name="changedAtUtc"/> is <c>default</c>
    /// (no usable change-time available) — falls back to the harness's
    /// snapshot-age estimate.
    /// </para>
    /// </summary>
    private static void EmitPredicateChangedAtHeader(
        HttpListenerResponse response,
        DateTime changedAtUtc
    )
    {
        if (changedAtUtc == default)
        {
            return;
        }

        var msAgo = (long)(DateTime.UtcNow - changedAtUtc).TotalMilliseconds;
        if (msAgo < 0)
        {
            msAgo = 0;
        }

        response.Headers["X-Predicate-Changed-At-Ms-Ago"] = msAgo.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private async Task<GameStateSnapshot?> WaitForSnapshotAsync(
        Func<GameStateSnapshot, bool> predicate,
        TimeSpan timeout
    )
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            // Capture TCS BEFORE reading the snapshot. If PublishSnapshot rotates
            // the TCS between these two reads, the rotation also signals the TCS
            // we just captured — the await returns immediately and we re-evaluate.
            // The reverse order has a missed-wakeup window: a rotation between
            // the snapshot read and the TCS read leaves us awaiting the *next*
            // publish for a snapshot we'd already see if we re-read.
            var tcs = _snapshotChanged;
            var current = _snapshot;
            if (predicate(current))
            {
                return current;
            }

            try
            {
                await tcs.Task.WaitAsync(remaining);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
    }

    private TimeSpan ResolveWaitTimeout(string? raw)
    {
        if (long.TryParse(raw, out var ms) && ms > 0)
        {
            var requested = TimeSpan.FromMilliseconds(ms);
            return requested < WaitMaxTimeout ? requested : WaitMaxTimeout;
        }
        return WaitMaxTimeout;
    }

    private static long? ParseLong(string? raw) => long.TryParse(raw, out var v) ? v : null;

    private static int? ParseInt(string? raw) => int.TryParse(raw, out var v) ? v : null;

    private static bool? ParseBool(string? raw)
    {
        if (raw == null)
        {
            return null;
        }

        if (bool.TryParse(raw, out var b))
        {
            return b;
        }

        return null;
    }

    [ApiEndpoint("GET", "/stats", Summary = "Performance stats", Tag = "Diagnostics")]
    [ApiResponse(typeof(StatsResponse), 200)]
    private StatsResponse HandleGetStats()
    {
        var startedAt = ServerCommand.StartTimeUtc;
        return new StatsResponse
        {
            Fps = Math.Round(Volatile.Read(ref _currentFps), 1),
            Tps = Math.Round(Volatile.Read(ref _currentTps), 1),
            AvgTickMs = Math.Round(_avgTickMs.Average, 2),
            MemoryMb = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1),
            GcGen0 = GC.CollectionCount(0),
            GcGen1 = GC.CollectionCount(1),
            GcGen2 = GC.CollectionCount(2),
            PendingActions = _dispatcher.PendingCount,
            GameThreadWaitMs = Math.Round(_avgGameThreadWaitMs.Average, 2),
            StartedAtUtc = startedAt?.ToString("o"),
            UptimeSeconds = startedAt is { } t ? (long)(DateTime.UtcNow - t).TotalSeconds : null,
        };
    }

    /// <summary>Reads from periodic snapshot. See <see cref="TakeGameStateSnapshot"/>.</summary>
    [ApiEndpoint("GET", "/farmhands", Summary = "Get all farmhand slots", Tag = "Farmhands")]
    [ApiResponse(typeof(FarmhandsResponse), 200, Description = "List of farmhand slots")]
    private FarmhandsResponse HandleGetFarmhands() => HandleGetFarmhands(_snapshot);

    private static FarmhandsResponse HandleGetFarmhands(GameStateSnapshot snap)
    {
        return new FarmhandsResponse { Farmhands = snap.Farmhands, Sequence = snap.Sequence };
    }

    [ApiEndpoint("GET", "/settings", Summary = "Get server settings", Tag = "Settings")]
    [ApiResponse(typeof(SettingsResponse), 200, Description = "Current server settings")]
    private SettingsResponse HandleGetSettings()
    {
        var raw = _settings.Raw;
        return new SettingsResponse
        {
            Game = new GameSettingsInfo
            {
                FarmName = raw.Game.FarmName,
                FarmType = raw.Game.FarmType,
                ProfitMargin = raw.Game.ProfitMargin,
                StartingCabins = raw.Game.StartingCabins,
                SpawnMonstersAtNight = raw.Game.SpawnMonstersAtNight,
            },
            Server = new ServerRuntimeSettingsInfo
            {
                MaxPlayers = raw.Server.MaxPlayers,
                CabinStrategy = raw.Server.CabinStrategy.ToString(),
                SeparateWallets = raw.Server.SeparateWallets,
                ExistingCabinBehavior = raw.Server.ExistingCabinBehavior.ToString(),
                AllowCabinRelocation = raw.Server.AllowCabinRelocation,
            },
        };
    }

    /// <summary>Reads from periodic snapshot. See <see cref="TakeGameStateSnapshot"/>.</summary>
    [ApiEndpoint("GET", "/cabins", Summary = "Get cabin state and positions", Tag = "Cabins")]
    [ApiResponse(typeof(CabinsResponse), 200, Description = "Cabin state snapshot")]
    private CabinsResponse HandleGetCabins()
    {
        var snap = _snapshot;
        return new CabinsResponse
        {
            Strategy = _persistentOptions.Data.CabinStrategy.ToString(),
            TotalCount = snap.CabinTotalCount,
            AssignedCount = snap.CabinAssignedCount,
            AvailableCount = snap.CabinAvailableCount,
            Cabins = snap.Cabins,
            SavedPositionPlayerIds = _cabinManager.Data.PlayerCabinPositions.Keys.ToList(),
            Migration = snap.CabinMigration,
            StackSpot = snap.CabinStackSpot,
            MainFarmHouseEntry = snap.CabinMainFarmHouseEntry,
        };
    }

    [ApiEndpoint("GET", "/rendering", Summary = "Get render rate", Tag = "Server")]
    [ApiResponse(typeof(RenderingStatus), 200, Description = "Current render rate (0 = disabled)")]
    private RenderingStatus HandleGetRendering()
    {
        return new RenderingStatus { Fps = ServerOptimizerOverrides.GetCurrentServerFps() };
    }

    [ApiEndpoint(
        "GET",
        "/screenshot",
        Summary = "Capture a screenshot of the game",
        Tag = "Server"
    )]
    [ApiResponse(typeof(ScreenshotResponse), 200, Description = "Screenshot as base64-encoded PNG")]
    private async Task<ScreenshotResponse> HandleGetScreenshotAsync()
    {
        var result = new ScreenshotResponse();
        try
        {
            await RunOnGameThreadAsync(() =>
            {
                var device = Game1.graphics.GraphicsDevice;
                var pp = device.PresentationParameters;
                var width = pp.BackBufferWidth;
                var height = pp.BackBufferHeight;

                var backBuffer = new Microsoft.Xna.Framework.Color[width * height];
                device.GetBackBufferData(backBuffer);

                using var texture = new Microsoft.Xna.Framework.Graphics.Texture2D(
                    device,
                    width,
                    height,
                    false,
                    Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color
                );
                texture.SetData(backBuffer);

                using var stream = new System.IO.MemoryStream();
                texture.SaveAsPng(stream, width, height);

                result.Success = true;
                result.Base64Png = Convert.ToBase64String(stream.ToArray());
                result.Width = width;
                result.Height = height;
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        return result;
    }

    // TODO: Consider splitting config fields (Enabled, TimeoutSeconds, MaxAttempts) from
    // live state (AuthenticatedCount, PendingCount). Config is cheap; live state requires
    /// <summary>
    /// Reads player counts from periodic snapshot, config from the service directly.
    /// See <see cref="TakeGameStateSnapshot"/>.
    /// </summary>
    [ApiEndpoint(
        "GET",
        "/auth",
        Summary = "Get authentication/password protection status",
        Tag = "Auth"
    )]
    [ApiResponse(typeof(AuthStatusResponse), 200, Description = "Authentication status")]
    private AuthStatusResponse HandleGetAuthStatus()
    {
        if (_passwordProtectionService == null || !_passwordProtectionService.IsEnabled)
        {
            return new AuthStatusResponse
            {
                Enabled = false,
                AuthenticatedCount = 0,
                PendingCount = 0,
                TimeoutSeconds = 0,
                MaxAttempts = 0,
            };
        }

        var snap = _snapshot;
        return new AuthStatusResponse
        {
            Enabled = true,
            AuthenticatedCount = snap.AuthenticatedCount,
            PendingCount = snap.PendingCount,
            TimeoutSeconds = _passwordProtectionService.AuthTimeoutSeconds,
            MaxAttempts = _passwordProtectionService.MaxFailedAttempts,
        };
    }

    [ApiEndpoint("POST", "/auth/timeout", Summary = "Set auth timeout in seconds", Tag = "Auth")]
    [ApiResponse(typeof(AuthTimeoutResponse), 200, Description = "Auth timeout updated")]
    private AuthTimeoutResponse HandlePostAuthTimeout(string? value)
    {
        if (_passwordProtectionService == null || !_passwordProtectionService.IsEnabled)
        {
            return new AuthTimeoutResponse
            {
                Success = false,
                TimeoutSeconds = 0,
                Error = "Password protection is not enabled",
            };
        }

        if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var seconds) || seconds < 0)
        {
            return new AuthTimeoutResponse
            {
                Success = false,
                TimeoutSeconds = _passwordProtectionService.AuthTimeoutSeconds,
                Error = "Missing or invalid 'value' parameter (expected non-negative integer)",
            };
        }

        var previous = _passwordProtectionService.AuthTimeoutSeconds;
        _passwordProtectionService.AuthTimeoutSeconds = seconds;
        Monitor.Log($"[API] Auth timeout changed: {previous}s → {seconds}s", LogLevel.Info);

        return new AuthTimeoutResponse
        {
            Success = true,
            TimeoutSeconds = seconds,
            PreviousTimeoutSeconds = previous,
        };
    }

    [ApiEndpoint("POST", "/rendering", Summary = "Set server render rate", Tag = "Server")]
    [ApiResponse(typeof(RenderingSetResponse), 200, Description = "Render rate set")]
    private async Task<RenderingSetResponse> HandlePostRenderingAsync(string? fps)
    {
        if (string.IsNullOrEmpty(fps) || !int.TryParse(fps, out var newFps) || newFps < 0)
        {
            return new RenderingSetResponse
            {
                Success = false,
                Fps = ServerOptimizerOverrides.GetCurrentServerFps(),
                Error =
                    "Missing or invalid 'fps' parameter (expected non-negative integer; 0 disables rendering)",
            };
        }

        var previous = ServerOptimizerOverrides.GetCurrentServerFps();
        await RunOnGameThreadAsync(() =>
        {
            ServerOptimizerOverrides.SetServerFps(newFps, Monitor);
        });

        return new RenderingSetResponse
        {
            Success = true,
            Fps = newFps,
            PreviousFps = previous,
            Message = newFps == 0 ? "Rendering disabled" : $"Rendering enabled at {newFps} fps",
        };
    }

    [ApiEndpoint("POST", "/time", Summary = "Set game time of day", Tag = "Server")]
    [ApiResponse(typeof(TimeSetResponse), 200, Description = "Time set")]
    private async Task<TimeSetResponse> HandlePostTimeAsync(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new TimeSetResponse
            {
                Success = false,
                TimeOfDay = Game1.timeOfDay,
                Error = "Missing 'value' query parameter (e.g., 1200 for noon)",
            };
        }

        if (!int.TryParse(value, out var time))
        {
            return new TimeSetResponse
            {
                Success = false,
                TimeOfDay = Game1.timeOfDay,
                Error = $"Invalid value '{value}' (expected integer like 1200 for noon)",
            };
        }

        if (time < 600 || time > 2600)
        {
            return new TimeSetResponse
            {
                Success = false,
                TimeOfDay = Game1.timeOfDay,
                Error = $"Value {time} out of range (600-2600)",
            };
        }

        // Fail closed before mutating: with no world loaded netWorldState.Value is null, so the
        // replication push below would silently no-op while we still reported Success — the time
        // would never reach peers. (gameMode == 3 is playingGameMode; pattern matches /roles.)
        if (Game1.gameMode != 3 || !Game1.IsServer)
        {
            return new TimeSetResponse
            {
                Success = false,
                TimeOfDay = Game1.timeOfDay,
                Error = "Server not ready",
            };
        }

        await RunOnGameThreadAsync(() =>
        {
            Game1.timeOfDay = time;
            Game1.gameTimeInterval = 0;
            // timeOfDay is a replicated NetWorldState field; without this push the host's
            // local write never reaches clients, which keep their stale time until the host's
            // next natural 10-minute tick broadcasts a delta. Push it so SetTime is observable
            // on peers immediately (mirrors /test/set_date's same UpdateFromGame1 call).
            Game1.netWorldState.Value.UpdateFromGame1();
        });
        Monitor.Log($"Time set to {time} via API", LogLevel.Info);

        return new TimeSetResponse
        {
            Success = true,
            TimeOfDay = time,
            Message = $"Time set to {time}",
        };
    }

    [ApiEndpoint("POST", "/clock-speed", Summary = "Adjust game clock speed", Tag = "Server")]
    [ApiResponse(typeof(ClockSpeedResponse), 200, Description = "Clock speed adjusted")]
    private async Task<ClockSpeedResponse> HandlePostClockSpeedAsync(string? multiplierStr)
    {
        if (string.IsNullOrEmpty(multiplierStr))
        {
            return new ClockSpeedResponse
            {
                Success = false,
                Error =
                    "Missing 'multiplier' query parameter (e.g., 10 for 10x speed, 1 to restore)",
            };
        }

        if (
            !double.TryParse(
                multiplierStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var multiplier
            )
        )
        {
            return new ClockSpeedResponse
            {
                Success = false,
                Error =
                    $"Invalid multiplier '{multiplierStr}' (expected a number like 10, 0.5, or 1)",
            };
        }

        if (multiplier <= 0)
        {
            return new ClockSpeedResponse
            {
                Success = false,
                Error = "Multiplier must be greater than 0",
            };
        }

        int effectiveMs = 0;
        await RunOnGameThreadAsync(() =>
        {
            // Capture default on first call
            if (_defaultRealMsPerGameMinute < 0)
            {
                _defaultRealMsPerGameMinute = Game1.realMilliSecondsPerGameMinute;
            }

            var newMs = (int)(_defaultRealMsPerGameMinute / multiplier);
            if (newMs < 1)
            {
                newMs = 1;
            }

            Game1.realMilliSecondsPerGameMinute = newMs;
            Game1.realMilliSecondsPerGameTenMinutes = newMs * 10;
            effectiveMs = newMs;
        });

        Monitor.Log(
            $"Clock speed set to {multiplier}x ({effectiveMs}ms/game-minute) via API",
            LogLevel.Info
        );

        return new ClockSpeedResponse
        {
            Success = true,
            Multiplier = multiplier,
            EffectiveMs = effectiveMs,
        };
    }

    [ApiEndpoint("POST", "/roles/admin", Summary = "Grant admin role to a player", Tag = "Roles")]
    [ApiResponse(typeof(RoleGrantResponse), 200, Description = "Admin granted")]
    private async Task<RoleGrantResponse> HandlePostGrantAdminAsync(string? name, string? playerId)
    {
        var hasName = !string.IsNullOrEmpty(name);
        var hasId = !string.IsNullOrEmpty(playerId);

        if (!hasName && !hasId)
        {
            return new RoleGrantResponse
            {
                Success = false,
                Error = "Missing 'name' or 'playerId' query parameter",
            };
        }
        if (hasName && hasId)
        {
            return new RoleGrantResponse
            {
                Success = false,
                Error = "Provide either 'name' or 'playerId', not both",
            };
        }

        long parsedId = 0;
        if (hasId && !long.TryParse(playerId, out parsedId))
        {
            return new RoleGrantResponse
            {
                Success = false,
                Error = $"Invalid 'playerId' value: '{playerId}'",
            };
        }

        if (Game1.gameMode != 3 || !Game1.IsServer)
        {
            return new RoleGrantResponse { Success = false, Error = "Server not ready" };
        }

        RoleGrantResponse? result = null;
        await RunOnGameThreadAsync(() =>
        {
            var farmer = hasId
                ? Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == parsedId)
                : Game1
                    .getAllFarmers()
                    .FirstOrDefault(f =>
                        string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)
                    );

            if (farmer == null)
            {
                var lookup = hasId ? $"id={parsedId}" : $"'{name}'";
                result = new RoleGrantResponse
                {
                    Success = false,
                    Error = $"Player {lookup} not found",
                };
                return;
            }

            _roleService.AssignAdmin(farmer.UniqueMultiplayerID);
            Monitor.Log(
                $"Admin role granted to '{ChatRedaction.MaskValue(farmer.Name)}' (ID: {farmer.UniqueMultiplayerID}) via API",
                LogLevel.Info
            );

            result = new RoleGrantResponse
            {
                Success = true,
                PlayerId = farmer.UniqueMultiplayerID,
                PlayerName = farmer.Name,
                Message = $"Admin role granted to '{farmer.Name}'",
            };
        });

        return result!;
    }

    [ApiEndpoint(
        "DELETE",
        "/farmhands",
        Summary = "Delete a farmhand by name or player ID",
        Tag = "Farmhands"
    )]
    [ApiResponse(typeof(FarmhandResponse), 200, Description = "Farmhand deleted")]
    private async Task<FarmhandResponse> HandleDeleteFarmhandAsync(string? name, string? playerId)
    {
        var hasName = !string.IsNullOrEmpty(name);
        var hasId = !string.IsNullOrEmpty(playerId);

        if (!hasName && !hasId)
        {
            return new FarmhandResponse
            {
                Success = false,
                Error = "Missing 'name' or 'playerId' query parameter",
            };
        }
        if (hasName && hasId)
        {
            return new FarmhandResponse
            {
                Success = false,
                Error = "Provide either 'name' or 'playerId', not both",
            };
        }

        long parsedId = 0;
        if (hasId && !long.TryParse(playerId, out parsedId))
        {
            return new FarmhandResponse
            {
                Success = false,
                Error = $"Invalid 'playerId' value: '{playerId}'",
            };
        }

        if (Game1.gameMode != 3 || !Game1.IsServer)
        {
            return new FarmhandResponse { Success = false, Error = "Server not ready" };
        }

        if (Game1.game1.IsSaving)
        {
            return new FarmhandResponse
            {
                Success = false,
                Error = "Cannot delete farmhand while save is in progress",
            };
        }

        var lookupLabel = hasId ? $"id={parsedId}" : $"'{name}'";

        try
        {
            // Run the entire lookup + online check + deletion on the game thread.
            // Reading Game1.getAllFarmhands() / getOnlineFarmers() off-thread is racy:
            // the collections can be mid-update when a player is joining or leaving.
            FarmhandResponse? result = null;
            await RunOnGameThreadAsync(
                () =>
                {
                    Farmer? targetFarmhand = null;
                    foreach (var farmer in Game1.getAllFarmhands())
                    {
                        var matches = hasId
                            ? farmer.UniqueMultiplayerID == parsedId
                            : farmer.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true;
                        if (matches)
                        {
                            targetFarmhand = farmer;
                            break;
                        }
                    }

                    if (targetFarmhand == null)
                    {
                        result = new FarmhandResponse
                        {
                            Success = false,
                            Error = $"Farmhand {lookupLabel} not found",
                        };
                        return;
                    }

                    // Check if farmhand is online
                    if (
                        Game1
                            .getOnlineFarmers()
                            .Any(f => f.UniqueMultiplayerID == targetFarmhand.UniqueMultiplayerID)
                    )
                    {
                        result = new FarmhandResponse
                        {
                            Success = false,
                            Error = $"Cannot delete farmhand {lookupLabel} - currently online",
                        };
                        return;
                    }

                    var resolvedName = targetFarmhand.Name ?? string.Empty;
                    ExecuteFarmhandDeletion(targetFarmhand.UniqueMultiplayerID, resolvedName);
                    Monitor.Log(
                        $"Deleted farmhand '{ChatRedaction.MaskValue(resolvedName)}' (ID: {targetFarmhand.UniqueMultiplayerID})",
                        LogLevel.Info
                    );
                    result = new FarmhandResponse
                    {
                        Success = true,
                        Message = $"Farmhand '{resolvedName}' deleted successfully",
                    };
                },
                timeoutMs: 15000
            );

            return result!;
        }
        catch (TaskCanceledException)
        {
            Monitor.Log($"Farmhand deletion timed out for {lookupLabel}", LogLevel.Error);
            return new FarmhandResponse { Success = false, Error = "Deletion timed out" };
        }
        catch (Exception ex)
        {
            Monitor.Log($"Error deleting farmhand: {ex}", LogLevel.Error);
            return new FarmhandResponse { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Executes the actual farmhand deletion on the main game thread.
    /// Called via RunOnGameThreadAsync from HandleDeleteFarmhandAsync.
    /// Exceptions are propagated back to the caller via the TaskCompletionSource.
    /// </summary>
    private void ExecuteFarmhandDeletion(long farmhandId, string farmhandName)
    {
        var farm = Game1.getFarm();

        // Find the cabin (farmhand may already be gone from farmhandData if deletion was queued multiple times)
        Cabin? targetCabin = null;
        Building? cabinBuilding = null;
        foreach (var building in farm.buildings)
        {
            if (!building.isCabin)
            {
                continue;
            }

            var cabin = building.GetIndoors<Cabin>();
            if (cabin?.owner?.UniqueMultiplayerID == farmhandId)
            {
                targetCabin = cabin;
                cabinBuilding = building;
                break;
            }
        }

        // Centralized destruction: drops farmhand, removes building, fans out
        // stale homeLocation references in surviving farmhandData entries.
        if (targetCabin != null)
        {
            _cabinManager.DestroyCabin(cabinBuilding);
            Monitor.Log(
                $"Destroyed cabin for farmhand '{ChatRedaction.MaskValue(farmhandName)}'",
                LogLevel.Debug
            );
        }
        else
        {
            // Fallback: farmhand exists but no cabin found.
            if (Game1.netWorldState.Value.farmhandData.FieldDict.ContainsKey(farmhandId))
            {
                // Defense against netcode races: scan for any cabin still pointing
                // at this farmhand via farmhandReference and null it before removal.
                foreach (var building in farm.buildings)
                {
                    if (!building.isCabin)
                    {
                        continue;
                    }

                    var cabin = building.GetIndoors<Cabin>();
                    if (
                        cabin != null
                        && cabin.farmhandReference.defined.Value
                        && cabin.farmhandReference.uid.Value == farmhandId
                    )
                    {
                        cabin.farmhandReference.Value = null;
                        Monitor.Log(
                            $"Cleared dangling farmhandReference on cabin '{cabin.NameOrUniqueName}' for farmhand '{ChatRedaction.MaskValue(farmhandName)}' (id={farmhandId})",
                            LogLevel.Warn
                        );
                    }
                }

                Monitor.Log(
                    $"No cabin found for farmhand '{ChatRedaction.MaskValue(farmhandName)}', removing from farmhandData directly",
                    LogLevel.Warn
                );
                Game1.netWorldState.Value.farmhandData.Remove(farmhandId);
                // DestroyCabin drops the owner record on the cabin path; this direct-removal
                // fallback must too, or the deleted slot's record lingers until the next load's
                // self-heal.
                _farmhandOwnership.RemoveOwner(farmhandId);
            }
            else
            {
                Monitor.Log(
                    $"Farmhand '{ChatRedaction.MaskValue(farmhandName)}' already deleted (no cabin or farmhandData found)",
                    LogLevel.Debug
                );
                return;
            }
        }

        // Remove from cabin manager tracking. PlayerCabinPositions shares the
        // farmhand's lifecycle, so clear its intent entry here too — otherwise a
        // deleted player's position record leaks into the save indefinitely.
        var removedEverJoined = _cabinManager.Data.AllPlayerIdsEverJoined.Remove(farmhandId);
        var removedPosition = _cabinManager.Data.PlayerCabinPositions.TryRemove(farmhandId, out _);
        if (removedEverJoined || removedPosition)
        {
            _cabinManager.Data.Write();
            Monitor.Log($"Removed farmhand from cabin tracking", LogLevel.Debug);
        }

        // Let cabin management create a fresh cabin with a new farmhand
        _cabinManager.EnsureAtLeastXCabins();
    }

    [ApiEndpoint(
        "POST",
        "/newgame",
        Summary = "Create a new game with specified settings",
        Tag = "Server"
    )]
    [ApiResponse(typeof(NewGameResponse), 200, Description = "New game created successfully")]
    private async Task HandlePostNewGameAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        // Parse the request body. The reject sink lets a settings converter (e.g. FarmType) default a
        // malformed field instead of aborting the parse, so the rest of the request still applies.
        NewGameRequest? body = null;
        var rejects = new List<SettingReject>();
        try
        {
            using var reader = new System.IO.StreamReader(
                request.InputStream,
                request.ContentEncoding
            );
            var json = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(json))
            {
                body = JsonConvert.DeserializeObject<NewGameRequest>(
                    json,
                    new JsonSerializerSettings
                    {
                        Context = new StreamingContext(StreamingContextStates.Other, rejects),
                    }
                );
            }
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to parse /newgame request body: {ex.Message}", LogLevel.Debug);
        }

        foreach (var reject in rejects)
        {
            Monitor.Log(
                $"/newgame request: {reject.Path} '{reject.RejectedValue}' is not recognized; using '{reject.FallbackUsed}'.",
                LogLevel.Debug
            );
        }

        body ??= new NewGameRequest();

        // Build the config from the settings file (defaults), overriding with any explicitly
        // provided request values. Starting from FromSettings guarantees EVERY file setting
        // flows into the created game unless the request overrides it — a hand-picked
        // parameter list drops whatever it forgets (CabinLayoutNearby, for one, decides
        // which designated-position layout every later load resolves).
        var config = NewGameConfig.FromSettings(_settings);
        if (body.FarmType.HasValue)
        {
            config.WhichFarm = body.FarmType.Value;
        }
        if (body.FarmName != null)
        {
            config.FarmName = body.FarmName;
        }
        if (body.StartingCabins.HasValue)
        {
            config.StartingCabins = body.StartingCabins.Value;
        }
        if (body.CabinStrategy != null)
        {
            if (!CabinStrategyParser.TryParse(body.CabinStrategy, out var strategy))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(
                    response,
                    new NewGameResponse
                    {
                        Success = false,
                        Error =
                            $"Unknown cabinStrategy '{body.CabinStrategy}' (use CabinStack, "
                            + "FarmhouseStack, or None).",
                    }
                );
                return;
            }
            config.CabinStrategy = strategy;
        }
        if (body.MaxPlayers.HasValue)
        {
            config.MaxPlayers = body.MaxPlayers.Value;
        }
        // Validate the EFFECTIVE MaxPlayers — the request override if present, else the
        // server-settings.json default FromSettings seeded — before it reaches the durable game:
        // ApplyNewGameConfig persists it and SyncFromSettings reapplies it on every later load, so
        // a 0/negative value sticks, and under None it freezes the cap at min(designated,
        // MaxPlayers), leaving a game no player can ever join. settings validate uses the same
        // [1..100] range but only as a diagnostic, so the API must gate it here — and gating the
        // effective value (not just the request override) closes the omitted-field path where a
        // bad settings default would otherwise flow through unchecked.
        if (config.MaxPlayers < 1 || config.MaxPlayers > 100)
        {
            response.StatusCode = 400;
            await WriteJsonAsync(
                response,
                new NewGameResponse
                {
                    Success = false,
                    Error = $"maxPlayers {config.MaxPlayers} is out of range [1..100].",
                }
            );
            return;
        }
        if (body.ProfitMargin.HasValue)
        {
            config.ProfitMargin = body.ProfitMargin.Value;
        }
        if (config.ProfitMargin < 0.25f || config.ProfitMargin > 1.0f)
        {
            response.StatusCode = 400;
            await WriteJsonAsync(
                response,
                new NewGameResponse
                {
                    Success = false,
                    Error = $"profitMargin {config.ProfitMargin} is out of range [0.25..1.0].",
                }
            );
            return;
        }
        if (body.SeparateWallets.HasValue)
        {
            config.UseSeparateWallets = body.SeparateWallets.Value;
        }
        if (body.AllowCabinRelocation.HasValue)
        {
            config.AllowCabinRelocation = body.AllowCabinRelocation.Value;
        }
        if (body.AllowIpConnections.HasValue)
        {
            config.AllowIpConnections = body.AllowIpConnections.Value;
        }

        Monitor.Log($"[API] New game requested: {config}", LogLevel.Info);

        var gameManager = GameManagerService.Instance;
        if (gameManager == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(
                response,
                new NewGameResponse { Success = false, Error = "Game manager not initialized yet" }
            );
            return;
        }

        // Lease check → client-count check → take lease → request, in ONE game-thread action so
        // no join or competing transition can land between check and act, and no early return
        // ever holds a lease it must give back. Lease before count: the body is stable while a
        // reset's own client is still connected. The lease is released when the operation's
        // task settles — never on the 120 s HTTP timeout below, so an operation that outlives
        // its request still holds it until the world is loaded.
        Task? newGameTask = null;
        string? rejection = null;
        try
        {
            await RunOnGameThreadAsync(() =>
            {
                if (gameManager.Disruption.Holder is { } holder)
                {
                    rejection = $"{holder} in progress";
                    return;
                }

                var connectedClients = Game1.otherFarmers.Count;
                if (connectedClients > 0)
                {
                    rejection =
                        $"Cannot create new game while {connectedClients} client(s) are connected. Disconnect all players first.";
                    return;
                }

                newGameTask = StartWorldTransition(
                    gameManager,
                    WorldTransitionNewGame,
                    () => gameManager.RequestNewGame(config)
                );
            });
        }
        catch (Exception ex)
        {
            // Fail closed: the count could not be read, so don't assume nobody is connected.
            Monitor.Log(
                $"[API] New game precheck could not run on the game thread: {ex.Message}",
                LogLevel.Warn
            );
            response.StatusCode = 503;
            await WriteJsonAsync(
                response,
                new NewGameResponse
                {
                    Success = false,
                    Error =
                        "Cannot verify server state right now (game thread busy, likely a day transition or save sync). Retry after a few seconds.",
                }
            );
            return;
        }

        if (rejection != null)
        {
            response.StatusCode = 409;
            await WriteJsonAsync(
                response,
                new NewGameResponse { Success = false, Error = rejection }
            );
            return;
        }

        // Wait for the new game creation to complete (title screen → game creation → ready).
        // This spans the full ExitToTitle → CleanupReturningToTitle → TitleMenu → CreateNewGame cycle.
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
            var completed = await Task.WhenAny(newGameTask!, timeoutTask);

            if (completed == newGameTask)
            {
                await newGameTask!; // Propagate any exception from CreateNewGame
                await WriteJsonAsync(
                    response,
                    new NewGameResponse
                    {
                        Success = true,
                        Message = $"New game created with farm type {config.WhichFarm}",
                    }
                );
                return;
            }

            // Timeout
            response.StatusCode = 504;
            await WriteJsonAsync(
                response,
                new NewGameResponse
                {
                    Success = false,
                    Error = "New game creation timed out (120s)",
                }
            );
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await WriteJsonAsync(
                response,
                new NewGameResponse
                {
                    Success = false,
                    Error = $"New game creation failed: {ex.Message}",
                }
            );
        }
    }

    [ApiEndpoint(
        "POST",
        "/reload",
        Summary = "Re-read server-settings.json and reload the active world (no restart)",
        Tag = "Server"
    )]
    [ApiResponse(typeof(ReloadResponse), 200, Description = "World reloaded successfully")]
    private async Task HandlePostReloadAsync(HttpListenerResponse response)
    {
        var gameManager = GameManagerService.Instance;
        if (gameManager == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(
                response,
                new ReloadResponse { Success = false, Error = "Game manager not initialized yet" }
            );
            return;
        }

        Monitor.Log("[API] World reload requested", LogLevel.Info);

        // Same single-action shape as /newgame: lease → client count → take lease → request.
        // Reload returns to title and would disconnect everyone, so fail closed when the count
        // can't be read (503), never assume 0.
        Task? reloadTask = null;
        string? rejection = null;
        try
        {
            await RunOnGameThreadAsync(() =>
            {
                if (gameManager.Disruption.Holder is { } holder)
                {
                    rejection = $"{holder} in progress";
                    return;
                }

                var connectedClients = Game1.otherFarmers.Count;
                if (connectedClients > 0)
                {
                    rejection =
                        $"Cannot reload while {connectedClients} client(s) are connected. Disconnect all players first.";
                    return;
                }

                reloadTask = StartWorldTransition(
                    gameManager,
                    WorldTransitionReload,
                    gameManager.RequestReloadSave
                );
            });
        }
        catch (Exception ex)
        {
            Monitor.Log(
                $"[API] Reload precheck could not run on the game thread: {ex.Message}",
                LogLevel.Warn
            );
            response.StatusCode = 503;
            await WriteJsonAsync(
                response,
                new ReloadResponse
                {
                    Success = false,
                    Error =
                        "Cannot verify server state right now (game thread busy, likely a day transition or save sync). Retry after a few seconds.",
                }
            );
            return;
        }

        if (rejection != null)
        {
            response.StatusCode = 409;
            await WriteJsonAsync(
                response,
                new ReloadResponse { Success = false, Error = rejection }
            );
            return;
        }

        // Wait for the reload to complete (ExitToTitle → title screen → LoadSave → ready).
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
            var completed = await Task.WhenAny(reloadTask!, timeoutTask);

            if (completed == reloadTask)
            {
                await reloadTask!; // Propagate any exception from LoadSave
                await WriteJsonAsync(
                    response,
                    new ReloadResponse { Success = true, Message = "World reloaded" }
                );
                return;
            }

            // Timeout
            response.StatusCode = 504;
            await WriteJsonAsync(
                response,
                new ReloadResponse { Success = false, Error = "World reload timed out (120s)" }
            );
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await WriteJsonAsync(
                response,
                new ReloadResponse { Success = false, Error = $"World reload failed: {ex.Message}" }
            );
        }
    }

    /// <summary>Lease holder names for the two HTTP world transitions; they surface in 409 bodies as "&lt;name&gt; in progress".</summary>
    private const string WorldTransitionNewGame = "new game";
    private const string WorldTransitionReload = "reload";

    /// <summary>
    /// Takes the world-disruption lease for <paramref name="holder"/> and starts the transition.
    /// The returned task carries the operation's outcome and settles only after the lease has
    /// been released, so a caller that observes completion can start the next transition at
    /// once. Game thread only, and only after the caller has checked the lease is free in the
    /// same action — the acquire cannot fail here, so the request always runs under the lease.
    /// </summary>
    private static Task StartWorldTransition(
        GameManagerService gameManager,
        string holder,
        Func<Task> request
    )
    {
        gameManager.Disruption.TryAcquire(holder);
        Task task;
        try
        {
            task = request();
        }
        catch
        {
            // A synchronous throw (ExitToTitle itself) would otherwise leave the lease held
            // until restart, with every later transition answering 409.
            gameManager.Disruption.Release(holder);
            throw;
        }

        return task.ContinueWith(
                t =>
                {
                    gameManager.Disruption.Release(holder);
                    return t;
                },
                TaskContinuationOptions.ExecuteSynchronously
            )
            .Unwrap();
    }

    #endregion

    #region Web Admin & Farm Endpoints

    [ApiEndpoint(
        "POST",
        "/api/auth/register",
        Public = true,
        Summary = "Registers or links a player account using an in-game OTP code.",
        Tag = "Auth"
    )]
    [ApiResponse(typeof(Auth.WebSessionInfo), 200, Description = "Registration successful.")]
    private async Task HandlePostAuthRegisterAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_accountLinkingService == null)
        {
            await WriteJsonAsync(response, new { error = "Auth service unavailable." }, statusCode: 503);
            return;
        }

        try
        {
            using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
            var json = await reader.ReadToEndAsync();
            var body = JsonConvert.DeserializeObject<RegisterRequest>(json);

            if (body == null || string.IsNullOrWhiteSpace(body.Code) || string.IsNullOrWhiteSpace(body.Password))
            {
                await WriteJsonAsync(response, new { error = "Both 'code' and 'password' are required." }, statusCode: 400);
                return;
            }

            var (success, message, session) = await _accountLinkingService.RegisterWithOtpAsync(body.Code, body.Password);
            if (!success || session == null)
            {
                await WriteJsonAsync(response, new { error = message }, statusCode: 400);
                return;
            }

            await WriteJsonAsync(response, session);
        }
        catch (Exception ex)
        {
            Monitor.Log($"[API] Register error: {ex.Message}", LogLevel.Error);
            await WriteJsonAsync(response, new { error = "Internal server error." }, statusCode: 500);
        }
    }

    [ApiEndpoint(
        "POST",
        "/api/auth/login",
        Public = true,
        Summary = "Authenticates a web user or superadmin and returns a session token.",
        Tag = "Auth"
    )]
    [ApiResponse(typeof(Auth.WebSessionInfo), 200, Description = "Login successful.")]
    private async Task HandlePostAuthLoginAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_accountLinkingService == null)
        {
            await WriteJsonAsync(response, new { error = "Auth service unavailable." }, statusCode: 503);
            return;
        }

        try
        {
            using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
            var json = await reader.ReadToEndAsync();
            var body = JsonConvert.DeserializeObject<LoginRequest>(json);

            if (body == null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
            {
                await WriteJsonAsync(response, new { error = "Both 'username' and 'password' are required." }, statusCode: 400);
                return;
            }

            var (success, message, session) = await _accountLinkingService.LoginAsync(body.Username, body.Password);
            if (!success || session == null)
            {
                await WriteJsonAsync(response, new { error = message }, statusCode: 401);
                return;
            }

            await WriteJsonAsync(response, session);
        }
        catch (Exception ex)
        {
            Monitor.Log($"[API] Login error: {ex.Message}", LogLevel.Error);
            await WriteJsonAsync(response, new { error = "Internal server error." }, statusCode: 500);
        }
    }

    [ApiEndpoint(
        "GET",
        "/api/auth/me",
        Summary = "Returns authenticated user identity and role.",
        Tag = "Auth"
    )]
    private async Task HandleGetAuthMeAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var authHeader = request.Headers["Authorization"];
        var token = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authHeader.Substring(7).Trim()
            : null;

        if (string.IsNullOrEmpty(token))
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (token == Env.ApiKey)
        {
            await WriteJsonAsync(response, new
            {
                authenticated = true,
                farmerId = 0,
                username = Env.AdminUsername,
                role = "SUPERADMIN"
            });
            return;
        }

        if (_accountLinkingService != null)
        {
            var session = await _accountLinkingService.ValidateSessionAsync(token);
            if (session != null)
            {
                await WriteJsonAsync(response, new
                {
                    authenticated = true,
                    farmerId = session.FarmerId,
                    username = session.Username,
                    role = session.Role
                });
                return;
            }
        }

        await WriteUnauthorizedAsync(response);
    }

    [ApiEndpoint(
        "GET",
        "/farm/crops",
        Public = true,
        Summary = "Returns real-time cached crop state across the farm.",
        Tag = "Farm"
    )]
    [ApiResponse(typeof(CropTracker.FarmCropsSnapshot), 200, Description = "Snapshot of all crops currently planted.")]
    private CropTracker.FarmCropsSnapshot HandleGetFarmCrops()
    {
        if (_cropTrackerService != null)
        {
            return _cropTrackerService.GetLatestSnapshot();
        }

        return new CropTracker.FarmCropsSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow
        };
    }

    [ApiEndpoint(
        "GET",
        "/weather",
        Public = true,
        Summary = "Returns current weather condition and tomorrow's forecast.",
        Tag = "Farm"
    )]
    [ApiResponse(typeof(WeatherResponse), 200, Description = "Current and forecast weather condition.")]
    private WeatherResponse HandleGetWeather()
    {
        return new WeatherResponse
        {
            Today = GetCurrentWeatherSummary(),
            Tomorrow = GetTomorrowWeatherForecast(),
            Day = Game1.dayOfMonth,
            Season = Game1.currentSeason ?? "spring",
            Year = Game1.year,
            IsRaining = Game1.isRaining,
            IsLightning = Game1.isLightning,
            IsSnowing = Game1.isSnowing,
            IsDebris = Game1.isDebrisWeather,
            IsFestival = Utility.isFestivalDay(Game1.dayOfMonth, Game1.season)
        };
    }

    private static string GetCurrentWeatherSummary()
    {
        try
        {
            if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.season))
            {
                return "Festival";
            }
            if (Game1.isLightning)
            {
                return "Thunderstorm";
            }
            if (Game1.isRaining)
            {
                return "Rain";
            }
            if (Game1.isSnowing)
            {
                return "Snow";
            }
            if (Game1.isDebrisWeather)
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

    private static string GetTomorrowWeatherForecast()
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

    #endregion

    #region Response Helpers

    private static async Task WriteJsonAsync(HttpListenerResponse response, object data, int statusCode = 200)
    {
        if (statusCode != 200)
        {
            response.StatusCode = statusCode;
        }
        else if (response.StatusCode == 0)
        {
            response.StatusCode = 200;
        }
        var json = JsonConvert.SerializeObject(data, JsonSettings);
        await WriteJsonRawAsync(response, json);
    }

    private static async Task WriteJsonRawAsync(HttpListenerResponse response, string json)
    {
        response.ContentType = "application/json";
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-API-Key, X-Request-Id, X-File-Name");
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private static async Task WriteHtmlAsync(HttpListenerResponse response, string html)
    {
        response.ContentType = "text/html";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    [ApiEndpoint(
        "GET",
        "/swagger/v1/swagger.json",
        Summary = "OpenAPI specification",
        Tag = "Docs",
        Public = true
    )]
    private string HandleGetOpenApiSpec() => _openApiSpec ?? "{}";

    [ApiEndpoint("GET", "/docs", Summary = "API documentation UI", Tag = "Docs", Public = true)]
    private string HandleGetDocs()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <title>Stardew Dedicated Server API</title>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
</head>
<body>
    <script id=""api-reference"" data-url=""swagger/v1/swagger.json""></script>
    <script src=""https://cdn.jsdelivr.net/npm/@scalar/api-reference""></script>
</body>
</html>";
    }

    #endregion

    #region Phase 8 Operations & Chat Relay

    public class ChatHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Author { get; set; } = "";
        public string Message { get; set; } = "";
        public string Source { get; set; } = "in-game";
        public string TimestampUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    private static readonly List<ChatHistoryEntry> _chatHistory = new();
    private static readonly object _chatHistoryLock = new();

    public static void AddChatMessage(string author, string message, string source = "in-game")
    {
        lock (_chatHistoryLock)
        {
            _chatHistory.Add(new ChatHistoryEntry
            {
                Author = author,
                Message = message,
                Source = source,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
            if (_chatHistory.Count > 100)
            {
                _chatHistory.RemoveAt(0);
            }
        }
    }

    public static List<ChatHistoryEntry> GetChatHistory()
    {
        lock (_chatHistoryLock)
        {
            return _chatHistory.ToList();
        }
    }

    private async Task<(bool Authenticated, long FarmerId, string Username, string Role)?> GetUserFromRequestAsync(HttpListenerRequest request)
    {
        string? token = null;
        var authHeader = request.Headers["Authorization"];
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            token = authHeader.Substring(7).Trim();
        }
        else if (!string.IsNullOrEmpty(request.Headers["X-API-Key"]))
        {
            token = request.Headers["X-API-Key"]?.Trim();
        }
        else if (!string.IsNullOrEmpty(request.QueryString["token"]))
        {
            token = request.QueryString["token"]?.Trim();
        }
        else if (!string.IsNullOrEmpty(request.QueryString["key"]))
        {
            token = request.QueryString["key"]?.Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (token == Env.ApiKey)
        {
            return (true, 0, Env.AdminUsername, "SUPERADMIN");
        }

        if (_accountLinkingService != null)
        {
            var session = await _accountLinkingService.ValidateSessionAsync(token);
            if (session != null)
            {
                return (true, session.FarmerId, session.Username, session.Role);
            }
        }

        return null;
    }

    public class FarmAnimalInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int Age { get; set; }
        public int Friendship { get; set; }
        public int Hearts => Friendship / 200;
        public bool WasPet { get; set; }
        public bool WasFed { get; set; }
        public string? CurrentProduce { get; set; }
        public string Location { get; set; } = "";
        public string HomeBuilding { get; set; } = "";
    }

    private async Task<List<FarmAnimalInfo>> HandleGetFarmAnimalsAsync()
    {
        var list = new List<FarmAnimalInfo>();
        if (!Game1.hasLoadedGame)
        {
            return list;
        }

        await RunOnGameThreadAsync(() =>
        {
            try
            {
                var farm = Game1.getFarm();
                if (farm == null) return;

                foreach (var animal in farm.animals.Values)
                {
                    if (animal == null) continue;
                    list.Add(new FarmAnimalInfo
                    {
                        Name = animal.Name,
                        Type = animal.type.Value,
                        Age = animal.age.Value,
                        Friendship = animal.friendshipTowardFarmer.Value,
                        WasPet = animal.wasPet.Value,
                        WasFed = animal.fullness.Value > 0,
                        CurrentProduce = !string.IsNullOrEmpty(animal.currentProduce.Value) ? animal.currentProduce.Value : null,
                        Location = "Farm",
                        HomeBuilding = animal.home?.buildingType.Value ?? "Barn/Coop"
                    });
                }

                foreach (var building in farm.buildings)
                {
                    if (building.GetIndoors() is AnimalHouse animalHouse)
                    {
                        foreach (var animal in animalHouse.animals.Values)
                        {
                            if (animal == null) continue;
                            list.Add(new FarmAnimalInfo
                            {
                                Name = animal.Name,
                                Type = animal.type.Value,
                                Age = animal.age.Value,
                                Friendship = animal.friendshipTowardFarmer.Value,
                                WasPet = animal.wasPet.Value,
                                WasFed = animal.fullness.Value > 0,
                                CurrentProduce = !string.IsNullOrEmpty(animal.currentProduce.Value) ? animal.currentProduce.Value : null,
                                Location = building.GetIndoors().NameOrUniqueName,
                                HomeBuilding = building.buildingType.Value
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error reading farm animals: {ex.Message}", LogLevel.Error);
            }
        });

        return list;
    }

    private async Task HandlePostChatSendAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        string message = "";
        string? queryMsg = request.QueryString["message"];
        if (!string.IsNullOrEmpty(queryMsg))
        {
            message = queryMsg.Trim();
        }
        else
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var bodyText = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    var jObj = JObject.Parse(bodyText);
                    message = jObj["message"]?.ToString()?.Trim() ?? "";
                }
                catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Parameter 'message' cannot be empty" });
            return;
        }

        string author = user.Value.Username;
        BroadcastChatMessage(author, message, "web");

        await RunOnGameThreadAsync(() =>
        {
            OnExternalChatMessage?.Invoke(author, message);
        });

        await WriteJsonAsync(response, new
        {
            success = true,
            author = author,
            message = message,
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }

    private async Task HandlePostConsoleExecuteAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (user.Value.Role != "SUPERADMIN")
        {
            response.StatusCode = 403;
            await WriteJsonAsync(response, new { error = "Superadmin role required to execute console commands." });
            return;
        }

        string commandText = "";
        string? queryCmd = request.QueryString["command"];
        if (!string.IsNullOrEmpty(queryCmd))
        {
            commandText = queryCmd.Trim();
        }
        else
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var bodyText = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    var jObj = JObject.Parse(bodyText);
                    commandText = jObj["command"]?.ToString()?.Trim() ?? "";
                }
                catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(commandText))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Parameter 'command' cannot be empty" });
            return;
        }

        var parts = commandText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0];
        var args = parts.Skip(1).ToArray();

        bool executed = false;
        string output = "";

        await RunOnGameThreadAsync(() =>
        {
            try
            {
                if (cmd.StartsWith("!"))
                {
                    OnExternalChatMessage?.Invoke(user.Value.Username, commandText);
                    executed = true;
                    output = $"Broadcasted chat command '{commandText}'";
                }
                else
                {
                    OnExternalChatMessage?.Invoke(user.Value.Username, commandText);
                    executed = true;
                    output = $"Broadcasted command '{commandText}' to server chat";
                }
            }
            catch (Exception ex)
            {
                output = $"Execution error: {ex.Message}";
            }
        });

        await WriteJsonAsync(response, new
        {
            success = executed,
            command = commandText,
            output = output,
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }

    private async Task HandlePostServerPauseAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_pauseService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Pause service not available." });
            return;
        }

        var user = await GetUserFromRequestAsync(request);
        string initiatorName = user?.Username ?? "Web Admin";

        string reason = "Web Admin Pause";
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var bodyText = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                var bodyObj = Newtonsoft.Json.Linq.JObject.Parse(bodyText);
                if (bodyObj["reason"] != null)
                {
                    reason = bodyObj["reason"]!.ToString();
                }
            }
        }
        catch
        {
            // Ignore parse errors, default reason used
        }

        await RunOnGameThreadAsync(() =>
        {
            _pauseService.PauseGame(initiatorName, reason);
        });

        await WriteJsonAsync(response, new
        {
            success = true,
            message = $"Server paused by {initiatorName}.",
            isPaused = true,
            reason,
            pausedBy = initiatorName
        });
    }

    private async Task HandlePostServerResumeAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_pauseService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Pause service not available." });
            return;
        }

        var user = await GetUserFromRequestAsync(request);
        string initiatorName = user?.Username ?? "Web Admin";

        await RunOnGameThreadAsync(() =>
        {
            _pauseService.ResumeGame(initiatorName);
        });

        await WriteJsonAsync(response, new
        {
            success = true,
            message = $"Server resumed by {initiatorName}.",
            isPaused = false
        });
    }

    private async Task HandlePostModPolicyAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null || (user.Value.Role != "SUPERADMIN" && user.Value.Role != "ADMIN"))
        {
            response.StatusCode = 403;
            await WriteJsonAsync(response, new { error = "Administrator role required." });
            return;
        }

        if (_modEnforcementService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Mod enforcement service is not active." });
            return;
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var newPolicy = JsonConvert.DeserializeObject<ModManagement.ModPolicyData>(body);
            if (newPolicy == null)
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "Invalid policy JSON payload." });
                return;
            }

            _modEnforcementService.UpdatePolicy(newPolicy);
            await WriteJsonAsync(response, new { success = true, policy = _modEnforcementService.GetPolicy() });
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandlePostModPolicySyncSveAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null || (user.Value.Role != "SUPERADMIN" && user.Value.Role != "ADMIN"))
        {
            response.StatusCode = 403;
            await WriteJsonAsync(response, new { error = "Administrator role required." });
            return;
        }

        if (_modEnforcementService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Mod enforcement service is not active." });
            return;
        }

        int added = _modEnforcementService.AutoSyncServerMods();
        await WriteJsonAsync(response, new
        {
            success = true,
            addedCount = added,
            message = $"Successfully synced {added} mod(s) to required policy.",
            policy = _modEnforcementService.GetPolicy()
        });
    }

    private async Task HandlePostModUploadAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null || (user.Value.Role != "SUPERADMIN" && user.Value.Role != "ADMIN"))
        {
            response.StatusCode = 403;
            await WriteJsonAsync(response, new { error = "Hanya Administrator / Sysadmin yang berhak mengunggah mod ke server." });
            return;
        }

        if (_modDistributionService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Layanan distribusi mod tidak aktif." });
            return;
        }

        try
        {
            string fileName = request.Headers["X-File-Name"] ?? request.QueryString["filename"] ?? "uploaded_mod.zip";
            fileName = Uri.UnescapeDataString(fileName);

            var (success, message, installedMods) = await _modDistributionService.InstallModZipAsync(request.InputStream, fileName);
            if (!success)
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { success = false, error = message });
                return;
            }

            await WriteJsonAsync(response, new
            {
                success = true,
                message = message,
                installedMods = installedMods
            });
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleDeleteCustomModAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null || (user.Value.Role != "SUPERADMIN" && user.Value.Role != "ADMIN"))
        {
            response.StatusCode = 403;
            await WriteJsonAsync(response, new { error = "Hanya Administrator / Sysadmin yang berhak menghapus mod server." });
            return;
        }

        string? folderName = request.QueryString["folder"];
        if (string.IsNullOrEmpty(folderName) && request.Url != null)
        {
            var segments = request.Url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && segments[0] == "api" && segments[1] == "mods" && segments[2] == "custom")
            {
                folderName = Uri.UnescapeDataString(segments[3]);
            }
        }

        if (string.IsNullOrEmpty(folderName))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Parameter 'folder' diperlukan." });
            return;
        }

        bool success = _modDistributionService?.DeleteCustomMod(folderName) ?? false;
        if (success)
        {
            await WriteJsonAsync(response, new { success = true, deleted = folderName });
        }
        else
        {
            response.StatusCode = 404;
            await WriteJsonAsync(response, new { error = "Mod kustom tidak ditemukan atau gagal dihapus." });
        }
    }

    private static bool IsPublicSyncPath(string path)
    {
        return path == "/api/mods/sync/manifest"
            || path == "/api/mods/sync/bundle"
            || path == "/api/mods/sync/client-mod"
            || path.StartsWith("/api/mods/sync/download");
    }

    private async Task HandleGetModBundleZipAsync(HttpListenerResponse response)
    {
        if (_modDistributionService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Mod distribution service is not active." });
            return;
        }

        var zipBytes = _modDistributionService.GetBundleZip();
        if (zipBytes == null || zipBytes.Length == 0)
        {
            response.StatusCode = 404;
            await WriteJsonAsync(response, new { error = "No client-compatible mods found to package." });
            return;
        }

        response.ContentType = "application/zip";
        response.Headers["Content-Disposition"] = "attachment; filename=\"stardew-server-mods.zip\"";
        response.ContentLength64 = zipBytes.Length;
        await response.OutputStream.WriteAsync(zipBytes, 0, zipBytes.Length);
    }

    private async Task HandleGetModZipDownloadAsync(HttpListenerRequest request, HttpListenerResponse response, string? folderParam = null)
    {
        if (_modDistributionService == null)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Mod distribution service is not active." });
            return;
        }

        string? folder = folderParam ?? request.QueryString["folder"];
        if (string.IsNullOrWhiteSpace(folder))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Parameter 'folder' is required." });
            return;
        }

        var zipBytes = _modDistributionService.GetModZip(folder);
        if (zipBytes == null || zipBytes.Length == 0)
        {
            response.StatusCode = 404;
            await WriteJsonAsync(response, new { error = $"Mod package '{folder}' not found." });
            return;
        }

        response.ContentType = "application/zip";
        response.Headers["Content-Disposition"] = $"attachment; filename=\"{folder}.zip\"";
        response.ContentLength64 = zipBytes.Length;
        await response.OutputStream.WriteAsync(zipBytes, 0, zipBytes.Length);
    }

    private async Task HandleGetClientModZipAsync(HttpListenerResponse response)
    {
        var searchPaths = new[]
        {
            Path.Combine(Helper.DirectoryPath, "JunimoClientSync.zip"),
            Path.Combine(Path.GetDirectoryName(Helper.DirectoryPath) ?? "", "JunimoClientSync.zip"),
            Path.Combine(Directory.GetCurrentDirectory(), "mods", "JunimoClientSync.zip"),
            Path.Combine(Directory.GetCurrentDirectory(), "JunimoClientSync.zip")
        };

        string? foundPath = searchPaths.FirstOrDefault(File.Exists);
        if (foundPath != null)
        {
            var bytes = await File.ReadAllBytesAsync(foundPath);
            response.ContentType = "application/zip";
            response.Headers["Content-Disposition"] = "attachment; filename=\"JunimoClientSync.zip\"";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            return;
        }

        response.StatusCode = 404;
        await WriteJsonAsync(response, new { error = "Client sync mod package (JunimoClientSync.zip) not found." });
    }

    private async Task HandleGetFarmerProfileAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (!Game1.hasLoadedGame)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Save game is not loaded yet." });
            return;
        }

        FarmerProfileResponse? profile = null;
        await RunOnGameThreadAsync(() =>
        {
            try
            {
                Farmer? target = null;
                if (user.Value.FarmerId != 0)
                {
                    target = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == user.Value.FarmerId);
                }

                // Fallback for Superadmin or if not found: host farmer
                if (target == null)
                {
                    target = Game1.player;
                }

                if (target == null) return;

                profile = new FarmerProfileResponse
                {
                    Id = target.UniqueMultiplayerID,
                    Name = target.Name,
                    FarmName = target.farmName.Value,
                    FavoriteThing = target.favoriteThing.Value,
                    IsMale = target.IsMale,
                    Money = target.Money,
                    ClubCoins = target.clubCoins,
                    QiGems = target.QiGems,
                    Health = target.health,
                    MaxHealth = target.maxHealth,
                    Stamina = (float)Math.Round(target.Stamina, 1),
                    MaxStamina = target.MaxStamina,
                    CurrentLocation = target.currentLocation?.NameOrUniqueName ?? "Unknown",
                    TileX = target.TilePoint.X,
                    TileY = target.TilePoint.Y
                };

                // Skills helper
                int GetExp(int index)
                {
                    try
                    {
                        if (target.experiencePoints != null && index < target.experiencePoints.Count)
                        {
                            return target.experiencePoints[index];
                        }
                    }
                    catch { }
                    return 0;
                }

                profile.Skills["Farming"] = new CharacterSkillInfo { Level = target.FarmingLevel, Experience = GetExp(0) };
                profile.Skills["Fishing"] = new CharacterSkillInfo { Level = target.FishingLevel, Experience = GetExp(1) };
                profile.Skills["Foraging"] = new CharacterSkillInfo { Level = target.ForagingLevel, Experience = GetExp(2) };
                profile.Skills["Mining"] = new CharacterSkillInfo { Level = target.MiningLevel, Experience = GetExp(3) };
                profile.Skills["Combat"] = new CharacterSkillInfo { Level = target.CombatLevel, Experience = GetExp(4) };
                profile.Skills["Luck"] = new CharacterSkillInfo { Level = target.LuckLevel, Experience = GetExp(5) };

                // Inventory (36 slots)
                if (target.Items != null)
                {
                    for (int i = 0; i < target.Items.Count && i < 36; i++)
                    {
                        var item = target.Items[i];
                        if (item == null) continue;

                        int q = 0;
                        if (item is StardewValley.Object sdvObj)
                        {
                            q = sdvObj.Quality;
                        }
                        else
                        {
                            try { q = item.Quality; } catch { }
                        }

                        string cat = "";
                        try { cat = item.getCategoryName(); } catch { }

                        profile.Inventory.Add(new InventoryItemInfo
                        {
                            SlotIndex = i,
                            ItemId = item.QualifiedItemId ?? item.ItemId ?? "",
                            Name = item.Name ?? "",
                            DisplayName = item.DisplayName ?? item.Name ?? "",
                            Stack = item.Stack,
                            Quality = q,
                            CategoryName = cat ?? ""
                        });
                    }
                }

                // Equipment
                try
                {
                    if (target.hat.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "Hat", ItemId = target.hat.Value.QualifiedItemId ?? "", Name = target.hat.Value.DisplayName ?? "Hat" });
                    if (target.shirtItem.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "Shirt", ItemId = target.shirtItem.Value.QualifiedItemId ?? "", Name = target.shirtItem.Value.DisplayName ?? "Shirt" });
                    if (target.pantsItem.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "Pants", ItemId = target.pantsItem.Value.QualifiedItemId ?? "", Name = target.pantsItem.Value.DisplayName ?? "Pants" });
                    if (target.boots.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "Boots", ItemId = target.boots.Value.QualifiedItemId ?? "", Name = target.boots.Value.DisplayName ?? "Boots" });
                    if (target.leftRing.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "LeftRing", ItemId = target.leftRing.Value.QualifiedItemId ?? "", Name = target.leftRing.Value.DisplayName ?? "Ring" });
                    if (target.rightRing.Value != null)
                        profile.Equipment.Add(new EquipmentItemInfo { Slot = "RightRing", ItemId = target.rightRing.Value.QualifiedItemId ?? "", Name = target.rightRing.Value.DisplayName ?? "Ring" });
                }
                catch (Exception ex)
                {
                    Monitor.Log($"[API] Error reading equipment: {ex.Message}", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error reading farmer profile: {ex.Message}", LogLevel.Error);
            }
        });

        if (profile == null)
        {
            response.StatusCode = 404;
            await WriteJsonAsync(response, new { error = "Farmer character not found in active save." });
            return;
        }

        await WriteJsonAsync(response, profile);
    }

    private async Task HandleGetFarmerSocialAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (!Game1.hasLoadedGame)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Save game is not loaded yet." });
            return;
        }

        FarmerSocialResponse? socialData = null;
        await RunOnGameThreadAsync(() =>
        {
            try
            {
                Farmer? target = null;
                if (user.Value.FarmerId != 0)
                {
                    target = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == user.Value.FarmerId);
                }
                if (target == null)
                {
                    target = Game1.player;
                }

                socialData = new FarmerSocialResponse
                {
                    FarmerId = target?.UniqueMultiplayerID ?? 0,
                    FarmerName = target?.Name ?? "Server",
                    Villagers = new List<VillagerFriendshipDto>()
                };

                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (target != null && target.friendshipData != null)
                {
                    foreach (var pair in target.friendshipData.Pairs)
                    {
                        string npcName = pair.Key;
                        var friendship = pair.Value;
                        if (friendship == null) continue;
                        known.Add(npcName);

                        int maxH = 10;
                        string bSeason = "";
                        int bDay = 0;
                        bool isDatable = false;
                        string gender = "";
                        string dispName = npcName;
                        bool isBirthday = false;

                        try
                        {
                            NPC? npc = Game1.getCharacterFromName(npcName);
                            if (npc != null)
                            {
                                dispName = npc.displayName ?? npcName;
                                bSeason = npc.Birthday_Season ?? "";
                                bDay = npc.Birthday_Day;
                                isDatable = npc.datable.Value;
                                gender = npc.Gender.ToString();
                                isBirthday = string.Equals(npc.Birthday_Season, Game1.currentSeason, StringComparison.OrdinalIgnoreCase) && npc.Birthday_Day == Game1.dayOfMonth;

                                if (friendship.Status == FriendshipStatus.Married)
                                    maxH = 14;
                                else if (npc.datable.Value && friendship.Status != FriendshipStatus.Dating)
                                    maxH = 8;
                                else
                                    maxH = 10;
                            }
                            else if (Game1.characterData != null && Game1.characterData.TryGetValue(npcName, out var cd))
                            {
                                dispName = cd.DisplayName ?? npcName;
                                bSeason = cd.BirthSeason ?? "";
                                bDay = cd.BirthDay;
                                isDatable = cd.CanSocialize && cd.Gender != Gender.Undefined;
                                gender = cd.Gender.ToString();
                                isBirthday = string.Equals(cd.BirthSeason, Game1.currentSeason, StringComparison.OrdinalIgnoreCase) && cd.BirthDay == Game1.dayOfMonth;
                                maxH = isDatable ? 8 : 10;
                            }
                        }
                        catch { }

                        socialData.Villagers.Add(new VillagerFriendshipDto
                        {
                            Name = npcName,
                            DisplayName = dispName,
                            Points = friendship.Points,
                            Hearts = Math.Min(maxH, friendship.Points / 250),
                            MaxHearts = maxH,
                            GiftsThisWeek = friendship.GiftsThisWeek,
                            MaxGiftsPerWeek = 2,
                            GiftGivenToday = friendship.GiftsToday > 0,
                            TalkedToToday = friendship.TalkedToToday,
                            Status = friendship.Status.ToString(),
                            BirthdaySeason = bSeason,
                            BirthdayDay = bDay,
                            IsBirthdayToday = isBirthday,
                            IsDatable = isDatable,
                            Gender = gender
                        });
                    }
                }

                // Add standard town NPCs not met yet
                if (Game1.characterData != null)
                {
                    foreach (var pair in Game1.characterData)
                    {
                        string npcName = pair.Key;
                        var cd = pair.Value;
                        if (known.Contains(npcName)) continue;
                        if (!cd.CanSocialize) continue;
                        if (cd.HomeRegion == "Other" && npcName != "Krobus" && npcName != "Dwarf" && npcName != "Sandy" && npcName != "Leo") continue;

                        bool isDatable = cd.CanSocialize && cd.Gender != Gender.Undefined;
                        bool isBirthday = string.Equals(cd.BirthSeason, Game1.currentSeason, StringComparison.OrdinalIgnoreCase) && cd.BirthDay == Game1.dayOfMonth;

                        socialData.Villagers.Add(new VillagerFriendshipDto
                        {
                            Name = npcName,
                            DisplayName = cd.DisplayName ?? npcName,
                            Points = 0,
                            Hearts = 0,
                            MaxHearts = isDatable ? 8 : 10,
                            GiftsThisWeek = 0,
                            MaxGiftsPerWeek = 2,
                            GiftGivenToday = false,
                            TalkedToToday = false,
                            Status = "Friendly",
                            BirthdaySeason = cd.BirthSeason ?? "",
                            BirthdayDay = cd.BirthDay,
                            IsBirthdayToday = isBirthday,
                            IsDatable = isDatable,
                            Gender = cd.Gender.ToString()
                        });
                    }
                }

                socialData.Villagers = socialData.Villagers
                    .OrderByDescending(v => v.IsBirthdayToday)
                    .ThenByDescending(v => v.Points)
                    .ThenBy(v => v.DisplayName)
                    .ToList();

                socialData.TotalVillagersMet = socialData.Villagers.Count(v => v.Points > 0 || v.TalkedToToday);
                socialData.MaxHeartsAchieved = socialData.Villagers.Count(v => v.Hearts >= v.MaxHearts && v.MaxHearts > 0);
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error reading social data: {ex.Message}", LogLevel.Error);
            }
        });

        if (socialData == null)
        {
            socialData = new FarmerSocialResponse
            {
                FarmerId = user.Value.FarmerId,
                FarmerName = user.Value.Username ?? "Farmer",
                Villagers = new List<VillagerFriendshipDto>()
            };
        }

        await WriteJsonAsync(response, socialData);
    }

    private async Task HandleGetFarmerQuestsAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (!Game1.hasLoadedGame)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Save game is not loaded yet." });
            return;
        }

        FarmerQuestsResponse? questsData = null;
        await RunOnGameThreadAsync(() =>
        {
            try
            {
                Farmer? target = null;
                if (user.Value.FarmerId != 0)
                {
                    target = Game1.getAllFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == user.Value.FarmerId);
                }
                if (target == null)
                {
                    target = Game1.player;
                }

                questsData = new FarmerQuestsResponse
                {
                    FarmerId = target?.UniqueMultiplayerID ?? 0,
                    FarmerName = target?.Name ?? "Server",
                    ActiveQuests = new List<QuestDto>(),
                    SpecialOrders = new List<SpecialOrderDto>()
                };

                // 1. Personal Quests
                if (target != null && target.questLog != null)
                {
                    foreach (var q in target.questLog)
                    {
                        if (q == null) continue;
                        try
                        {
                            questsData.ActiveQuests.Add(new QuestDto
                            {
                                Id = q.id?.Value ?? "",
                                Title = q.questTitle ?? "Misi Lembah",
                                Description = q.questDescription ?? "",
                                CurrentObjective = q.currentObjective ?? "",
                                RewardMoney = q.moneyReward?.Value ?? 0,
                                DaysLeft = q.daysLeft?.Value ?? 0,
                                IsDailyQuest = q.dailyQuest?.Value ?? false,
                                IsComplete = q.completed?.Value ?? false,
                                QuestType = q.questType?.Value.ToString() ?? "Standard"
                            });
                        }
                        catch { }
                    }
                }

                // 2. Special Orders
                if (Game1.player?.team?.specialOrders != null)
                {
                    foreach (var so in Game1.player.team.specialOrders)
                    {
                        if (so == null) continue;
                        try
                        {
                            var sDto = new SpecialOrderDto
                            {
                                Key = so.orderKey?.Value ?? "",
                                Name = so.questName?.Value ?? "Special Order",
                                Description = so.questDescription?.Value ?? "",
                                OrderType = so.orderType?.Value ?? "",
                                Requester = so.requester?.Value ?? "",
                                DaysLeft = Math.Max(0, (so.dueDate?.Value ?? Game1.dayOfMonth) - Game1.dayOfMonth),
                                IsComplete = false,
                                Objectives = new List<QuestObjectiveDto>()
                            };

                            if (so.objectives != null)
                            {
                                bool allComplete = true;
                                foreach (var obj in so.objectives)
                                {
                                    if (obj == null) continue;
                                    try
                                    {
                                        bool c = obj.IsComplete();
                                        if (!c) allComplete = false;
                                        sDto.Objectives.Add(new QuestObjectiveDto
                                        {
                                            Description = obj.GetDescription() ?? "",
                                            CurrentCount = obj.GetCount(),
                                            MaxCount = obj.GetMaxCount(),
                                            IsComplete = c
                                        });
                                    }
                                    catch { }
                                }
                                sDto.IsComplete = allComplete;
                            }

                            questsData.SpecialOrders.Add(sDto);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error reading quests data: {ex.Message}", LogLevel.Error);
            }
        });

        if (questsData == null)
        {
            questsData = new FarmerQuestsResponse
            {
                FarmerId = user.Value.FarmerId,
                FarmerName = user.Value.Username ?? "Farmer",
                ActiveQuests = new List<QuestDto>(),
                SpecialOrders = new List<SpecialOrderDto>()
            };
        }

        await WriteJsonAsync(response, questsData);
    }

    private async Task HandleGetCommunityCenterAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (!Game1.hasLoadedGame)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Save game is not loaded yet." });
            return;
        }

        CommunityCenterResponse? ccData = null;
        await RunOnGameThreadAsync(() =>
        {
            try
            {
                ccData = new CommunityCenterResponse
                {
                    Rooms = new List<CommunityRoomDto>(),
                    JojaProjects = new List<JojaProjectDto>()
                };

                bool isJoja = (Game1.player?.mailReceived?.Contains("JojaMember") == true) ||
                              (Game1.player?.mailReceived?.Contains("jojaVault") == true) ||
                              (Game1.player != null && Game1.player.hasCompletedTownProject("JojaMember"));
                ccData.IsJojaRoute = isJoja;

                if (isJoja)
                {
                    ccData.JojaProjects = new List<JojaProjectDto>
                    {
                        new JojaProjectDto { Key = "jojaGreenhouse", Name = "Rumah Kaca (Greenhouse)", Description = "Membuka kembali Rumah Kaca di kebun untuk bercocok tanam sepanjang tahun.", Cost = 35000, IsComplete = Game1.player?.mailReceived?.Contains("jojaGreenhouse") == true },
                        new JojaProjectDto { Key = "jojaBoilerRoom", Name = "Kereta Tambang (Minecarts)", Description = "Memperbaiki jaringan gerobak tambang untuk perjalanan instan antara Kota, Tambang, dan Halte Bus.", Cost = 15000, IsComplete = Game1.player?.mailReceived?.Contains("jojaBoilerRoom") == true },
                        new JojaProjectDto { Key = "jojaCraftsRoom", Name = "Jembatan Tambang (Bridge Repair)", Description = "Memperbaiki jembatan menuju Tambang Batu (Quarry).", Cost = 25000, IsComplete = Game1.player?.mailReceived?.Contains("jojaCraftsRoom") == true },
                        new JojaProjectDto { Key = "jojaVault", Name = "Perbaikan Bus (Bus Repair)", Description = "Memperbaiki bus Pam untuk perjalanan ke Gurun Calico.", Cost = 40000, IsComplete = Game1.player?.mailReceived?.Contains("jojaVault") == true },
                        new JojaProjectDto { Key = "jojaFishTank", Name = "Pendulangan Emas (Panning)", Description = "Menghilangkan batu besar berkilau di hulu sungai untuk memungkinkan pendulangan bijih mineral.", Cost = 20000, IsComplete = Game1.player?.mailReceived?.Contains("jojaFishTank") == true },
                        new JojaProjectDto { Key = "ccMovieTheater", Name = "Bioskop Lembah (Movie Theater)", Description = "Membangun bioskop mewah di bekas bangunan JojaMart.", Cost = 500000, IsComplete = Game1.player?.mailReceived?.Contains("ccMovieTheater") == true }
                    };
                    ccData.IsComplete = ccData.JojaProjects.All(p => p.IsComplete);
                    ccData.TotalBundles = ccData.JojaProjects.Count;
                    ccData.CompletedBundles = ccData.JojaProjects.Count(p => p.IsComplete);
                }

                CommunityCenter? cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
                if (cc == null)
                {
                    cc = Game1.locations?.OfType<CommunityCenter>()?.FirstOrDefault();
                }

                var roomNames = new Dictionary<int, (string Name, string DisplayName, string Reward)>
                {
                    { 0, ("Pantry", "Ruang Persediaan (Pantry)", "Greenhouse (Rumah Kaca)") },
                    { 1, ("Crafts Room", "Ruang Kerajinan (Crafts Room)", "Bridge Repair (Jembatan Tambang)") },
                    { 2, ("Fish Tank", "Akuarium (Fish Tank)", "Glittering Boulder / Panning (Pendulangan)") },
                    { 3, ("Boiler Room", "Ruang Ketel (Boiler Room)", "Minecarts (Kereta Tambang Cepat)") },
                    { 4, ("Vault", "Kubah Uang (Vault)", "Bus Repair (Perbaikan Bus Gurun Calico)") },
                    { 5, ("Bulletin Board", "Papan Pengumuman (Bulletin Board)", "+500 Poin Persahabatan dengan Seluruh Warga") },
                    { 6, ("Abandoned JojaMart", "Toko Terbengkalai (Missing Bundle)", "Movie Theater (Bioskop Lembah)") }
                };

                var rooms = new Dictionary<int, CommunityRoomDto>();
                foreach (var r in roomNames)
                {
                    bool roomComplete = false;
                    try
                    {
                        if (cc != null && cc.areasComplete != null && r.Key < cc.areasComplete.Length)
                        {
                            roomComplete = cc.areasComplete[r.Key];
                        }
                    }
                    catch { }

                    rooms[r.Key] = new CommunityRoomDto
                    {
                        RoomIndex = r.Key,
                        Name = r.Value.Name,
                        DisplayName = r.Value.DisplayName,
                        Reward = r.Value.Reward,
                        IsComplete = roomComplete,
                        Bundles = new List<BundleDto>()
                    };
                }

                var bundleData = Game1.netWorldState?.Value?.BundleData;
                if (bundleData != null)
                {
                    foreach (var kvp in bundleData)
                    {
                        string key = kvp.Key;
                        string value = kvp.Value;
                        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;

                        var keyParts = key.Split('/');
                        int bIdx = 0;
                        int rIdx = 0;

                        if (keyParts.Length > 1 && int.TryParse(keyParts[1], out var parsedBIdx))
                        {
                            bIdx = parsedBIdx;
                            string rStr = keyParts[0].Trim();
                            var foundRoom = roomNames.FirstOrDefault(x => string.Equals(x.Value.Name, rStr, StringComparison.OrdinalIgnoreCase));
                            rIdx = foundRoom.Key;
                        }
                        else if (int.TryParse(keyParts[0], out var singleIdx))
                        {
                            bIdx = singleIdx;
                            if (bIdx >= 0 && bIdx <= 5) rIdx = 0;
                            else if (bIdx >= 13 && bIdx <= 18) rIdx = 1;
                            else if (bIdx >= 6 && bIdx <= 11) rIdx = 2;
                            else if (bIdx >= 20 && bIdx <= 22) rIdx = 3;
                            else if (bIdx >= 23 && bIdx <= 26) rIdx = 4;
                            else if (bIdx >= 31 && bIdx <= 35) rIdx = 5;
                            else if (bIdx == 36) rIdx = 6;
                        }

                        var valParts = value.Split('/');
                        if (valParts.Length < 3) continue;

                        string bName = valParts[0];
                        string rewardStr = valParts[1];
                        string ingredientsStr = valParts[2];
                        int reqCount = 0;
                        if (valParts.Length > 4 && int.TryParse(valParts[4], out var rc))
                        {
                            reqCount = rc;
                        }

                        var bDto = new BundleDto
                        {
                            BundleIndex = bIdx,
                            Name = bName,
                            RoomIndex = rIdx,
                            RoomName = rooms.TryGetValue(rIdx, out var roomDto) ? roomDto.Name : "Pantry",
                            Ingredients = new List<BundleIngredientDto>()
                        };

                        var tokens = ingredientsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        bool[]? fulfilledSlots = null;
                        try
                        {
                            if (cc != null && cc.bundles != null && cc.bundles.TryGetValue(bIdx, out var arr))
                            {
                                fulfilledSlots = arr;
                            }
                        }
                        catch { }

                        int slotIdx = 0;
                        int fulfilledCount = 0;
                        for (int i = 0; i + 2 < tokens.Length; i += 3)
                        {
                            string itemId = tokens[i];
                            int stack = int.TryParse(tokens[i + 1], out var s) ? s : 1;
                            int quality = int.TryParse(tokens[i + 2], out var q) ? q : 0;
                            bool slotDone = fulfilledSlots != null && slotIdx < fulfilledSlots.Length && fulfilledSlots[slotIdx];
                            if (slotDone) fulfilledCount++;

                            string itemName = itemId;
                            try
                            {
                                var itemData = ItemRegistry.GetData(itemId) ?? ItemRegistry.GetData("(O)" + itemId);
                                if (itemData != null) itemName = itemData.DisplayName;
                            }
                            catch { }

                            bDto.Ingredients.Add(new BundleIngredientDto
                            {
                                ItemId = itemId,
                                Name = itemName,
                                Stack = stack,
                                Quality = quality,
                                IsFulfilled = slotDone
                            });

                            slotIdx++;
                        }

                        if (reqCount <= 0) reqCount = bDto.Ingredients.Count;
                        bDto.RequiredItemsCount = reqCount;
                        bDto.FulfilledCount = fulfilledCount;
                        bool bundleComplete = false;
                        try
                        {
                            bundleComplete = (cc != null && cc.isBundleComplete(bIdx)) || (fulfilledCount >= reqCount);
                        }
                        catch
                        {
                            bundleComplete = fulfilledCount >= reqCount;
                        }
                        bDto.IsComplete = bundleComplete;

                        try
                        {
                            var rewTokens = rewardStr.Split(' ');
                            if (rewTokens.Length >= 2)
                            {
                                string rewId = rewTokens[1];
                                int rewStack = rewTokens.Length >= 3 && int.TryParse(rewTokens[2], out var rs) ? rs : 1;
                                var rewItem = ItemRegistry.GetData(rewId) ?? ItemRegistry.GetData("(O)" + rewId);
                                bDto.RewardName = rewItem?.DisplayName ?? rewId;
                                bDto.RewardCount = rewStack;
                            }
                            else
                            {
                                bDto.RewardName = rewardStr;
                            }
                        }
                        catch { bDto.RewardName = rewardStr; }

                        if (rooms.TryGetValue(rIdx, out var targetRoom))
                        {
                            targetRoom.Bundles.Add(bDto);
                        }
                    }
                }

                foreach (var r in rooms.Values)
                {
                    r.TotalBundles = r.Bundles.Count;
                    r.CompletedBundles = r.Bundles.Count(b => b.IsComplete);
                    if (r.TotalBundles > 0 && r.CompletedBundles >= r.TotalBundles)
                    {
                        r.IsComplete = true;
                    }
                    ccData.Rooms.Add(r);
                }

                ccData.TotalBundles = ccData.Rooms.Sum(r => r.TotalBundles);
                ccData.CompletedBundles = ccData.Rooms.Sum(r => r.CompletedBundles);
                bool allComplete = false;
                try
                {
                    allComplete = (cc != null && cc.areAllAreasComplete()) ||
                                  (ccData.TotalBundles > 0 && ccData.CompletedBundles >= ccData.TotalBundles);
                }
                catch
                {
                    allComplete = ccData.TotalBundles > 0 && ccData.CompletedBundles >= ccData.TotalBundles;
                }
                ccData.IsComplete = allComplete;
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error reading community center: {ex.Message}", LogLevel.Error);
            }
        });

        if (ccData == null)
        {
            ccData = new CommunityCenterResponse
            {
                Rooms = new List<CommunityRoomDto>(),
                JojaProjects = new List<JojaProjectDto>()
            };
        }

        await WriteJsonAsync(response, ccData);
    }

    private async Task HandleGetMapTrackingAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var user = await GetUserFromRequestAsync(request);
        if (user == null)
        {
            await WriteUnauthorizedAsync(response);
            return;
        }

        if (!Game1.hasLoadedGame)
        {
            response.StatusCode = 503;
            await WriteJsonAsync(response, new { error = "Save game is not loaded yet." });
            return;
        }

        var mapData = new MapTrackingResponse();
        await RunOnGameThreadAsync(() =>
        {
            try
            {
                var farm = Game1.getFarm();
                mapData.FarmType = Game1.GetFarmTypeKey() ?? "Standard";
                if (farm != null)
                {
                    mapData.FarmWidth = farm.Map?.Layers.Count > 0 ? farm.Map.Layers[0].LayerWidth : 80;
                    mapData.FarmHeight = farm.Map?.Layers.Count > 0 ? farm.Map.Layers[0].LayerHeight : 65;

                    // Farm buildings
                    foreach (var b in farm.buildings)
                    {
                        if (b == null) continue;

                        string bType = b.buildingType.Value ?? "Building";
                        bool isCabin = b.isCabin;
                        string? ownerName = null;
                        long? ownerId = null;
                        bool isLobby = LobbyService.IsLobbyCabin(b);
                        int animalCount = 0;

                        if (isCabin && b.GetIndoors<Cabin>() is Cabin cabin)
                        {
                            ownerName = cabin.owner?.Name;
                            ownerId = cabin.owner?.UniqueMultiplayerID;
                        }

                        if (b.GetIndoors() is AnimalHouse ah)
                        {
                            animalCount = ah.animals.Length;
                        }

                        mapData.Buildings.Add(new FarmBuildingPin
                        {
                            Name = isCabin ? (string.IsNullOrEmpty(ownerName) ? "Unassigned Cabin" : $"{ownerName}'s Cabin") : bType,
                            Type = bType,
                            TileX = b.tileX.Value,
                            TileY = b.tileY.Value,
                            TilesWide = b.tilesWide.Value,
                            TilesHigh = b.tilesHigh.Value,
                            IsCabin = isCabin,
                            OwnerName = ownerName,
                            OwnerId = ownerId,
                            IsLobby = isLobby,
                            AnimalCount = animalCount
                        });
                    }
                }

                // Connected and saved farmers
                foreach (var f in Game1.getAllFarmers())
                {
                    if (f.UniqueMultiplayerID == 0 && string.IsNullOrEmpty(f.Name)) continue;

                    bool isOnline = (f.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
                        || Game1.otherFarmers.ContainsKey(f.UniqueMultiplayerID);

                    string loc = f.currentLocation?.NameOrUniqueName ?? f.homeLocation.Value ?? "Farm";
                    string zone = ClassifyZone(loc);

                    mapData.Players.Add(new PlayerMapPin
                    {
                        Id = f.UniqueMultiplayerID,
                        Name = f.Name,
                        Location = loc,
                        Zone = zone,
                        TileX = f.TilePoint.X,
                        TileY = f.TilePoint.Y,
                        PixelX = f.Position.X,
                        PixelY = f.Position.Y,
                        FacingDirection = f.FacingDirection,
                        IsOnline = isOnline
                    });
                }

                // Crop counts by location
                if (_cropTrackerService != null)
                {
                    var cropsSnap = _cropTrackerService.GetLatestSnapshot();
                    foreach (var crop in cropsSnap.Crops)
                    {
                        string loc = crop.Location ?? "Farm";
                        if (!mapData.CropCountsByLocation.ContainsKey(loc))
                        {
                            mapData.CropCountsByLocation[loc] = 0;
                        }
                        mapData.CropCountsByLocation[loc]++;
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"[API] Error compiling map tracking: {ex.Message}", LogLevel.Error);
            }
        });

        await WriteJsonAsync(response, mapData);
    }

    private static string ClassifyZone(string location)
    {
        if (string.IsNullOrEmpty(location)) return "farm";
        var l = location.ToLowerInvariant();
        if (l == "farm" || l.Contains("cabin") || l.Contains("farmhouse") || l.Contains("greenhouse") || l.Contains("shed") || l.Contains("barn") || l.Contains("coop"))
            return "farm";
        if (l == "town" || l.Contains("saloon") || l.Contains("hospital") || l.Contains("seedshop") || l.Contains("archaeology") || l.Contains("manor") || l.Contains("trailer"))
            return "town";
        if (l == "beach" || l.Contains("fishshop"))
            return "beach";
        if (l == "mountain" || l.Contains("sciencehouse") || l.Contains("adventureguild") || l.Contains("bathhouse"))
            return "mountain";
        if (l == "forest" || l.Contains("animalshop") || l.Contains("wizardhouse") || l.Contains("cindersap"))
            return "forest";
        if (l.Contains("mine") || l.Contains("skullcave") || l.Contains("undergroundmine"))
            return "mines";
        if (l.Contains("desert") || l.Contains("club") || l.Contains("oasis"))
            return "desert";
        if (l.Contains("island"))
            return "island";
        return "other";
    }

    #endregion

    public async Task StopServerAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();

            if (_serverTask != null)
            {
                await Task.WhenAny(_serverTask, Task.Delay(5000));
            }

            _isRunning = false;
            Monitor.Log("API server stopped", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Error stopping API server: {ex}", LogLevel.Warn);
        }
    }
}
