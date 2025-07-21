namespace FoolMeGame.Modules.Telegram;

public interface IMessageCommandHandler
{
    Task<bool> HandleAsync(TelegramCommand command);
}
