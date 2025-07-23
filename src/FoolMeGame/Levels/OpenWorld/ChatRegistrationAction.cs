using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
namespace FoolMeGame.Levels.OpenWorld;

[LevelAction(RegisterCommand, LevelActionAttribute.OnBaseLevel)]
[LevelAction(UnregisterCommand, LevelActionAttribute.OnBaseLevel)]
public class ChatRegistrationAction : ILevelAction
{
    private readonly ISettingsManager _settingsManager;
    private readonly TelegramHelper _telegram;
    private const string RegisterCommand = "register";
    private const string UnregisterCommand = "unregister";

    public ChatRegistrationAction(
        ISettingsManager settingsManager,
        TelegramHelper telegram)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public Task HandleAsync(TelegramCommand command) => command.Name switch {
        RegisterCommand => RegisterAsync(command),
        UnregisterCommand => UnregisterAsync(command),
        _ => throw new NotSupportedException($"Command '{command.Name}' is not supported in {nameof(ChatRegistrationAction)}."),
    };

    private Task RegisterAsync(TelegramCommand command)
    {
        var hasSettings = _settingsManager.TryGetSettings(command.ChatId, out var settings);
        if (hasSettings)
            return _telegram.SendBackAsync("⚠️ This chat is already registered.");

        _settingsManager.SetSettings(command.ChatId, settings);
        return _telegram.SendBackAsync("✅ This chat has been registered successfully.");
    }

    private Task UnregisterAsync(TelegramCommand command)
    {
        var hasSettings = _settingsManager.TryGetSettings(command.ChatId, out _);
        if (!hasSettings)
            return _telegram.SendBackAsync("⚠️ This chat is not registered.");

        _settingsManager.SetSettings(command.ChatId, null);

        return _telegram.SendBackAsync("ℹ️ This chat has been unregistered successfully.");
    }
}
