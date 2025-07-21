namespace FoolMeGame.Modules.Telegram;

public record TelegramSettings
{
    public required string Token { get; init; }
}
