using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace FoolMeGame.Shared.Telegram;

public class TelegramHelper
{
    private readonly ITelegramBotClient _telegram;
    private readonly ILogger<TelegramHelper> _logger;
    private Message? _message;
    private CallbackQuery? _callbackQuery;

    public TelegramHelper(ITelegramBotClient telegram, ILogger<TelegramHelper> logger)
    {
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Message Message => _message ?? new ();
    public CallbackQuery CallbackQuery => _callbackQuery ?? new ();
    public long UserId => _message?.From?.Id ?? _callbackQuery?.From.Id ?? 0;
    public string UserName => _message?.From?.Username ?? _callbackQuery?.From.Username ?? string.Empty;
    public long ChatId => _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id ?? 0;

    public bool SetContext(Update? update)
    {
        if (update == null) return false;

        _message = update.Message;
        _callbackQuery = update.CallbackQuery;

        if (_message == null && _callbackQuery == null) return false;
        return true;
    }

    public bool HasMessage() => _message != null;
    public bool HasCallback() => _callbackQuery != null;

    public async Task<Message?> SendMessageBackAsync(string text,
        bool asReply = false,
        ParseMode parseMode = ParseMode.Html,
        ReplyMarkup? replyMarkup = null,
        LinkPreviewOptions? linkPreviewOptions = null,
        IEnumerable<MessageEntity>? entities = null,
        bool disableNotification = false)
    {
        var chatId = _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id;
        try
        {
            if (chatId == null)
            {
                _logger.LogWarning("Cannot send message, chat id not found");
                return null;
            }

            var result = await _telegram.SendMessage(chatId,
                text,
                parseMode,
                replyParameters: asReply && _message != null ? new ReplyParameters { MessageId = _message.Id } : null,
                replyMarkup,
                linkPreviewOptions,
                messageThreadId: null,
                entities,
                disableNotification,
                protectContent: false,
                messageEffectId: null,
                businessConnectionId: null,
                allowPaidBroadcast: false);

            return result;
        }
        catch
        {
            _logger.LogError("Cannot send message: '{Message}'", text);
            _logger.LogError("Chat ID: {ChatId}", chatId);
            throw;
        }
    }

    public Task EditMessageButtons(long chatId, int messageId, InlineKeyboardMarkup? replyMarkup)
    {
        return _telegram.EditMessageReplyMarkup(chatId, messageId, replyMarkup);
    }

    public Task SendStatusAsync(ChatAction action, CancellationToken cancellationToken = default)
    {
        var chatId = _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id;
        if (chatId == null)
        {
            _logger.LogWarning("Cannot send status to chat, chat id not found");
            return Task.CompletedTask;
        }

        return _telegram.SendChatAction(chatId, action, cancellationToken: cancellationToken);
    }

    public Task SendCallbackResponseAsync(string? text = null, bool showAlert = false)
    {
        if (_callbackQuery == null)
        {
            _logger.LogWarning("Cannot send callback response without a callback");
            return Task.CompletedTask;
        }

        return _telegram.AnswerCallbackQuery(_callbackQuery.Id, text, showAlert);
    }

    public Task SendBackAsync(string text,
        bool asReply = false,
        ParseMode parseMode = ParseMode.Html,
        ReplyMarkup? replyMarkup = null,
        LinkPreviewOptions? linkPreviewOptions = null,
        IEnumerable<MessageEntity>? entities = null,
        bool disableNotification = false,
        bool showAlert = true)
    {
        return HasCallback()
            ? SendCallbackResponseAsync(text, showAlert)
            : SendMessageBackAsync(text, asReply, parseMode, replyMarkup, linkPreviewOptions, entities, disableNotification);
    }

    public Task EditMessageAsync(int messageId, string text,
        ParseMode parseMode = ParseMode.Html,
        InlineKeyboardMarkup? replyMarkup = null,
        LinkPreviewOptions? linkPreviewOptions = null,
        IEnumerable<MessageEntity>? entities = null) =>
        _telegram.EditMessageText(_message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id ?? 0, messageId, text, parseMode, entities, linkPreviewOptions, replyMarkup);

    public Task DeleteMessageAsync(int? messageId = null)
    {
        var chatId = _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id;
        if (chatId == null)
        {
            _logger.LogWarning("Cannot delete message without a message or callback");
            return Task.CompletedTask;
        }

        messageId ??= _message?.MessageId ?? _callbackQuery?.Message?.MessageId;
        if (messageId == null)
        {
            _logger.LogWarning("Cannot find message id to delete");
            return Task.CompletedTask;
        }

        return _telegram.DeleteMessage(chatId, messageId.Value);
    }

    public Task PinMessageAsync(int messageId, bool disableNotification = true)
    {
        var chatId = _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id;
        if (chatId == null)
        {
            _logger.LogWarning("Cannot delete message without a message or callback");
            return Task.CompletedTask;
        }

        return _telegram.PinChatMessage(chatId, messageId, disableNotification: disableNotification);
    }

    public Task UnpinMessageAsync(int messageId)
    {
        var chatId = _message?.Chat.Id ?? _callbackQuery?.Message?.Chat.Id;
        if (chatId == null)
        {
            _logger.LogWarning("Cannot unpin message without a message or callback");
            return Task.CompletedTask;
        }

        try
        {
            return _telegram.UnpinChatMessage(chatId, messageId);
        }
        catch
        {
            return Task.CompletedTask; // Ignore if message is not pinned
        }
    }
}
