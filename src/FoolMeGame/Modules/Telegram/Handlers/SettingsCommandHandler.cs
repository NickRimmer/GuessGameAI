using FoolMeGame.Modules.Telegram.Services;
using JetBrains.Annotations;
using Telegram.Bot.Types.Enums;
namespace FoolMeGame.Modules.Telegram.Handlers;

[UsedImplicitly]
[CommandNames(SettingsCommandName, RegisterCommandName, UnregisterCommandName)]
public class SettingsCommandHandler : IMessageCommandHandler
{
    private const string SettingsCommandName = "settings";
    private const string RegisterCommandName = "register";
    private const string UnregisterCommandName = "unregister";

    private readonly TelegramHelper _telegram;
    private readonly ChatSettingsService _chatSettings;

    public SettingsCommandHandler(TelegramHelper telegram, ChatSettingsService chatSettings)
    {
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
        _chatSettings = chatSettings ?? throw new ArgumentNullException(nameof(chatSettings));
    }

    public async Task<bool> HandleAsync(TelegramCommand command)
    {
        var settings = _chatSettings.GetSettings(command.ChatId);
        if (command.UserId != settings.AdminId && command.UserId != Constants.GlobalAdminUserId)
        {
            await _telegram.SendMessageBackAsync("You do not have permission to change settings.", asReply: true);
            return true;
        }

        return command.Name switch {
            SettingsCommandName => await HandleSettingsCommandAsync(command),
            RegisterCommandName => await HandleRegisterCommandAsync(command, true),
            UnregisterCommandName => await HandleRegisterCommandAsync(command, false),
            _ => false,
        };
    }

    private async Task<bool> HandleSettingsCommandAsync(TelegramCommand command)
    {
        var isRegistered = _chatSettings.IsChatRegistered(command.ChatId);
        if (!isRegistered)
        {
            await _telegram.SendMessageBackAsync($"This chat ({command.ChatId}) is not registered. Use `/register` to register it.", asReply: true);
            return true;
        }

        if (command.Parameters.Count == 0)
            return await ShowSettingsAsync(command);

        return await SetSettingsAsync(command);
    }

    private async Task<bool> HandleRegisterCommandAsync(TelegramCommand command, bool register)
    {
        if (register)
        {
            _chatSettings.RegisterChat(command.ChatId);
            await _telegram.SendMessageBackAsync("Chat registered successfully.", asReply: true);
        }
        else
        {
            _chatSettings.UnregisterChat(command.ChatId);
            await _telegram.SendMessageBackAsync("Chat unregistered successfully.", asReply: true);
        }

        return true;
    }

    private async Task<bool> ShowSettingsAsync(TelegramCommand command)
    {
        var currentSettings = _chatSettings.GetSettingsPairs(command.ChatId);
        var message = new[] {
                "Settings mode enabled. Please provide settings list to change.",
                string.Empty,
                "# Current settings:",
            }
            .Concat(currentSettings.Select(x => $"{x.Key}: *{x.Value}*"))
            .Concat([
                string.Empty,
                "To change a setting, send a message in the format:",
                "`/settings <setting_name> <new_value>`",
            ])
            .Join("\n");

        await _telegram.SendMessageBackAsync(message, parseMode: ParseMode.Markdown);
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
        var updated = _chatSettings.SetSettings(command.ChatId, settingName, newValue);

        if (!updated)
        {
            await _telegram.SendMessageBackAsync($"⚠️ Setting `{settingName}` cannot be set.", parseMode: ParseMode.Markdown);
            return true;
        }


        await _telegram.SendMessageBackAsync($"Setting `{settingName}` updated to `{newValue}`.", parseMode: ParseMode.Markdown);
        return true;
    }
}
