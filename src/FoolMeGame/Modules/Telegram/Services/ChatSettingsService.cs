using System.Reflection;
using FoolMeGame.Modules.Data;
using FoolMeGame.Modules.Data.Models;
namespace FoolMeGame.Modules.Telegram.Services;

public class ChatSettingsService
{
    private readonly DbStorage _db;

    public ChatSettingsService(DbStorage db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Dictionary<string, string> GetSettingsPairs(long chatId)
    {
        var chatSettings = GetSettings(chatId);
        var result = chatSettings
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => !new[] { nameof(ChatSettingsEntity.Id) }.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(x => x.Name, x => x.GetValue(chatSettings)?.ToString() ?? string.Empty);

        return result;
    }

    public bool SetSettings(long chatId, string key, string value)
    {
        var chatSettings = _db
            .Chats
            .FindById(chatId) ?? new ChatSettingsEntity { Id = chatId };

        var property = chatSettings
            .GetType()
            .GetProperty(key, BindingFlags.Public | BindingFlags.Instance);

        if (property == null)
            return false;

        try
        {
            var convertedValue = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(chatSettings, convertedValue);

            _db.Chats.Upsert(chatSettings);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public ChatSettingsEntity GetSettings(long chatId)
    {
        var chatSettings = _db
            .Chats
            .FindById(chatId) ?? new ChatSettingsEntity { Id = chatId };

        return chatSettings;
    }

    public bool IsChatRegistered(long chatId)
    {
        var chatSettings = _db
            .Chats
            .FindById(chatId);

        return chatSettings != null;
    }

    public void RegisterChat(long chatId)
    {
        if (IsChatRegistered(chatId))
            return;

        var chatSettings = new ChatSettingsEntity { Id = chatId };
        _db.Chats.Upsert(chatSettings);
    }

    public void UnregisterChat(long chatId)
    {
        if (!IsChatRegistered(chatId))
            return;

        _db.Chats.Delete(chatId);
    }
}
