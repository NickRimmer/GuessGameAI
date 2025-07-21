using System.Text.Json;
using FoolMeGame.Modules.Data;
using FoolMeGame.Modules.Data.Models;
using OpenAI.Chat;
namespace FoolMeGame.Modules.Agents;

public class WordsGeneratorAgent
{
    private readonly DbStorage _db;
    private readonly ILogger<WordsGeneratorAgent> _logger;

    private readonly ChatClient _ai;
    private readonly Lazy<IReadOnlyCollection<string>> _words;

    public WordsGeneratorAgent(Func<ChatClient> aiProvider, DbStorage db, ILogger<WordsGeneratorAgent> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ai = aiProvider.Invoke();

        _words = new Lazy<IReadOnlyCollection<string>>(() =>
        {
            var json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "words.ru.json"));
            return JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize words from JSON file.");
        });
    }

    public string GetRandomWord()
    {
        if (_words.Value.Count == 0)
            throw new InvalidOperationException("No words available to generate.");

        var index = Random.Shared.Next(_words.Value.Count);
        return _words.Value.ElementAt(index);
    }

    public async Task<string> GetAiRandomWordAsync(string language)
    {
        var lastWords = _db
            .Words
            .Query()
            .OrderByDescending(x => x.ShowedCount)
            .Limit(25)
            .ToList();

        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are a word generator for a fun word guessing game.",
                "",
                "Your task is to generate one interesting and playful Russian noun that will be used as the secret word in the game.",
                "",
                "Rules:",
                "- Always return only one single word — no punctuation, no explanations.",
                $"- The word MUST be in {language} language and in nominative case.",
                "- It must be a noun.",
                "- It should be well-known, concrete, and easy enough for players to guess using associative hints.",
                "- Avoid abstract, technical, rare, or overly complex words.",
                "- Avoid words that are too similar to each other or to the last words used in the game.",
                "- Fun, silly, or visual words are great for the game.",
                "",
                $"Respond with just one {language} noun. Nothing else.",
            }.Join("\n")),

            new UserChatMessage("Last 25 words was you should not repeat: " + string.Join(", ", lastWords.Select(x => x.Word)) + $".\nIgnore previous words language and use only {language} language."),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var secretWord = response.Value.Content.FirstOrDefault()?.Text.Trim() ?? string.Empty;

        var secretWordParts = secretWord.Split(' ');
        if (secretWordParts.Length > 1)
            _logger.LogWarning("Generated word contains multiple parts: {Word}", secretWord);

        secretWord = secretWordParts[0];
        if (string.IsNullOrWhiteSpace(secretWord))
            throw new InvalidOperationException("Generated word is empty or whitespace.");

        UpdateStats(secretWord, x => x with { ShowedCount = x.ShowedCount + 1 });
        return secretWord;
    }

    public void UpdateStats(string word, Func<TheWordEntity, TheWordEntity> updater)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            _logger.LogWarning("Attempted to update stats with an empty or whitespace word");
            return;
        }

        var entity = _db.Words.FindOne(x => x.Word.Equals(word));
        if (entity == null)
        {
            entity = new TheWordEntity {
                Word = word,
                UpdatedAtUtc = DateTime.UtcNow,
            };

            _db.Words.Insert(entity);
        }
        else
        {
            _db.Words.Update(updater(entity) with { UpdatedAtUtc = DateTime.UtcNow });
        }
    }
}
