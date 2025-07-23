using GuessTheWord.Business.Data;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
namespace GuessTheWord.Business.Game.Modes.TwoWords;

public class TwoWordsProvider : IWordsProvider
{
    private const string LastWordsStateKey = "lastTwoWords";
    private readonly DbStorageGame _db;
    private readonly ILogger<TwoWordsProvider> _logger;
    private readonly ChatClient _ai;

    public TwoWordsProvider(
        Func<ChatClient> aiProvider,
        DbStorageGame db,
        ILogger<TwoWordsProvider> logger)
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
                    $"Your task is to generate an interesting and playful two-word phrase in {language}, consisting of an adjective and a noun, that will be used as the secret phrase in the game.",
                    "",
                    "- Always return exactly two words: an adjective followed by a noun.",
                    "- No punctuation, no explanations — just the two words.",
                    $"- The phrase MUST be in {language} and in nominative case.",
                    "- The noun should be well-known, concrete, and easy to guess using associative hints.",
                    "- The adjective should be meaningful and help create a fun or visual image (e.g., color, size, mood, style, etc.).",
                    "- Avoid rare, abstract, or overly technical words.",
                    "- Avoid combinations that are too similar to each other or to recently used ones.",
                    "",
                    $"Respond with just two words in {language}: adjective + noun. Nothing else.",
                }
                .Concat([
                    "Here are the last phrases used in the game, you should not repeat them:",
                    lastWords is { Count: > 0 } ? string.Join(", ", lastWords) : "No previous words.",
                ])
                .Join("\n")),
        ];

        // request AI to generate a new word
        var response = await _ai.CompleteChatAsync(messages);
        var secretPhrase = response.Value.Content.FirstOrDefault()?.Text.Trim() ?? string.Empty;

        // clean up the word
        var secretPhraseParts = secretPhrase.Split(' ');
        if (secretPhraseParts.Length != 2)
            _logger.LogWarning("Generated phrase does not contain exactly two words: {Phrase}", secretPhrase);

        if (secretPhraseParts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Generated word is empty or whitespace.");

        // remember the last words used in the game
        _db.GameStates.Upsert(new GameStateEntity {
            Key = LastWordsStateKey,
            Value = string.Join(',', lastWords?.Append(secretPhrase) ?? [secretPhrase]),
        });

        // return the secret word
        return secretPhrase;
    }
}
