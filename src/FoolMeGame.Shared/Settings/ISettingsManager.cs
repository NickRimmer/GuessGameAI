namespace FoolMeGame.Shared.Settings;

public interface ISettingsManager
{
    bool TryGetSettings(long chatId, out object settings);
    object GetSettings(long chatId);

    void SetSettings(long chatId, object? settings);
    bool IsUserAdmin(long chatId, long userId);
}
