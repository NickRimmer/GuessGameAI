using Telegram.Bot.Types;
namespace FoolMeGame.Modules.Telegram.Models;

public record TelegramText: TelegramBaseMessage
{
    public TelegramText(Message message, User user) : base(message, user)
    {
        Text = message.Text ?? string.Empty;
    }

    public string Text { get; }
}
