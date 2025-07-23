using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
namespace GuessTheWord.Business.Levels.OpenWorld;

[LevelAction("me")]
public class MeAction : ILevelAction
{
    private readonly DbStorageGame _db;
    private readonly TelegramHelper _telegram;

    public MeAction(DbStorageGame db, TelegramHelper telegram)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public Task HandleAsync(TelegramCommand command)
    {
        var player = _db.Players.FindById(command.UserId) ??
            new PlayerEntity { UserId = command.UserId, UserName = command.UserName };

        var message = new[] {
            $"ℹ️ @{command.UserName} stats:",
            string.Empty,
            $"- score: <b>{player.Score}</b>",
            $"- total games played: <b>{player.PlayedGames}</b>",
        }.Join("\n");

        return _telegram.SendBackAsync(message);
    }
}
