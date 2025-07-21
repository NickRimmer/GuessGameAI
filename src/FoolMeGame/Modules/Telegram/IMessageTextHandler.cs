using FoolMeGame.Modules.Telegram.Models;
namespace FoolMeGame.Modules.Telegram;

public interface IMessageTextHandler
{
    Task<bool> HandleAsync(TelegramText message);
}
