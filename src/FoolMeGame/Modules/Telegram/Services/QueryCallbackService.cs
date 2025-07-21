using System.Reflection;
namespace FoolMeGame.Modules.Telegram.Services;

public class QueryCallbackService
{
    private readonly TelegramHelper _telegram;
    private readonly ChatSettingsService _chatSettings;
    private readonly ILogger<QueryCallbackService> _logger;
    private readonly IEnumerable<ICallbackCommandHandler> _commandHandlers;

    public QueryCallbackService(
        TelegramHelper telegram,
        ChatSettingsService chatSettings,
        ILogger<QueryCallbackService> logger,
        IEnumerable<ICallbackCommandHandler> commandHandlers)
    {
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _chatSettings = chatSettings ?? throw new ArgumentNullException(nameof(chatSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commandHandlers = commandHandlers ?? throw new ArgumentNullException(nameof(commandHandlers));
    }

    public async Task<IResult> HandleAsync()
    {
        var queryData = _telegram.CallbackQuery?.Data;
        if (string.IsNullOrWhiteSpace(queryData))
        {
            _logger.LogWarning("Cannot handle query callback without data");
            return Results.Ok();
        }

        var isChatRegistered = _chatSettings.IsChatRegistered(_telegram.CallbackQuery?.Message?.Chat.Id ?? 0);
        if (_telegram.CallbackQuery?.From?.Id != Constants.GlobalAdminUserId && !isChatRegistered)
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
        if (_telegram.CallbackQuery.Message?.Entities is not { Length: > 0 }) return null;

        var command = queryData.TrimStart('/').Split('_', ' ')[0];
        if (command.IsEmpty()) return null;

        // load handlers
        var handlers = _commandHandlers
            .Select(x => new {
                Handler = x,
                Attribute = x.GetType().GetCustomAttribute<CommandNamesAttribute>(),
            })
            .ToList();

        // try handle commands
        var found = handlers
            .Select(x => new {
                Command = x.Attribute?.Commands.FirstOrDefault(c => c.Equals(command, StringComparison.OrdinalIgnoreCase)) ?? string.Empty,
                x.Handler,
            })
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Command));

        if (found != null && await found.Handler.HandleAsync(new TelegramCommand(_telegram.CallbackQuery.Message, _telegram.CallbackQuery.From, _telegram.CallbackQuery.Message.Entities.First(), found.Command)))
            return Results.Ok();

        // send message back about unknown command
        await _telegram.SendCallbackResponseAsync($"Unknown command: {command}", showAlert: true);
        return Results.Ok();
    }

    private async Task<IResult> HandleUnknownAsync(string queryData)
    {
        await _telegram.SendCallbackResponseAsync("I don't understand you 😕", showAlert: true);
        return Results.Ok();
    }
}
