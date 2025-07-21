using FoolMeGame.Modules.Game;
using FoolMeGame.Modules.Telegram.Models;
using FoolMeGame.Modules.Telegram.Services;
namespace FoolMeGame.Modules.Telegram.Handlers;

public class GameTextHandler : IMessageTextHandler
{
    private readonly GameplayService _gameplay;
    private readonly ChatSettingsService _settingsService;
    private readonly TelegramHelper _telegram;

    public GameTextHandler(
        GameplayService gameplay,
        ChatSettingsService settingsService,
        TelegramHelper telegram)
    {
        _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public async Task<bool> HandleAsync(TelegramText message)
    {
        if (!_settingsService.IsChatRegistered(message.ChatId))
        {
            // chat is unregistered, ignore the command
            return true;
        }

        if (!_gameplay.IsRunning(message.ChatId) || !_gameplay.IsUserTurn(message.ChatId, message.UserId))
            return false;

        await _gameplay.ProcessTextAsync(message.ChatId, message.UserId, message.Text, _telegram);
        return true;
    }
}
