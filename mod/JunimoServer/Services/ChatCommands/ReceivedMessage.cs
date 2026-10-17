using StardewValley;

namespace JunimoServer.Services.ChatCommands;

public class ReceivedMessage
{
    public enum ChatKinds
    {
        ChatMessage,
        ErrorMessage,
        UserNotification,
        PrivateMessage,
    }

    public long SourceFarmer { get; set; }
    public ChatKinds ChatKind { get; set; }
    public LocalizedContentManager.LanguageCode Language { get; set; }
    public string Message { get; set; }

    public bool IsCommand
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Message))
            {
                return false;
            }

            var trimmed = Message.TrimStart().Replace('\u00A0', ' ');
            return trimmed.StartsWith("!") && trimmed.Length > 1;
        }
    }

    public ChatCommandMessage Command
    {
        get
        {
            var trimmed = Message.TrimStart().Replace('\u00A0', ' ');
            // Remove prefix and command name with ranges
            var commandParts = ArgUtility.SplitBySpace(trimmed[1..]);
            var commandName = commandParts[0];
            var commandArgs = commandParts[1..];

            return new ChatCommandMessage(commandName, commandArgs);
        }
    }
}
