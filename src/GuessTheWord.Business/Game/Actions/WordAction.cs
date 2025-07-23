using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using Telegram.Bot.Types.Enums;
namespace GuessTheWord.Business.Game.Actions;

[LevelAction("word", GameInfo.LevelName)]
public class WordAction : ILevelAction
{
    private readonly DbStorageGame _db;
    private readonly TelegramHelper _telegram;

    public WordAction(DbStorageGame db, TelegramHelper telegram)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public Task HandleAsync(TelegramCommand command)
    {
        var room = _db.Rooms.FindById(command.ChatId) ?? throw new InvalidOperationException("Room not found");
        if (room.Phrase is not {Count: > 0})
            return _telegram.SendMessageBackAsync("⚠️ The game is not running.", asReply: true);

        return _telegram.SendMessageBackAsync(
            new[] {
                $"ℹ️ *{room.Phrase.Select(x => x.Word).Join(" ")}*",
                string.Empty,
                $"Bot tried *{room.Round}* times.",
            }.Join("\n"),
            parseMode: ParseMode.Markdown,
            asReply: true);
    }
}
