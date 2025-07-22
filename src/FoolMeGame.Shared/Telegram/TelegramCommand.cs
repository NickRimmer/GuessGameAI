using Telegram.Bot.Types;
namespace FoolMeGame.Shared.Telegram;

public record TelegramCommand
{
    public TelegramCommand(Message message, User from, MessageEntity? telegramEntity, string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(commandName));
        Name = commandName;

        Body = message.Text ?? string.Empty;
        Parameters = ExtractArguments(message, telegramEntity);
        ChatId = message.Chat.Id;
        UserId = from.Id;
        UserName = from.Username ?? from.Id.ToString();
    }

    public string UserName { get; }

    public long UserId { get; }

    public long ChatId { get; }

    public string Name { get; }

    public string Body { get; }
    public IReadOnlyCollection<string> Parameters { get; }

    private IReadOnlyCollection<string> ExtractArguments(Message message, MessageEntity? telegramEntity)
    {
        var parts = message.Text?.Substring(telegramEntity?.Offset ?? 0).Split('_', ' ');
        if (parts is not { Length: > 1 }) return [];

        return parts[1..].Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
};
