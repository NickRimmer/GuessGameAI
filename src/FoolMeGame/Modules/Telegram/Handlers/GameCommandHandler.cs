using FoolMeGame.Modules.Game;
using FoolMeGame.Modules.Telegram.Services;
using JetBrains.Annotations;
using Telegram.Bot.Types.ReplyMarkups;
namespace FoolMeGame.Modules.Telegram.Handlers;

[UsedImplicitly]
[CommandNames(CreateCommandName, CreateNewCommandName, CancelCommandName, JoinCommandName, RunCommandName, NotifyWordCommandName, MeCommandName)]
public class GameCommandHandler : IMessageCommandHandler, ICallbackCommandHandler
{
    private const string CreateCommandName = "create";
    private const string CreateNewCommandName = "new";
    private const string CancelCommandName = "cancel";
    private const string JoinCommandName = "join";
    private const string RunCommandName = "run";
    private const string MeCommandName = "me";
    private const string NotifyWordCommandName = "word";

    private readonly RoomGameService _rooms;
    private readonly GameplayService _gameplay;
    private readonly ChatSettingsService _settingsService;
    private readonly ILogger<GameCommandHandler> _logger;
    private readonly TelegramHelper _telegram;

    public GameCommandHandler(
        RoomGameService rooms,
        GameplayService gameplay,
        ChatSettingsService settingsService,
        ILogger<GameCommandHandler> logger,
        TelegramHelper telegram)
    {
        _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public async Task<bool> HandleAsync(TelegramCommand command)
    {
        if (!_settingsService.IsChatRegistered(command.ChatId))
        {
            // chat is unregistered, ignore the command
            return true;
        }

        var task = command.Name switch {
            CreateCommandName => CreateRoomAsync(command),
            CreateNewCommandName => CreateRoomAsync(command),
            CancelCommandName => CancelRoomAsync(command),
            JoinCommandName => JoinRoomAsync(command),
            RunCommandName => RunRoomAsync(command),
            NotifyWordCommandName => NotifyWordAsync(command),
            MeCommandName => ShowMeAsync(command),
            _ => Task.FromResult(false),
        };

        await task;
        return true;
    }

    private async Task<bool> CreateRoomAsync(TelegramCommand command)
    {
        if (_telegram.HasCallback())
        {
            _ = _telegram.SendCallbackResponseAsync();
            _ = _telegram.EditMessageButtons(command.ChatId, _telegram.CallbackQuery.Message!.Id, null);
        }

        if (!_rooms.Open(command.ChatId, command.UserId, command.UserName))
        {
            await _telegram.SendMessageBackAsync("This chat already has a game room. Please, use /join command to join the existing room.", asReply: true);
            return true;
        }

        await RefreshOnboardingAsync(command.ChatId);
        return true;
    }

    private async Task<bool> CancelRoomAsync(TelegramCommand command)
    {
        if (_rooms.TryClose(command.ChatId, out var room))
        {
            if (_telegram.HasCallback())
            {
                await _telegram.SendCallbackResponseAsync("⛔ The game room has been cancelled successfully.");
            }
            else
            {
                await _telegram.SendMessageBackAsync("⛔ The game room has been cancelled successfully.", asReply: true);
            }

            if (room.JoinMessageId.HasValue)
                try
                {
                    await _telegram.EditMessageAsync(room.JoinMessageId.Value, room.Id, "The game room has been cancelled.");
                }
                catch
                {
                    // it's ok...
                }

            return true;
        }

        _logger.LogWarning("Cannot cancel room for chat {ChatId}. Room not found", command.ChatId);
        if (_telegram.HasCallback())
        {
            await _telegram.SendCallbackResponseAsync("Cannot cancel the game room. Room not found.", showAlert: true);
        }
        else
        {
            await _telegram.SendMessageBackAsync("Cannot cancel the game room. Room not found.", asReply: true);
        }
        return true;
    }

    private async Task<bool> JoinRoomAsync(TelegramCommand command)
    {
        if (!_rooms.TryGetRoomForChat(command.ChatId, out var room))
        {
            _logger.LogWarning("Cannot join room for chat {ChatId}. Room not found", command.ChatId);
            return true;
        }

        if (room.IsRunning)
        {
            _logger.LogWarning("Cannot join room for chat {ChatId}. Room is already running", command.ChatId);
            if (_telegram.HasCallback())
            {
                await _telegram.SendCallbackResponseAsync("Cannot join the game. Room is already running.", showAlert: true);
            }
            else
            {
                await _telegram.SendMessageBackAsync("Cannot join the game. Room is already running.", asReply: true);
            }
            return true;
        }

        if (room.Players.Any(x => x.UserId == command.UserId))
        {
            if (_telegram.HasCallback()) await _telegram.SendCallbackResponseAsync("You are already in the game room.", showAlert: true);
            else await _telegram.SendMessageBackAsync("You are already in the game room.", asReply: true);
            return true;
        }

        if (!_rooms.TryJoin(command.ChatId, command.UserId, command.UserName))
        {
            _logger.LogWarning("Cannot join room for chat {ChatId}. Room is full or not found", command.ChatId);
            return true;
        }

        await RefreshOnboardingAsync(command.ChatId);

        if (_telegram.HasCallback())
        {
            await _telegram.SendCallbackResponseAsync("You have joined the game room successfully.");
        }
        else
        {
            await _telegram.SendMessageBackAsync("You have joined the game room successfully.", asReply: true);
        }

        return true;
    }

    private async Task<bool> RunRoomAsync(TelegramCommand command)
    {
        if (!_rooms.TryGetRoomForChat(command.ChatId, out var room))
        {
            _logger.LogWarning("Cannot join room for chat {ChatId}. Room not found", command.ChatId);
            return true;
        }

        if (room.Players.All(x => x.UserId != command.UserId) &&
            !_rooms.TryJoin(command.ChatId, command.UserId, command.UserName))
        {
            await _telegram.SendMessageBackAsync("You are not in the game room. Please, use /join command to join the existing room.", asReply: true);
            return true;
        }

        if (await _gameplay.TryRunAsync(command.ChatId, _telegram)) return true;

        _logger.LogWarning("Cannot run room for chat {ChatId}. Room not found or already running", command.ChatId);
        if (_telegram.HasCallback())
        {
            await _telegram.SendCallbackResponseAsync("Cannot run the game. Room not found or already running.", showAlert: true);
        }
        else
        {
            await _telegram.SendMessageBackAsync("Cannot run the game. Room not found or already running.", asReply: true);
        }
        return true;
    }

    private async Task<bool> NotifyWordAsync(TelegramCommand command)
    {
        if (!_rooms.TryGetRoomForChat(command.ChatId, out var room) || !room.IsRunning)
        {
            await _telegram.SendMessageBackAsync("Game is not started yet.", asReply: true);
            return true;
        }

        await _telegram.SendMessageBackAsync(
            $"ℹ️ Bot trying to guess the word: <b>{room.TheWord}</b>\n",
            asReply: true);

        return true;
    }

    private async Task<bool> ShowMeAsync(TelegramCommand command)
    {
        var details = _gameplay.GetMe(command.UserId);

        await _telegram.SendMessageBackAsync(
            new[] {
                $"ℹ️ @{command.UserName} stats:",
                string.Empty,
                $"- score: <b>{details.Score}</b>",
                $"- total games played: <b>{details.PlayedGames}</b>",
            }.Join("\n"));

        return true;
    }

    private async Task RefreshOnboardingAsync(long chatId)
    {
        if (!_rooms.TryGetRoomForChat(chatId, out var room))
        {
            _logger.LogWarning("Cannot refresh onboarding for chat {ChatId}. Room details not found", chatId);
            return;
        }

        var chatSettings = _settingsService.GetSettings(chatId);
        var buttons = new InlineKeyboardMarkup([
            new[] {
                room.Players.Count >= chatSettings.MinPlayersToStart ? new InlineKeyboardButton($"Run: {room.Players.Count} players") { CallbackData = "/run" } : null,
                room.Players.Count < chatSettings.MaxPlayersToPlay ? new InlineKeyboardButton("Join") { CallbackData = "/join" } : null,
                new InlineKeyboardButton("Cancel") { CallbackData = "/cancel" },
            }.OfType<InlineKeyboardButton>(),
        ]);

        if (!room.JoinMessageId.HasValue)
        {
            // add onboarding message
            var text = new[] {
                    "New game room created!",
                    "You can join the game using the command: /join or click 'Join'",
                    string.Empty,
                }
                .Concat(room.Players.Select(x => $"- {x.UserName}"))
                .Join("\n");

            var sentMessage = await _telegram.SendMessageBackAsync(text, replyMarkup: buttons);
            if (!_rooms.SetJoinMessage(chatId, sentMessage!.MessageId))
                _logger.LogWarning("Failed to set join message for chat {ChatId}. Room not found", chatId);
        }
        else
        {
            // update existing onboarding message
            await _telegram.EditMessageButtons(room.Id, room.JoinMessageId.Value, buttons);
        }
    }
}
