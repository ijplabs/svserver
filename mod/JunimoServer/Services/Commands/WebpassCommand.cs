using System;
using JunimoServer.Services.Auth;
using JunimoServer.Services.ChatCommands;
using JunimoServer.Util;
using StardewModdingAPI;

namespace JunimoServer.Services.Commands;

public class WebpassCommand
{
    private static IModHelper _helper;
    private static IMonitor _monitor;
    private static AccountLinkingService _accountLinking;

    public static void Register(
        IModHelper helper,
        IMonitor monitor,
        ChatCommandsService chatCommandsService,
        AccountLinkingService accountLinking
    )
    {
        _helper = helper;
        _monitor = monitor;
        _accountLinking = accountLinking;

        chatCommandsService.RegisterCommand(
            "webpass",
            "Generates a 6-digit registration code to link your account to the Web Dashboard.",
            async (args, msg) =>
            {
                var farmerId = msg.SourceFarmer;
                var farmerName = _helper.GetFarmerNameById(farmerId);
                if (string.IsNullOrWhiteSpace(farmerName))
                {
                    _helper.SendPrivateMessage(farmerId, "Error: Could not identify your farmer profile.");
                    return;
                }

                try
                {
                    var otp = await _accountLinking.GenerateOtpAsync(farmerId, farmerName);
                    _helper.SendPrivateMessage(farmerId, $"[WebAuth] Your registration OTP is: {otp}");
                    _helper.SendPrivateMessage(farmerId, "Code is valid for 10 minutes. Enter this code on the Web Dashboard to register.");
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[WebAuth] Error generating OTP for {farmerName}: {ex.Message}", LogLevel.Error);
                    _helper.SendPrivateMessage(farmerId, "Failed to generate registration code. Please try again later.");
                }
            }
        );
    }
}
