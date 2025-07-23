using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
namespace GuessTheWord.Business.Levels.Onboarding;

[LevelAction("run", OnboardingConstants.LevelName)]
public class RunAction : ILevelAction
{
    private readonly Game.GameInfo _game;
    private readonly TelegramHelper _telegram;
    private readonly DbStorageGame _db;

    public RunAction(
        Game.GameInfo game,
        TelegramHelper telegram,
        DbStorageGame db)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task HandleAsync(TelegramCommand command)
    {
        // remove onboarding message
        var room = _db.Rooms.FindById(command.ChatId) ?? throw new InvalidOperationException("Room not found");
        if (room.OnboardingMessageId.HasValue)
        {
            await _telegram.DeleteMessageAsync(room.OnboardingMessageId.Value);
            room = room with { OnboardingMessageId = null };
            _db.Rooms.Upsert(room);
        }

        // start the game
        await _game.StartGameAsync();
    }
}
