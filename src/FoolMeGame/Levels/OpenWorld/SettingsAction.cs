using System.Reflection;
using FoolMeGame.Shared.Data;
using FoolMeGame.Shared.Data.Models;
using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using Telegram.Bot.Types.Enums;
namespace FoolMeGame.Levels.OpenWorld;

[LevelAction("settings")]
public class SettingsAction : ILevelAction
{
    private readonly ISettingsManager _settingsManager;
    private readonly TelegramHelper _telegram;
    private readonly DbStorage _db;

    public SettingsAction(ISettingsManager settingsManager, TelegramHelper telegram, DbStorage db)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task HandleAsync(TelegramCommand command)
    {
        var isUserAdmin = _settingsManager.IsUserAdmin(command.ChatId, command.UserId);
        if (!isUserAdmin)
            return _telegram.SendBackAsync("You do not have permission to change settings.", asReply: true);

        return command.Parameters.Count == 0
            ? ShowSettingsAsync(command)
            : SetSettingsAsync(command);
    }

    private async Task<bool> ShowSettingsAsync(TelegramCommand command)
    {
        _ = _settingsManager.TryGetSettings(command.ChatId, out var settings);
        var currentSettings = settings
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x.GetValue(settings)?.ToString() ?? string.Empty);

        var chatSystemSettings = _db.ChatSystemSettings.FindById(command.ChatId)
            ?? new ChatSystemSettings { ChatId = command.ChatId };

        var currentLevelName = chatSystemSettings.CurrentLevelName;
        if (string.IsNullOrEmpty(currentLevelName)) currentLevelName = "Base Level";

        var message = new[] {
                "Settings mode enabled. Please provide settings list to change.",
                string.Empty,
                "# Current settings:",
            }
            .Concat(currentSettings.Select(x => $"{x.Key}: *{x.Value}*"))
            .Concat([string.Empty, $"# Current level: *{currentLevelName}*"])
            .Concat([
                string.Empty,
                "To change a setting, send a message in the format:",
                "`/settings <setting_name> <new_value>`",
            ])
            .Join("\n");

        await _telegram.SendBackAsync(message, parseMode: ParseMode.Markdown);
        return true;
    }

    private async Task<bool> SetSettingsAsync(TelegramCommand command)
    {
        var args = command.Parameters.Take(2).ToArray();
        if (args.Length != 2)
        {
            await _telegram.SendMessageBackAsync("Invalid command format. Use: `/settings <setting_name> <new_value>`", parseMode: ParseMode.Markdown);
            return true;
        }

        var settingName = args[0];
        var newValue = args[1];

        // get current settings
        _ = _settingsManager.TryGetSettings(command.ChatId, out var settings);
        var props = settings
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // find property by name
        var prop = props.FirstOrDefault(x => string.Equals(x.Name, settingName, StringComparison.OrdinalIgnoreCase));
        if (prop is null)
        {
            await _telegram.SendMessageBackAsync($"⚠️ Setting `{settingName}` does not exist.", parseMode: ParseMode.Markdown);
            return true;
        }

        // update property value
        try
        {
            var convertedValue = Convert.ChangeType(newValue, prop.PropertyType);
            prop.SetValue(settings, convertedValue);
        }
        catch (Exception ex)
        {
            await _telegram.SendMessageBackAsync($"⚠️ Failed to set `{settingName}` to `{newValue}`: {ex.Message}", parseMode: ParseMode.Markdown);
            return true;
        }

        // save updated settings
        _settingsManager.SetSettings(command.ChatId, settings);

        await _telegram.SendMessageBackAsync($"Setting `{settingName}` updated to `{newValue}`.", parseMode: ParseMode.Markdown);
        return true;
    }
}
