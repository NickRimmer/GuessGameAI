using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
namespace FoolMeGame.Modules.Telegram.Services;

public class MessageReceivedService
{
    private readonly LevelsProvider _levelsProvider;
    private readonly ISettingsManager _settingsManager;
    private readonly TelegramHelper _telegram;

    public MessageReceivedService(
        LevelsProvider levelsProvider,
        ISettingsManager settingsManager,
        TelegramHelper telegram)
    {
        _levelsProvider = levelsProvider ?? throw new ArgumentNullException(nameof(levelsProvider));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public async Task<IResult> HandleAsync()
    {
        var message = _telegram.Message;
        var isChatRegistered = _settingsManager.TryGetSettings(message.Chat.Id, out _);

        if (message.From?.Id != GameInfo.GlobalAdminUserId && !isChatRegistered)
        {
            await _telegram.SendMessageBackAsync($"You are not whitelisted to use this bot ({message.Chat.Id})", asReply: true);
            return Results.Ok();
        }

        return
            await TryHandleCommandsAsync(message) ??
            await TryHandleTextAsync(message) ??
            await HandleUnknownAsync(message);
    }

    private async Task<IResult?> TryHandleCommandsAsync(Message message)
    {
        if (message?.From == null) return null;

        // extract all commands from the message
        var data = message.Entities?
            .Where(e => e.Type == MessageEntityType.BotCommand)
            .Select(e => new {
                Entity = e,
                Command = message.Text?.Substring(e.Offset, e.Length).TrimStart('/'),
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Command))
            .Select(x => new {
                x.Entity,
                Command = x.Command?.Split(['_', '@', ' '])[0],
            })
            .ToList() ?? [];
        if (data.Count == 0) return null;

        // try handle commands
        foreach (var d in data)
        {
            if (string.IsNullOrWhiteSpace(d.Command) || !_levelsProvider.TryGet(message.Chat.Id, d.Command, out var action, out var command)) continue;
            await action.HandleAsync(new TelegramCommand(message, message.From, d.Entity, command));
            return Results.Ok();
        }

        // send message back about unknown command
        await _telegram.SendMessageBackAsync($"Unknown command: {data.Select(x => x.Command).Join(", ")}", asReply: true);
        return Results.Ok();
    }

    private async Task<IResult?> TryHandleTextAsync(Message message)
    {
        if (message.From == null || string.IsNullOrWhiteSpace(message.Text))
            return null;

        if (_levelsProvider.TryGet(message.Chat.Id, LevelActionAttribute.TextCommand, out var action, out var command))
            await action.HandleAsync(new TelegramCommand(message, message.From, null, command));

        return Results.Ok();
    }

    private async Task<IResult> HandleUnknownAsync(Message message)
    {
        if (message.Type == MessageType.PinnedMessage)
        {
            // delete message about pinned message
            await _telegram.DeleteMessageAsync(message.Id);
        }

        return Results.Ok();
    }
}
