using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using Telegram.Bot.Types.ReplyMarkups;
namespace GuessTheWord.Business.Levels.Onboarding;

[LevelAction("cancel", OnboardingConstants.LevelName)]
public class CancelAction : ILevelAction
{
    private readonly DbStorageGame _db;
    private readonly TelegramHelper _telegram;
    private readonly LevelsManager _levelsManager;

    public CancelAction(DbStorageGame db, TelegramHelper telegram, LevelsManager levelsManager)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _levelsManager = levelsManager ?? throw new ArgumentNullException(nameof(levelsManager));
    }

    public async Task HandleAsync(TelegramCommand command)
    {
        await CancelRoomAsync(command.ChatId);
        _levelsManager.SetBaseLevel();
    }

    public async Task CancelRoomAsync(long chatId, string cancellationMessage = "The game has been cancelled.")
    {
        var room = _db.Rooms.FindById(chatId) ?? throw new InvalidOperationException("Room not found");

        if (room.OnboardingMessageId.HasValue)
            await _telegram.UnpinMessageAsync(room.OnboardingMessageId.Value);

        var buttons = new InlineKeyboardMarkup([
            [new InlineKeyboardButton("New Game") { CallbackData = "/new" }],
        ]);

        await _telegram.SendMessageBackAsync(cancellationMessage, replyMarkup: buttons);

        _db.Rooms.Delete(room.Id);
    }
}
