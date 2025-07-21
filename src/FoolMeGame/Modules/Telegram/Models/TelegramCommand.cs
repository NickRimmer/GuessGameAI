using Telegram.Bot.Types;
namespace FoolMeGame.Modules.Telegram.Models;

public record TelegramCommand: TelegramBaseMessage
{
    public TelegramCommand(Message message, User from, MessageEntity telegramEntity, string commandName): base(message, from)
    {
        if (string.IsNullOrWhiteSpace(commandName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(commandName));
        Name = commandName;

        Body = message.Text ?? string.Empty;
        Parameters = ExtractArguments(message, telegramEntity);
    }

    public string Name { get; }

    public string Body { get; }
    public IReadOnlyCollection<string> Parameters { get; }

    private IReadOnlyCollection<string> ExtractArguments(Message message, MessageEntity telegramEntity)
    {
        var parts = message.Text?.Substring(telegramEntity.Offset).Split('_', ' ');
        if (parts is not { Length: > 1 }) return [];

        return parts[1..].Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
};
