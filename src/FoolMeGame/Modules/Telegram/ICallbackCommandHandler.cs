namespace FoolMeGame.Modules.Telegram;

public interface ICallbackCommandHandler
{
    Task<bool> HandleAsync(TelegramCommand command);
}
