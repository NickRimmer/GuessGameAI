using FoolMeGame.Shared.Settings;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
namespace GuessTheWord.Business.Settings;

public class GameSettingsManager : ISettingsManager
{
    private readonly DbStorageGame _db;

    public GameSettingsManager(DbStorageGame db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public bool TryGetSettings(long chatId, out object settings)
    {
        var found = _db.ChatSettings.FindById(chatId);
        settings = found ?? new ChatSettingsEntity { Id = chatId };
        return found != null;
    }

    public object GetSettings(long chatId)
    {
        var settings = _db.ChatSettings.FindById(chatId) ?? new ChatSettingsEntity { Id = chatId };
        return settings;
    }

    public void SetSettings(long chatId, object? settings)
    {
        if (settings == null)
            _db.ChatSettings.Delete(chatId);
        else if (settings is ChatSettingsEntity chatSettings)
            _db.ChatSettings.Upsert(chatSettings);
        else
            throw new ArgumentException("Invalid settings type", nameof(settings));
    }

    public bool IsUserAdmin(long chatId, long userId) => userId == GameInfo.GlobalAdminUserId ||
        _db.ChatSettings.FindById(chatId)?.AdminId == userId;
}
