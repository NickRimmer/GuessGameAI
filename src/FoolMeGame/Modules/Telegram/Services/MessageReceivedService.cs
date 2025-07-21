using System.Reflection;
using FoolMeGame.Modules.Telegram.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
namespace FoolMeGame.Modules.Telegram.Services;

public class MessageReceivedService
{
    private readonly IEnumerable<IMessageTextHandler> _textHandlers;
    private readonly ChatSettingsService _chatSettings;
    private readonly TelegramHelper _telegram;
    private readonly IReadOnlyCollection<IMessageCommandHandler> _commandHandlers;

    public MessageReceivedService(
        IEnumerable<IMessageCommandHandler> commandHandlers,
        IEnumerable<IMessageTextHandler> textHandlers,
        ChatSettingsService chatSettings,
        TelegramHelper telegram)
    {
        _textHandlers = textHandlers ?? throw new ArgumentNullException(nameof(textHandlers));
        _chatSettings = chatSettings ?? throw new ArgumentNullException(nameof(chatSettings));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _commandHandlers = commandHandlers?.ToList() ?? throw new ArgumentNullException(nameof(commandHandlers));
    }

    public async Task<IResult> HandleAsync()
    {
        var message = _telegram.Message;
        var isChatRegistered = _chatSettings.IsChatRegistered(message.Chat.Id);

        if (message.From?.Id != Constants.GlobalAdminUserId && !isChatRegistered)
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

        // load handlers
        var handlers = _commandHandlers
            .Select(x => new {
                Handler = x,
                Attribute = x.GetType().GetCustomAttribute<CommandNamesAttribute>(),
            })
            .ToList();

        // try handle commands
        var handled = false;
        foreach (var d in data)
        {
            var found = handlers
                .Select(x => new {
                    Command = x.Attribute?.Commands.FirstOrDefault(c => c.Equals(d.Command, StringComparison.OrdinalIgnoreCase)),
                    x.Handler,
                })
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Command));

            if (found == null) continue;
            handled = await found.Handler.HandleAsync(new TelegramCommand(message, message.From, d.Entity, found.Command!));
        }

        if (handled) return Results.Ok();

        // send message back about unknown command
        await _telegram.SendMessageBackAsync($"Unknown command: {data.Select(x => x.Command).Join(", ")}", asReply: true);
        return Results.Ok();
    }

    private async Task<IResult?> TryHandleTextAsync(Message message)
    {
        if (message.From == null || string.IsNullOrWhiteSpace(message.Text))
            return null;

        foreach (var handler in _textHandlers)
            if (await handler.HandleAsync(new TelegramText(message, message.From)))
                break;

        return Results.Ok();
    }

    private async Task<IResult> HandleUnknownAsync(Message message)
    {
        // await _telegram.SendMessageBackAsync("I don't understand you 😕", asReply: true);
        return Results.Ok();
    }
}
