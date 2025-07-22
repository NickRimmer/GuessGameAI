using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using Telegram.Bot.Types.ReplyMarkups;
namespace GuessTheWord.Business.Levels.Onboarding;

[LevelAction("join", OnboardingConstants.LevelName)]
public class JoinAction : ILevelAction
{
    private readonly DbStorageGame _db;
    private readonly ISettingsManager _settingsManager;
    private readonly TelegramHelper _telegram;

    public JoinAction(
        DbStorageGame db,
        ISettingsManager settingsManager,
        TelegramHelper telegram)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public Task HandleAsync(TelegramCommand command) =>
        JoinRoomAsync(command.ChatId, command.UserId, command.UserName);

    public Task JoinRoomAsync(long chatId, long userId, string userName)
    {
        var room = _db.Rooms.FindById(chatId) ?? throw new InvalidOperationException("Room not found");
        if (room.Players.Any(x => x.Id == userId))
            return _telegram.SendBackAsync($"@{userName}, you are already in the room.");

        room.Players.Add(new RoomPlayerEntity {
            Id = userId,
            UserName = userName,
        });

        _db.Rooms.Upsert(room);
        return RefreshOnboardingMessageAsync(room);
    }

    private async Task RefreshOnboardingMessageAsync(RoomEntity room)
    {
        var settings = (ChatSettingsEntity) _settingsManager.GetSettings(room.Id);

        var buttons = new InlineKeyboardMarkup([
            new[] {
                room.Players.Count >= settings.MinPlayersToStart ? new InlineKeyboardButton("Start") { CallbackData = "/run" } : null,
                room.Players.Count < settings.MaxPlayersToPlay ? new InlineKeyboardButton("Join") { CallbackData = "/join" } : null,
                new InlineKeyboardButton("Cancel") { CallbackData = "/cancel" },
            }.OfType<InlineKeyboardButton>(),
        ]);

        var text = new[] {
                "New game room created!",
                "You can join the game using the command: /join or click 'Join'",
                string.Empty,
                $"Players: {room.Players.Count}",
            }
            .Concat(room.Players.Select(x => $"- {x.UserName}"))
            .Join("\n");

        if (room.OnboardingMessageId.HasValue)
        {
            await _telegram.EditMessageAsync(room.OnboardingMessageId.Value, text, replyMarkup: buttons);
            return;
        }

        var message = await _telegram.SendMessageBackAsync(text, replyMarkup: buttons);
        if (message != null)
        {
            room = room with {
                OnboardingMessageId = message.Id,
            };
            _db.Rooms.Upsert(room);
        }
    }
}
