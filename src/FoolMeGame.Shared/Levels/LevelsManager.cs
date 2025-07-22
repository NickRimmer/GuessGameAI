using FoolMeGame.Shared.Data;
using FoolMeGame.Shared.Data.Models;
using FoolMeGame.Shared.Telegram;
namespace FoolMeGame.Shared.Levels;

public class LevelsManager
{
    private readonly DbStorage _db;
    private readonly TelegramHelper _telegram;

    public LevelsManager(DbStorage db, TelegramHelper telegram)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public void SetBaseLevel()
    {
        Set(LevelActionAttribute.OnBaseLevel);
    }

    public void Set(string levelName)
    {
        var current = _db.ChatSystemSettings.FindById(_telegram.ChatId) ??
            new ChatSystemSettings { ChatId = _telegram.ChatId };

        current = current with { CurrentLevelName = levelName };
        _db.ChatSystemSettings.Upsert(current);
    }
}
