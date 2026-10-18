using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Services.Redis;
using JunimoServer.Util;
using StardewModdingAPI;
using StardewValley;

namespace JunimoServer.Services.Economy;

/// <summary>
/// Service managing the in-game banking and player-to-player economic transactions.
/// Provides personal bank accounts (!bank deposit, !bank withdraw, !balance) persisted via Redis,
/// and direct player transfers (!pay player amount) for separate wallets.
/// </summary>
public class BankService : ModService
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly IRedisService _redis;
    private readonly ChatCommandsService _chatCommandsService;

    private const string BankKeyPrefix = "bank:balance:";

    public BankService(
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
        _monitor.Log("[Economy] BankService initialized.", LogLevel.Info);
    }

    private void RegisterCommands()
    {
        _chatCommandsService.RegisterCommand(
            "bank",
            "Akses rekening bank pribadi (!bank, !bank deposit <jumlah>, !bank withdraw <jumlah>)",
            HandleBankCommand
        );

        _chatCommandsService.RegisterCommand(
            "balance",
            "Cek saldo rekening bank pribadi dan saldo dompet saat ini",
            (args, msg) => HandleBalance(msg.SourceFarmer)
        );

        _chatCommandsService.RegisterCommand(
            "deposit",
            "Menyetor uang ke rekening bank pribadi (!deposit <jumlah>)",
            (args, msg) =>
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int amt))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "Penggunaan: !deposit <jumlah>");
                    return;
                }
                _ = DepositAsync(msg.SourceFarmer, amt);
            }
        );

        _chatCommandsService.RegisterCommand(
            "withdraw",
            "Menarik uang dari rekening bank pribadi (!withdraw <jumlah>)",
            (args, msg) =>
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int amt))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "Penggunaan: !withdraw <jumlah>");
                    return;
                }
                _ = WithdrawAsync(msg.SourceFarmer, amt);
            }
        );

        _chatCommandsService.RegisterCommand(
            "pay",
            "Mentransfer uang ke pemain lain (!pay <nama_pemain> <jumlah>)",
            HandlePayCommand
        );

        _chatCommandsService.RegisterCommand(
            "kas",
            "Akses rekening Kas Pembangunan Desa (!kas, !kas setor <jumlah>, !kas target <nama> <biaya>)",
            HandleVillageTreasuryCommand
        );
    }

    private void HandleBankCommand(string[] args, ReceivedMessage msg)
    {
        if (args.Length == 0)
        {
            HandleBalance(msg.SourceFarmer);
            return;
        }

        string action = args[0].ToLowerInvariant();
        switch (action)
        {
            case "balance":
            case "saldo":
            case "check":
                HandleBalance(msg.SourceFarmer);
                break;

            case "deposit":
            case "setor":
                if (args.Length < 2 || !int.TryParse(args[1], out int depositAmount))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "Penggunaan: !bank deposit <jumlah>");
                    return;
                }
                _ = DepositAsync(msg.SourceFarmer, depositAmount);
                break;

            case "withdraw":
            case "tarik":
                if (args.Length < 2 || !int.TryParse(args[1], out int withdrawAmount))
                {
                    _helper.SendPrivateMessage(msg.SourceFarmer, "Penggunaan: !bank withdraw <jumlah>");
                    return;
                }
                _ = WithdrawAsync(msg.SourceFarmer, withdrawAmount);
                break;

            default:
                _helper.SendPrivateMessage(
                    msg.SourceFarmer,
                    "Perintah bank tidak dikenal. Gunakan: !bank [balance | deposit <jumlah> | withdraw <jumlah>]"
                );
                break;
        }
    }

    public async Task<int> GetBankBalanceAsync(long farmerId)
    {
        string key = BankKeyPrefix + farmerId;
        string? val = await _redis.GetStringAsync(key);
        if (int.TryParse(val, out int balance))
        {
            return balance;
        }
        return 0;
    }

    private void HandleBalance(long farmerId)
    {
        var farmer = Game1.GetPlayer(farmerId);
        if (farmer == null)
        {
            _helper.SendPrivateMessage(farmerId, "Profil karakter tidak ditemukan.");
            return;
        }

        Task.Run(async () =>
        {
            int bankBalance = await GetBankBalanceAsync(farmerId);
            int walletBalance = farmer.Money;
            bool isSeparate = Game1.player.team.useSeparateWallets.Value;

            string walletLabel = isSeparate ? "Dompet Pribadi" : "Kas Bersama Ladang";
            _helper.SendPrivateMessage(
                farmerId,
                $"[Bank] Saldo Rekening: {bankBalance:N0}g | {walletLabel}: {walletBalance:N0}g"
            );
        });
    }

    private async Task DepositAsync(long farmerId, int amount)
    {
        if (amount <= 0)
        {
            _helper.SendPrivateMessage(farmerId, "[Bank] Jumlah setoran harus lebih besar dari 0.");
            return;
        }

        var farmer = Game1.GetPlayer(farmerId);
        if (farmer == null)
        {
            _helper.SendPrivateMessage(farmerId, "[Bank] Karakter tidak ditemukan.");
            return;
        }

        bool isSeparate = Game1.player.team.useSeparateWallets.Value;
        int currentWallet = isSeparate ? farmer.Money : Game1.player.team.money.Value;

        if (currentWallet < amount)
        {
            _helper.SendPrivateMessage(
                farmerId,
                $"[Bank] Saldo tidak mencukupi. Anda hanya memiliki {currentWallet:N0}g di dompet."
            );
            return;
        }

        // Deduct from wallet
        if (isSeparate)
        {
            farmer.Money -= amount;
        }
        else
        {
            Game1.player.team.money.Value -= amount;
        }

        // Add to Redis bank
        string key = BankKeyPrefix + farmerId;
        int oldBalance = await GetBankBalanceAsync(farmerId);
        int newBalance = oldBalance + amount;
        await _redis.SetStringAsync(key, newBalance.ToString());

        _monitor.Log($"[Economy] {farmer.Name} ({farmerId}) deposited {amount}g. New bank balance: {newBalance}g", LogLevel.Info);
        _helper.SendPrivateMessage(
            farmerId,
            $"[Bank] Setoran berhasil sebesar {amount:N0}g! Saldo rekening Anda sekarang: {newBalance:N0}g."
        );
    }

    private async Task WithdrawAsync(long farmerId, int amount)
    {
        if (amount <= 0)
        {
            _helper.SendPrivateMessage(farmerId, "[Bank] Jumlah penarikan harus lebih besar dari 0.");
            return;
        }

        var farmer = Game1.GetPlayer(farmerId);
        if (farmer == null)
        {
            _helper.SendPrivateMessage(farmerId, "[Bank] Karakter tidak ditemukan.");
            return;
        }

        string key = BankKeyPrefix + farmerId;
        int oldBalance = await GetBankBalanceAsync(farmerId);

        if (oldBalance < amount)
        {
            _helper.SendPrivateMessage(
                farmerId,
                $"[Bank] Saldo rekening tidak mencukupi. Saldo rekening Anda saat ini: {oldBalance:N0}g."
            );
            return;
        }

        int newBalance = oldBalance - amount;
        await _redis.SetStringAsync(key, newBalance.ToString());

        // Add to wallet
        bool isSeparate = Game1.player.team.useSeparateWallets.Value;
        if (isSeparate)
        {
            farmer.Money += amount;
        }
        else
        {
            Game1.player.team.money.Value += amount;
        }

        _monitor.Log($"[Economy] {farmer.Name} ({farmerId}) withdrew {amount}g. New bank balance: {newBalance}g", LogLevel.Info);
        _helper.SendPrivateMessage(
            farmerId,
            $"[Bank] Penarikan berhasil sebesar {amount:N0}g! Saldo rekening tersisa: {newBalance:N0}g."
        );
    }

    private void HandlePayCommand(string[] args, ReceivedMessage msg)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int amount))
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "Penggunaan: !pay <nama_pemain> <jumlah>");
            return;
        }

        if (amount <= 0)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Bank] Jumlah transfer harus lebih besar dari 0.");
            return;
        }

        var senderFarmer = Game1.GetPlayer(msg.SourceFarmer);
        if (senderFarmer == null)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Bank] Karakter pengirim tidak ditemukan.");
            return;
        }

        bool isSeparate = Game1.player.team.useSeparateWallets.Value;
        if (!isSeparate)
        {
            _helper.SendPrivateMessage(
                msg.SourceFarmer,
                "[Bank] Server menggunakan sistem Kas Bersama (Shared Wallet). Seluruh pemain sudah menggunakan uang yang sama."
            );
            return;
        }

        if (senderFarmer.Money < amount)
        {
            _helper.SendPrivateMessage(
                msg.SourceFarmer,
                $"[Bank] Saldo dompet Anda tidak cukup untuk transfer. Anda memiliki {senderFarmer.Money:N0}g."
            );
            return;
        }

        string targetName = args[0];
        var targetFarmer = _helper.FindPlayerIdByFarmerNameOrUserName(targetName);

        if (targetFarmer == null)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, $"[Bank] Pemain '{targetName}' tidak ditemukan atau sedang offline.");
            return;
        }

        if (targetFarmer.UniqueMultiplayerID == msg.SourceFarmer)
        {
            _helper.SendPrivateMessage(msg.SourceFarmer, "[Bank] Anda tidak dapat mentransfer uang ke diri sendiri.");
            return;
        }

        // Execute transfer
        senderFarmer.Money -= amount;
        targetFarmer.Money += amount;

        _monitor.Log($"[Economy] Transfer {amount}g from {senderFarmer.Name} to {targetFarmer.Name}", LogLevel.Info);
        _helper.SendPrivateMessage(
            msg.SourceFarmer,
            $"[Bank] Berhasil mentransfer {amount:N0}g kepada {targetFarmer.Name}. Sisa dompet: {senderFarmer.Money:N0}g."
        );
        _helper.SendPrivateMessage(
            targetFarmer.UniqueMultiplayerID,
            $"[Bank] Anda menerima transfer dana {amount:N0}g dari {senderFarmer.Name}!"
        );
    }

    private const string VillageTreasuryKey = "economy:village_treasury";
    private const string VillageTargetKey = "economy:village_target";
    private const string VillageTargetCostKey = "economy:village_target_cost";

    public async Task<int> GetVillageTreasuryAsync()
    {
        string? val = await _redis.GetStringAsync(VillageTreasuryKey);
        return int.TryParse(val, out int amount) ? amount : 0;
    }

    public async Task<bool> SetVillageTreasuryAsync(int amount)
    {
        return await _redis.SetStringAsync(VillageTreasuryKey, amount.ToString());
    }

    public async Task<(string targetName, int targetCost)> GetVillageTargetAsync()
    {
        string? name = await _redis.GetStringAsync(VillageTargetKey);
        string? costStr = await _redis.GetStringAsync(VillageTargetCostKey);
        int cost = int.TryParse(costStr, out int c) ? c : 10000;
        return (string.IsNullOrEmpty(name) ? "Pembangunan Silo / Fasilitas Desa" : name, cost);
    }

    public async Task SetVillageTargetAsync(string targetName, int targetCost)
    {
        await _redis.SetStringAsync(VillageTargetKey, targetName);
        await _redis.SetStringAsync(VillageTargetCostKey, targetCost.ToString());
    }

    private void HandleVillageTreasuryCommand(string[] args, ReceivedMessage msg)
    {
        long farmerId = msg.SourceFarmer;
        var farmer = Game1.GetPlayer(farmerId);
        if (farmer == null)
        {
            _helper.SendPrivateMessage(farmerId, "[Kas Desa] Profil karakter tidak ditemukan.");
            return;
        }

        if (args.Length == 0 || args[0].Equals("balance", StringComparison.OrdinalIgnoreCase) || args[0].Equals("saldo", StringComparison.OrdinalIgnoreCase))
        {
            Task.Run(async () =>
            {
                int treasury = await GetVillageTreasuryAsync();
                var (targetName, targetCost) = await GetVillageTargetAsync();
                _helper.SendPrivateMessage(farmerId, $"[Kas Desa] Saldo Kas Pembangunan: {treasury:N0}g");
                _helper.SendPrivateMessage(farmerId, $"[Kas Desa] Target Proyek: {targetName} ({treasury:N0} / {targetCost:N0}g)");
                _helper.SendPrivateMessage(farmerId, "[Kas Desa] Perintah: !kas setor <jumlah> | !kas target <nama> <biaya>");
            });
            return;
        }

        string action = args[0].ToLowerInvariant();
        if (action == "setor" || action == "deposit")
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int amount) || amount <= 0)
            {
                _helper.SendPrivateMessage(farmerId, "Penggunaan: !kas setor <jumlah>");
                return;
            }

            bool isSeparate = Game1.player.team.useSeparateWallets.Value;
            int currentWallet = isSeparate ? farmer.Money : Game1.player.team.money.Value;

            if (currentWallet < amount)
            {
                _helper.SendPrivateMessage(farmerId, $"[Kas Desa] Uang di dompet Anda tidak cukup ({currentWallet:N0}g).");
                return;
            }

            if (isSeparate)
            {
                farmer.Money -= amount;
            }
            else
            {
                Game1.player.team.money.Value -= amount;
            }

            Task.Run(async () =>
            {
                int oldTreasury = await GetVillageTreasuryAsync();
                int newTreasury = oldTreasury + amount;
                await SetVillageTreasuryAsync(newTreasury);

                var (targetName, targetCost) = await GetVillageTargetAsync();

                _monitor.Log($"[Economy] {farmer.Name} contributed {amount}g to village treasury. Total: {newTreasury}g", LogLevel.Info);

                foreach (var f in Game1.getAllFarmers())
                {
                    if (f != null)
                    {
                        _helper.SendPrivateMessage(
                            f.UniqueMultiplayerID,
                            $"[Kas Desa] {farmer.Name} menyetor {amount:N0}g ke Kas Desa! Total: {newTreasury:N0} / {targetCost:N0}g ({targetName})."
                        );
                    }
                }
            });
            return;
        }

        if (action == "target")
        {
            if (args.Length < 3 || !int.TryParse(args[2], out int targetCost) || targetCost <= 0)
            {
                _helper.SendPrivateMessage(farmerId, "Penggunaan: !kas target <nama_proyek> <biaya>");
                return;
            }

            string targetName = args[1];
            Task.Run(async () =>
            {
                await SetVillageTargetAsync(targetName, targetCost);
                int treasury = await GetVillageTreasuryAsync();
                foreach (var f in Game1.getAllFarmers())
                {
                    if (f != null)
                    {
                        _helper.SendPrivateMessage(
                            f.UniqueMultiplayerID,
                            $"[Kas Desa] Target pembangunan diperbarui menjadi: {targetName} ({treasury:N0} / {targetCost:N0}g) oleh {farmer.Name}."
                        );
                    }
                }
            });
            return;
        }

        if (action == "tarik" || action == "withdraw")
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int amount) || amount <= 0)
            {
                _helper.SendPrivateMessage(farmerId, "Penggunaan: !kas tarik <jumlah>");
                return;
            }

            Task.Run(async () =>
            {
                int treasury = await GetVillageTreasuryAsync();
                if (treasury < amount)
                {
                    _helper.SendPrivateMessage(farmerId, $"[Kas Desa] Saldo kas tidak mencukupi ({treasury:N0}g).");
                    return;
                }

                int newTreasury = treasury - amount;
                await SetVillageTreasuryAsync(newTreasury);

                bool isSeparate = Game1.player.team.useSeparateWallets.Value;
                if (isSeparate)
                {
                    farmer.Money += amount;
                }
                else
                {
                    Game1.player.team.money.Value += amount;
                }

                _monitor.Log($"[Economy] {farmer.Name} withdrew {amount}g from village treasury. Remaining: {newTreasury}g", LogLevel.Info);
                _helper.SendPrivateMessage(farmerId, $"[Kas Desa] Penarikan {amount:N0}g berhasil. Sisa kas: {newTreasury:N0}g.");
            });
            return;
        }

        _helper.SendPrivateMessage(farmerId, "Perintah tidak dikenal. Gunakan: !kas [saldo | setor <jumlah> | target <nama> <biaya> | tarik <jumlah>]");
    }

    public async Task<object> GetEconomyOverviewAsync()
    {
        bool isSeparate = Game1.hasLoadedGame && Game1.player.team.useSeparateWallets.Value;
        int sharedMoney = Game1.hasLoadedGame ? Game1.player.team.money.Value : 0;

        int villageTreasury = await GetVillageTreasuryAsync();
        var (targetName, targetCost) = await GetVillageTargetAsync();

        var accounts = new List<object>();

        if (Game1.hasLoadedGame)
        {
            var allFarmers = Game1.getAllFarmers();
            foreach (var farmer in allFarmers)
            {
                if (farmer == null || farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
                {
                    continue;
                }

                int bankBal = await GetBankBalanceAsync(farmer.UniqueMultiplayerID);
                int walletBal = isSeparate ? farmer.Money : sharedMoney;
                bool isOnline = Game1.otherFarmers.ContainsKey(farmer.UniqueMultiplayerID);

                accounts.Add(new
                {
                    farmerId = farmer.UniqueMultiplayerID,
                    farmerName = farmer.Name,
                    bankBalance = bankBal,
                    walletBalance = walletBal,
                    totalWealth = bankBal + (isSeparate ? walletBal : 0),
                    isOnline = isOnline
                });
            }
        }

        return new
        {
            useSeparateWallets = isSeparate,
            sharedFarmMoney = sharedMoney,
            villageTreasury = villageTreasury,
            villageTarget = new
            {
                name = targetName,
                cost = targetCost,
                progress = targetCost > 0 ? Math.Min(100.0, Math.Round((double)villageTreasury / targetCost * 100.0, 1)) : 0
            },
            accounts = accounts
        };
    }
}
