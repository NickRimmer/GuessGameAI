using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Levels.Onboarding;
namespace GuessTheWord.Business.Levels.OpenWorld;

[LevelAction("new", LevelActionAttribute.OnBaseLevel)]
public class CreateNewGameAction : ILevelAction
{
    private readonly LevelsManager _levelsManager;
    private readonly ISettingsManager _settingsManager;
    private readonly TelegramHelper _telegram;
    private readonly JoinAction _joinAction;
    private readonly DbStorageGame _db;

    public CreateNewGameAction(
        LevelsManager levelsManager,
        ISettingsManager settingsManager,
        TelegramHelper telegram,
        JoinAction joinAction,
        DbStorageGame db)
    {
        _levelsManager = levelsManager ?? throw new ArgumentNullException(nameof(levelsManager));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _joinAction = joinAction ?? throw new ArgumentNullException(nameof(joinAction));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task HandleAsync(TelegramCommand command)
    {
        CreateRoom(command.ChatId, command.UserId);
        _levelsManager.Set(OnboardingConstants.LevelName);
        _joinAction.JoinRoomAsync(command.ChatId, command.UserId, command.UserName);

        if (_telegram.HasCallback() && _telegram.CallbackQuery.Message != null)
            return _telegram.DeleteMessageAsync(_telegram.CallbackQuery.Message.Id);

        return Task.CompletedTask;
    }

    private void CreateRoom(long chatId, long creatorId)
    {
        var settings = (ChatSettingsEntity) _settingsManager.GetSettings(chatId);
        var room = new RoomEntity {
            Id = chatId,
            CreatorId = creatorId,
            MaxWords = settings.MaxWordsHint,
        };

        _db.Rooms.Upsert(room);
    }
}
