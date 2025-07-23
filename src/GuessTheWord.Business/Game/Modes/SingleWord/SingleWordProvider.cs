using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
namespace GuessTheWord.Business.Game.Modes.SingleWord;

public class SingleWordProvider: IWordsProvider
{
    private const string LastWordsStateKey = "lastWords";

    private readonly DbStorageGame _db;
    private readonly ILogger<SingleWordProvider> _logger;
    private readonly ChatClient _ai;

    public SingleWordProvider(
        Func<ChatClient> aiProvider,
        DbStorageGame db,
        ILogger<SingleWordProvider> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ai = aiProvider?.Invoke() ?? throw new ArgumentNullException(nameof(aiProvider));
    }

    public async Task<string> GetNewWordAsync(string language)
    {
        var lastWords = _db.GameStates.FindById(LastWordsStateKey)?.Value.Split(',').TakeLast(50).ToList();

        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                    "You are a word generator for a fun word guessing game.",
                    $"Your task is to generate one interesting and playful {language} noun that will be used as the secret word in the game.",
                    "",
                    "Rules:",
                    "- Always return only one single word — no punctuation, no explanations.",
                    $"- The word MUST be in {language} language and in nominative case.",
                    "- It must be a noun.",
                    "- It should be well-known, concrete to guess using associative hints.",
                    "- Avoid abstract, technical, rare, or overly complex words.",
                    "- Avoid words that are too similar to each other or to the last words used in the game.",
                    "- Fun, silly, or visual words are great for the game.",
                    "",
                    $"Respond with just one {language} noun. Nothing else.",
                }
                .Concat([
                    "Here are the last words used in the game, you should not repeat them:",
                    lastWords is { Count: > 0 } ? string.Join(", ", lastWords) : "No previous words.",
                ])
                .Join("\n")),
        ];

        // request AI to generate a new word
        var response = await _ai.CompleteChatAsync(messages);
        var secretWord = response.Value.Content.FirstOrDefault()?.Text.Trim() ?? string.Empty;

        // clean up the word
        var secretWordParts = secretWord.Split(' ');
        if (secretWordParts.Length > 1)
            _logger.LogWarning("Generated word contains multiple parts: {Word}", secretWord);

        secretWord = secretWordParts[0];
        if (string.IsNullOrWhiteSpace(secretWord))
            throw new InvalidOperationException("Generated word is empty or whitespace.");

        // remember the last words used in the game
        _db.GameStates.Upsert(new GameStateEntity {
            Key = LastWordsStateKey,
            Value = string.Join(',', lastWords?.Append(secretWord) ?? [secretWord]),
        });

        // return the secret word
        return secretWord;
    }
}
