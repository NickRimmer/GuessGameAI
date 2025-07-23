using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
namespace GuessTheWord.Business.Levels.OpenWorld;

[LevelAction("help")]
public class HelpAction : ILevelAction
{
    private readonly TelegramHelper _telegram;

    public HelpAction(TelegramHelper telegram)
    {
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public async Task HandleAsync(TelegramCommand command)
    {
        var message = new[] {
            $"About game 🤖 (chat: {command.ChatId} / {command.UserId})",
            string.Empty,
            "This is a game where you can play with an AI agent. The agent will try to guess secret word, and you have to help with hints.",
        }.Join("\n");
        await _telegram.SendMessageBackAsync(message);
    }
}
