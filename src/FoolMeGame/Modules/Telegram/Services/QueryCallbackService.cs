using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business;
namespace FoolMeGame.Modules.Telegram.Services;

public class QueryCallbackService
{
    private readonly TelegramHelper _telegram;
    private readonly ISettingsManager _settingsManager;
    private readonly LevelsProvider _levelsProvider;
    private readonly ILogger<QueryCallbackService> _logger;

    public QueryCallbackService(
        TelegramHelper telegram,
        ISettingsManager settingsManager,
        LevelsProvider levelsProvider,
        ILogger<QueryCallbackService> logger)
    {
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _levelsProvider = levelsProvider ?? throw new ArgumentNullException(nameof(levelsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IResult> HandleAsync()
    {
        var queryData = _telegram.CallbackQuery?.Data;
        if (string.IsNullOrWhiteSpace(queryData))
        {
            _logger.LogWarning("Cannot handle query callback without data");
            return Results.Ok();
        }

        var isChatRegistered = _settingsManager.TryGetSettings(_telegram.CallbackQuery?.Message?.Chat.Id ?? 0, out _);
        if (_telegram.CallbackQuery?.From?.Id != GameInfo.GlobalAdminUserId && !isChatRegistered)
        {
            await _telegram.SendCallbackResponseAsync($"You are not whitelisted to use this bot ({_telegram.CallbackQuery?.Message?.Chat.Id})", showAlert: true);
            return Results.Ok();
        }

        return
            await TryHandleCommandsAsync(queryData) ??
            await HandleUnknownAsync(queryData);
    }

    private async Task<IResult?> TryHandleCommandsAsync(string queryData)
    {
        if (!queryData.StartsWith('/')) return null;
        if (_telegram.CallbackQuery.Message == null) return null;

        var commandText = queryData.TrimStart('/').Split('_', ' ')[0];
        if (commandText.IsEmpty()) return null;

        if (!_levelsProvider.TryGet(_telegram.CallbackQuery.Message.Chat.Id, commandText, out var action, out var command))
        {
            await _telegram.SendCallbackResponseAsync($"Unknown command: {commandText}", showAlert: true);
            return Results.Ok();
        }

        await action.HandleAsync(new TelegramCommand(_telegram.CallbackQuery.Message, _telegram.CallbackQuery.From, null, command));
        _ = _telegram.SendCallbackResponseAsync(); // Acknowledge the callback query without any message

        return Results.Ok();
    }

    private async Task<IResult> HandleUnknownAsync(string queryData)
    {
        await _telegram.SendCallbackResponseAsync($"I don't understand you 😕 ({queryData})", showAlert: true);
        return Results.Ok();
    }
}
