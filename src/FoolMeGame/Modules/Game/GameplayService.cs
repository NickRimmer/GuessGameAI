using FoolMeGame.Modules.Agents;
using FoolMeGame.Modules.Data;
using FoolMeGame.Modules.Data.Models;
using FoolMeGame.Modules.Game.Models;
using FoolMeGame.Modules.Telegram;
using FoolMeGame.Modules.Telegram.Services;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace FoolMeGame.Modules.Game;

public class GameplayService
{
    private readonly DbStorage _db;
    private readonly AiAgent _aiAgent;
    private readonly ChatSettingsService _chatSettings;
    private readonly WordsGeneratorAgent _wordsAgent;

    public GameplayService(
        DbStorage db,
        AiAgent aiAgent,
        ChatSettingsService chatSettings,
        WordsGeneratorAgent wordsAgent)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _aiAgent = aiAgent ?? throw new ArgumentNullException(nameof(aiAgent));
        _chatSettings = chatSettings ?? throw new ArgumentNullException(nameof(chatSettings));
        _wordsAgent = wordsAgent ?? throw new ArgumentNullException(nameof(wordsAgent));
    }

    public async Task<bool> TryRunAsync(long chatId, TelegramHelper telegram)
    {
        var room = _db.Rooms.FindById(chatId);
        if (room == null || room.IsRunning) return false;

        // remove join message if exists
        if (room.JoinMessageId.HasValue)
            await telegram.EditMessageAsync(room.JoinMessageId.Value, room.Id, "The game has started!");

        // send welcome message
        var welcomeText = new[] {
            "Welcome to the game!",
            "Your goal is to explain target word to me without using the word itself or it's forms.",
        }.Join("\n");

        await telegram.SendMessageBackAsync(welcomeText, asReply: true);
        await telegram.SendStatusAsync(ChatAction.Typing);

        // artificially delay to simulate game start
        await Task.Delay(1000);

        var settings = _chatSettings.GetSettings(chatId);
        var word = await _wordsAgent.GetAiRandomWordAsync(settings.Language);
        // var word = _wordsAgent.GetRandomWord();

        room = room with {
            TheWord = word,
            MaxWords = settings.MaxWordsHint,
        };

        _db.Rooms.Update(room);

        var rnd = new Random(room.RandomSeed);
        var orderedPlayers = room
            .Players
            .OrderBy(_ => rnd.Next())
            .ToList();

        var playerMessage = new[] {
            $"🔒 Secret word: <b>{word}</b>",
            string.Empty,
            $"😎 @{orderedPlayers[0].UserName}, you are the first!",
            $"Maximum <b>{settings.MaxWordsHint}</b> word(s) per message allowed.",
        }.Join("\n");
        await telegram.SendMessageBackAsync(playerMessage, asReply: true);

        room = room with {
            IsRunning = true,
            Players = orderedPlayers,
        };
        _db.Rooms.Update(room);

        return true;
    }

    public bool IsRunning(long chatId)
    {
        var room = _db.Rooms.FindById(chatId);
        return room?.IsRunning ?? false;
    }

    public bool IsUserTurn(long chatId, long userId)
    {
        var room = _db.Rooms.FindById(chatId);
        if (room is not { IsRunning: true }) return false;

        var currentPlayer = room.Players[room.Round % room.Players.Count];
        return currentPlayer.UserId == userId;
    }

    public async Task ProcessTextAsync(long chatId, long userId, string hintText, TelegramHelper telegram)
    {
        var room = _db.Rooms.FindById(chatId);
        if (room is not { IsRunning: true, TheWord: not null })
        {
            await telegram.SendMessageBackAsync("⚠️ The game is not running.", asReply: true);
            return;
        }

        var roomSettings = _chatSettings.GetSettings(chatId);

        var hintWords = hintText.Split([" ", "-", ".", "_"], StringSplitOptions.RemoveEmptyEntries);
        if (hintWords.Length > room.MaxWords || hintWords.Length == 0)
        {
            await telegram.SendMessageBackAsync($"⚠️ Too long hint, maximum {room.MaxWords} word(s) allowed.", asReply: true);
            return;
        }

        _ = telegram.SendStatusAsync(ChatAction.Typing);
        var isValidHint = await _aiAgent.ValidateHintAsync(hintText, room.TheWord!);
        if (!isValidHint)
        {
            await telegram.SendMessageBackAsync("⚠️ Invalid hint! Do not use the target word or its forms in your hints.", asReply: true);
            return;
        }

        // update history
        room.History.Add(new RoomHistory(hintText, RoomHistoryType.Hint));
        _db.Rooms.Update(room);

        // guess the word
        _ = telegram.SendStatusAsync(ChatAction.Typing);
        var guessedWord = await GuessWordAsync(room);
        var isGuessed = await CompareWordsAsync(guessedWord, room.TheWord!);

        if (!isGuessed)
        {
            room = room with {
                Round = room.Round + 1,
            };

            // room.Guesses.Add(guessedWord);
            room.History.Add(new RoomHistory(hintText, RoomHistoryType.Guess));
            _db.Rooms.Update(room);

            var nextPlayer = room
                .Players
                .ElementAt(room.Round % room.Players.Count);

            if (room.Round > roomSettings.MaxTurns)
            {
                var gameOverButtons = new InlineKeyboardMarkup([
                    [
                        new InlineKeyboardButton("Create new") { CallbackData = "/new" },
                    ],
                ]);

                UpdateLeaderboard(room.Players, x => x with { PlayedGames = x.PlayedGames + 1 }); // Increment played games for all players
                _db.Rooms.Delete(chatId);

                var endMessage = new[] {
                    $"My wrong guess: <b>{guessedWord}</b> 🥹",
                    string.Empty,
                    "🏁 The game has ended 🏁",
                    "Oh... this is to hard for me, so let's stop this game! To start a new one, use `/new` command.",
                }.Join("\n");
                await telegram.SendMessageBackAsync(endMessage, replyMarkup: gameOverButtons);
            }
            else
            {

                var replyText = new[] {
                    $"My wrong guess: <b>{guessedWord}</b> 🥹",
                    string.Empty,
                    (roomSettings.MaxTurns - room.Round) <= 3 ? $"<b>Only {roomSettings.MaxTurns - room.Round + 1} tries left!</b>" : null,
                    $"@{nextPlayer.UserName}, it's your turn!",
                }.OfType<string>().Join("\n");

                var wrongGuessButtons = room.Round % 10 == 9 ? new InlineKeyboardMarkup([
                    [
                        new InlineKeyboardButton("What was the word?") { CallbackData = "/word" },
                        new InlineKeyboardButton("Too hard, Cancel") { CallbackData = "/cancel" },
                    ],
                ]) : null;

                await telegram.SendMessageBackAsync(replyText, replyMarkup: wrongGuessButtons);
            }
            return;
        }

        // If the word is guessed
        _wordsAgent.UpdateStats(room.TheWord!, x => x with { GuessedCount = x.GuessedCount + 1 });
        var user = room.Players.First(x => x.UserId == userId);
        UpdateLeaderboard([user], x => x with { Score = x.Score + 1 }); // Increment the user's score
        UpdateLeaderboard(room.Players, x => x with { PlayedGames = x.PlayedGames + 1 }); // Increment played games for all players

        var winnerUserName = user.UserName ?? userId.ToString();
        await telegram.SendMessageBackAsync(new[] {
                $"🏆 @{winnerUserName} win! Congratulations!",
                $"Word found: <b>{room.TheWord}</b>",
                string.Empty,
            }
            .Concat(GetLeaderboardText(room.Players.Select(x => x.UserId).ToArray()))
            .Join("\n"), asReply: true);

        // stop the game
        _db.Rooms.Delete(chatId);

        await Task.Delay(2000);
        var buttons = new InlineKeyboardMarkup([
            [
                new InlineKeyboardButton("Create new") { CallbackData = "/new" },
            ],
        ]);

        await telegram.SendMessageBackAsync("🏁 The game has ended 🏁\n\nThanks for playing! To start a new one, use /new command.", replyMarkup: buttons);
    }

    private IReadOnlyCollection<string> GetLeaderboardText(IReadOnlyCollection<long> playerIds)
    {
        var players = _db.Leaderboard.Find(x => playerIds.Contains(x.UserId));
        return players
            .GroupBy(x => x.Score)
            .OrderByDescending(x => x.Key)
            .Select((x, i) => new {
                Position = i + 1,
                Players = x.OrderByDescending(p => p.PlayedGames).ThenBy(p => p.UserName).ToList(),
            })
            .SelectMany(group => group.Players.Select(player => $"{group.Position}. <b>{player.UserName}</b>: {player.Score} points ({player.PlayedGames} games)"))
            .ToList();
    }

    private Task<string> GuessWordAsync(RoomEntity room)
    {
        var settings = _chatSettings.GetSettings(room.Id);
        return _aiAgent.GuessWordAsync(room.History, settings.Language);
    }

    private async Task<bool> CompareWordsAsync(string guessedWord, string targetWord)
    {
        // TODO implement word comparison logic
        var isEqual = string.Equals(guessedWord, targetWord, StringComparison.OrdinalIgnoreCase);
        if (isEqual) return true;

        // TODO implement fuzzy comparison logic if needed
        return await _aiAgent.IsGuessCorrectAsync(guessedWord, targetWord);
    }

    private void UpdateLeaderboard(IReadOnlyCollection<PlayerEntity> users, Func<LeaderboardEntity, LeaderboardEntity> updateAction)
    {
        foreach (var user in users)
        {
            var leader = _db.Leaderboard.FindById(user.UserId);
            if (leader != null)
            {
                leader = updateAction(leader) with {
                    UserName = user.UserName,
                };
                _db.Leaderboard.Update(leader);
            }
            else
            {
                leader = updateAction(new LeaderboardEntity {
                    UserId = user.UserId,
                    UserName = user.UserName,
                });
                _db.Leaderboard.Insert(leader);
            }
        }
    }

    public MeDetails GetMe(long userId)
    {
        var result = new MeDetails();

        var leaderboard = _db.Leaderboard.FindById(userId);
        if (leaderboard != null)
        {
            result = result with {
                Score = leaderboard.Score,
                PlayedGames = leaderboard.PlayedGames,
            };
        }

        return result;
    }
}
