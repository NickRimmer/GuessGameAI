using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using Telegram.Bot.Types.Enums;
namespace GuessTheWord.Business.Game;

public class GameInfo
{
    public const string LevelName = "single_word_game";

    private readonly LevelsManager _levelsManager;
    private readonly ISettingsManager _settingsManager;
    private readonly IWordsProvider _wordsProvider;
    private readonly DbStorageGame _db;
    private readonly TelegramHelper _telegram;

    public GameInfo(
        LevelsManager levelsManager,
        ISettingsManager settingsManager,
        IWordsProvider wordsProvider,
        DbStorageGame db,
        TelegramHelper telegram)
    {
        _levelsManager = levelsManager ?? throw new ArgumentNullException(nameof(levelsManager));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _wordsProvider = wordsProvider ?? throw new ArgumentNullException(nameof(wordsProvider));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public async Task StartGameAsync()
    {
        _levelsManager.Set(LevelName);
        ArrangePlayersOrder();
        await SendWelcomeMessageAsync();
        using (_telegram.KeepStatus(ChatAction.Typing))
        {
            await Task.Delay(2000); // Wait for 2 seconds before starting the game
            await SendTheWordAsync();
        }
    }

    private void ArrangePlayersOrder()
    {
        var room = _db.Rooms.FindById(_telegram.ChatId) ?? throw new InvalidOperationException("Room not found");
        room = room with {
            Players = room.Players
                .OrderBy(_ => Random.Shared.Next())
                .ToList(),
            Round = 0,
        };

        _db.Rooms.Upsert(room);
    }

    private async Task SendTheWordAsync()
    {
        // save the word 
        var room = _db.Rooms.FindById(_telegram.ChatId) ?? throw new InvalidOperationException("Room not found");
        var settings = (ChatSettingsEntity) _settingsManager.GetSettings(_telegram.ChatId);
        var phrase = await _wordsProvider.GetNewWordAsync(settings.Language);
        room = room with {
            Phrase = phrase.Split(' ').Select(x => new WordEntity(x)).ToList(),
        };
        _db.Rooms.Upsert(room);

        // find the first player
        var player = room.Players[room.Round % room.Players.Count];

        // if (room.OnboardingMessageId.HasValue)
        // {
        //     await _telegram.EditMessageAsync(room.OnboardingMessageId.Value, $"ℹ️ Word: *{room.Phrase.Select(x => x.Word).Join(" ")}*.");
        //     await _telegram.PinMessageAsync(room.OnboardingMessageId.Value);
        // }

        // send message back
        var message = await _telegram.SendMessageBackAsync($"ℹ️ Word: *{room.Phrase.Select(x => x.Word).Join(" ")}*.");
        await _telegram.PinMessageAsync(message!.Id);
        await _telegram.SendMessageBackAsync($"@{player.UserName}, you can start first.", parseMode: ParseMode.Markdown);

        room = room with {
            OnboardingMessageId = message.Id,
        };
        _db.Rooms.Upsert(room);
    }

    private Task SendWelcomeMessageAsync()
    {
        var settings = (ChatSettingsEntity) _settingsManager.GetSettings(_telegram.ChatId);

        var text = new[] {
            "Welcome to the *Guess The Word Game*! 🎉",
            "In this game, bot will try to guess secret word by your hints.",
            string.Empty,
            $"You can send `{settings.MaxWordsHint}` words as hints.",
        }.Join("\n");

        return _telegram.SendMessageBackAsync(text, parseMode: ParseMode.Markdown);
    }
}
