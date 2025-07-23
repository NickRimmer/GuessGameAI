using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Settings;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace GuessTheWord.Business.Game.Actions;

[LevelAction(LevelActionAttribute.TextCommand, GameInfo.LevelName)]
public class TextAction : ILevelAction
{
    private readonly DbStorageGame _db;
    private readonly IHintCheckService _hintCheckService;
    private readonly ILogger<TextAction> _logger;
    private readonly ISettingsManager _settingsManager;
    private readonly LevelsManager _levelsManager;
    private readonly Levels.Onboarding.CancelAction _cancelAction;
    private readonly TelegramHelper _telegram;

    public TextAction(
        DbStorageGame db,
        IHintCheckService hintCheckService,
        ILogger<TextAction> logger,
        ISettingsManager settingsManager,
        LevelsManager levelsManager,
        GuessTheWord.Business.Levels.Onboarding.CancelAction cancelAction,
        TelegramHelper telegram)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hintCheckService = hintCheckService ?? throw new ArgumentNullException(nameof(hintCheckService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _levelsManager = levelsManager ?? throw new ArgumentNullException(nameof(levelsManager));
        _cancelAction = cancelAction ?? throw new ArgumentNullException(nameof(cancelAction));
        _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
    }

    public Task HandleAsync(TelegramCommand command)
    {
        var room = _db.Rooms.FindById(command.ChatId) ?? throw new InvalidOperationException("Room not found");
        var currentPlayer = room.Players[room.Round % room.Players.Count];

        // check if it is the current player's turn and if the word is already set
        if (currentPlayer.Id != command.UserId || room.Phrase is not {Count: > 0}) return Task.CompletedTask;
        return ProcessHintAsync(command);
    }

    private async Task ProcessHintAsync(TelegramCommand command)
    {
        var room = _db.Rooms.FindById(command.ChatId) ?? throw new InvalidOperationException("Room not found");
        var settings = (ChatSettingsEntity) _settingsManager.GetSettings(command.ChatId);
        var theWord = room.Phrase?.Select(x => x.Word).Join(" ") ?? string.Empty;

        // validate the text and room word
        if (command.Body.IsEmpty() || string.IsNullOrWhiteSpace(theWord))
        {
            _logger.LogWarning("Received empty text or room word is not set. Text: {Text}, Room Word: {RoomWord}", command.Body, theWord);
            await _telegram.SendMessageBackAsync("⚠️ The game is not running.", asReply: true);
            return;
        }

        // validate hint
        if (!IsHintLengthValid(command.Body, room.MaxWords))
        {
            await _telegram.SendMessageBackAsync($"⚠️ The hint cannot be longer then {room.MaxWords} words.", asReply: true);
            return;
        }

        using (_telegram.KeepStatus(ChatAction.Typing))
        {
            var hintValidationResult = await _hintCheckService.ValidateHintAsync(command.Body, theWord ?? string.Empty);
            if (!hintValidationResult.IsValid)
            {
                await _telegram.SendMessageBackAsync("⚠️ " + (hintValidationResult.Reason.IsEmpty() ? "Invalid hint. The hint must not contain the secret word or its variations." : hintValidationResult.Reason), asReply: true);
                return;
            }
        }

        room.History.Add(new RoomHistory(command.Body, RoomHistoryType.Hint));

        // try guess the word
        var guess = await _hintCheckService.GuessTheWord(room.History, settings.Language, room.Phrase!);
        var validationResult = await _hintCheckService.ValidateGuessAsync(guess, room.Phrase!);
        var isValid = validationResult.All(x => x.IsCorrect);

        if (isValid) await WinTheGameAsync(command.ChatId, command.UserId);
        else await NextRoundGameAsync(guess, room, settings, validationResult);
    }

    private async Task WinTheGameAsync(long chatId, long userId)
    {
        var room = _db.Rooms.FindById(chatId) ?? throw new InvalidOperationException("Room not found");
        var theWord = room.Phrase?.Select(x => x.Word).Join(" ") ?? string.Empty;

        // update player states
        var leaderboard = new List<PlayerEntity>();
        foreach (var roomPlayer in room.Players)
        {
            var player = _db.Players.FindById(roomPlayer.Id) ?? new PlayerEntity {
                UserId = roomPlayer.Id,
                UserName = roomPlayer.UserName,
            };

            player = player with {
                Score = player.Score + (roomPlayer.Id == userId ? 1 : 0),
                PlayedGames = player.PlayedGames + 1,
                LastGameAtUtc = DateTime.UtcNow,
            };

            _db.Players.Upsert(player);
            leaderboard.Add(player);
        }

        // display results
        var winner = room.Players.First(x => x.Id == userId);
        var leaderboardScores = leaderboard
            .GroupBy(x => x.Score)
            .OrderByDescending(x => x.Key)
            .Select((x, i) => new {
                Position = i + 1,
                Players = x.OrderByDescending(p => p.PlayedGames).ThenBy(p => p.UserName).ToList(),
            })
            .SelectMany(group => group.Players.Select(player => $"{group.Position}. <b>{player.UserName}</b>: {player.Score} points ({player.PlayedGames} games)"))
            .ToList();

        var text = new[] {
                $"🏆 @{winner.UserName} win! Congratulations!",
                $"Word found: <b>{theWord}</b>",
                string.Empty,
            }
            .Concat(leaderboardScores)
            .Join("\n");

        await _telegram.SendMessageBackAsync(text, asReply: true);

        // stop the game
        await Task.Delay(2000);
        await CancelGameAsync(chatId, "🏁 The game has ended 🏁\n\nThanks for playing! To start a new one, use /new command.");
    }

    private async Task NextRoundGameAsync(string guess, RoomEntity room, ChatSettingsEntity settings, IReadOnlyCollection<GuessValidationWord> validationResult)
    {
        room = room with {
            Round = room.Round + 1,
        };

        // if the game has reached the maximum number of turns, stop the game
        if (room.Round > settings.MaxTurns)
        {
            await StopTheGameAsync(room, guess);
            return;
        }

        // new round
        room.History.Add(new RoomHistory(guess, RoomHistoryType.Guess));
        var validWords = validationResult.Where(x => x.IsCorrect).ToList();
        room = room with {
            Phrase = room.Phrase?.Select(x => x with {
                IsSecret = !validWords.Any(v => v.Word.Equals(x.Word, StringComparison.OrdinalIgnoreCase)),
            }).ToList(),
        };

        _db.Rooms.Upsert(room);

        var nextPlayer = room
            .Players
            .ElementAt(room.Round % room.Players.Count);

        var replyText = new[] {
            $"<b>{guess}</b> - ❌ Incorrect guess!",
            string.Empty,
            (settings.MaxTurns - room.Round) <= 3 ? $"<b>Only {settings.MaxTurns - room.Round + 1} tries left!</b>" : null,
            $"@{nextPlayer.UserName}, it's your turn!",
        }.OfType<string>().Join("\n");

        var buttons = room.Round % 10 == 9 ? new InlineKeyboardMarkup([
            [
                new InlineKeyboardButton("Remind word") { CallbackData = "/word" },
                new InlineKeyboardButton("Stop game") { CallbackData = "/cancel" },
            ],
        ]) : null;

        await _telegram.SendMessageBackAsync(replyText, replyMarkup: buttons);
    }

    private async Task StopTheGameAsync(RoomEntity room, string guess)
    {
        foreach (var roomPlayer in room.Players)
        {
            var player = _db.Players.FindById(roomPlayer.Id) ?? new PlayerEntity {
                UserId = roomPlayer.Id,
                UserName = roomPlayer.UserName,
            };

            player = player with {
                PlayedGames = player.PlayedGames + 1,
                LastGameAtUtc = DateTime.UtcNow,
            };

            _db.Players.Upsert(player);
        }

        var endMessage = new[] {
            $"<b>{guess}</b> - ❌ Incorrect guess!",
            string.Empty,
            "🏁 The game has ended 🏁",
            "Oh... this is to hard for me, so let's stop this game! To start a new one, use `/new` command.",
        }.Join("\n");

        // stop the game
        await CancelGameAsync(room.Id, endMessage);
    }

    private Task CancelGameAsync(long chatId, string message)
    {
        _levelsManager.SetBaseLevel();
        return _cancelAction.CancelRoomAsync(chatId, message);
    }

    private static bool IsHintLengthValid(string text, int maxWordsHint)
    {
        var hintWords = text.Split([" ", "-", ".", "_"], StringSplitOptions.RemoveEmptyEntries);
        return hintWords.Length > 0 && hintWords.Length <= maxWordsHint;
    }
}
