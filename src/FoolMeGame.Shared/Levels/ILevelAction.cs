using FoolMeGame.Shared.Telegram;
namespace FoolMeGame.Shared.Levels;

public interface ILevelAction
{
    Task HandleAsync(TelegramCommand command);
}
