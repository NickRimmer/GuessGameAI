using Telegram.Bot.Types;
namespace FoolMeGame.Modules.Telegram.Models;

public abstract record TelegramBaseMessage
{
    protected TelegramBaseMessage(Message message, User user)
    {
        UserName = user.Username ?? user.Id.ToString();
        UserId = user.Id;
        ChatId = message.Chat.Id;
    }

    public string UserName { get; }

    public long UserId { get; }

    public long ChatId { get; }
}
