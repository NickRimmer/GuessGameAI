using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
namespace GuessTheWord.Business.Game.Modes.SingleWord;

public class SingleWordCheckService : IHintCheckService
{
    private readonly ILogger<SingleWordCheckService> _logger;
    private readonly ChatClient _ai;

    public SingleWordCheckService(Func<ChatClient> aiProvider, ILogger<SingleWordCheckService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ai = aiProvider?.Invoke() ?? throw new ArgumentNullException(nameof(aiProvider));
    }

    public async Task<ValidationResult> ValidateHintAsync(string hint, string secretPhrase)
    {
        hint = hint.Trim().ToLowerInvariant();
        secretPhrase = secretPhrase.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(hint) || string.IsNullOrWhiteSpace(secretPhrase))
            return ValidationResult.Invalid;

        if (string.Equals(hint, secretPhrase)) return ValidationResult.Invalid;

        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI assistant that validates player input hint for a word guessing game.",
                "Your task is to check whether a given hint is invalid because it contains the target word or any form, variation, or derivative of it.",
                "The hint and the secret word will be provided. The hint must NOT:",
                "- Be equal to the secret word.",
                "- Contain the secret word as a substring.",
                "- Be a plural, verb form, adjective form, or any other derived form of the target word.",
                "- Translation of the target word in any language is NOT allowed.",
                "- Use synonyms are allowed, but the exact word or its grammatical variants are NOT allowed.",
                "",
                "Respond only with either: \"valid\" or \"invalid\".",
                "Do not explain your answer.",
            }.Join("\n")),

            new UserChatMessage($"Is this hint `{hint}` valid for the secret word `{secretPhrase}`?"),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return new ValidationResult(result.Replace("\"", string.Empty).Trim().Equals("valid", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<GuessValidationWord>> ValidateGuessAsync(string guess, IReadOnlyCollection<WordEntity> secretPhrase)
    {
        guess = guess.Trim().ToLowerInvariant();
        var secretWord = secretPhrase.Select(x => x.Word).Join(" ").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(guess) || string.IsNullOrWhiteSpace(secretWord))
            return [new GuessValidationWord { Word = secretWord, IsCorrect = false }];

        // direct match
        if (string.Equals(guess, secretWord, StringComparison.OrdinalIgnoreCase))
            return [new GuessValidationWord { Word = secretWord, IsCorrect = true }];

        // ai much
        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI assistant validating if provided guess and target words are same or not.",
                "",
                "The guess is considered correct if one of conditions is valid:",
                "- It exactly matches the target word.",
                "- It is a singular/plural variation.",
                "- It is the same word in a different grammatical form (e.g., past tense, adjective, etc.).",
                "- It is a different form of the word in the same language (e.g., verb to noun, etc.).",
                "- It is the same language guess as the target word. Guess MUST BE same language as target word.",
                "",
                "Respond only with: \"correct\" or \"incorrect\".",
                "Do not explain your answer.",
            }.Join("\n")),

            new UserChatMessage($"Target word is `{secretWord}`. Is the guess `{guess}` correct?"),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return [
            new GuessValidationWord {
                Word = secretWord,
                IsCorrect = result.Replace("\"", string.Empty).Trim().Equals("correct", StringComparison.OrdinalIgnoreCase),
            },
        ];
    }

    public async Task<string> GuessTheWord(IReadOnlyCollection<RoomHistory> history, string language, IReadOnlyCollection<WordEntity> phrase)
    {
        var guess = await GetBestGuessAsync(history, language);
        if (!history.Any(x => x.Message.Equals(guess, StringComparison.OrdinalIgnoreCase))) return guess;

        // try again if repeated
        _logger.LogWarning("Guess `{Guess}` was already made, trying to get a new guess", guess);
        history = history
            .Concat([
                new RoomHistory(guess, RoomHistoryType.Guess),
                new RoomHistory("Guess was already made, avoid repeating. What will be your guess for last hints?", RoomHistoryType.Hint),
            ])
            .ToList();

        return await GetBestGuessAsync(history, language);
    }

    private async Task<string> GetBestGuessAsync(IReadOnlyCollection<RoomHistory> history, string language)
    {
        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI playing a word guessing game.",
                "You will receive a list of hints from players, all pointing to the same secret word, you need to guess the secret work based on hints.",
                "",
                "Important rules:",
                "- You must respond with only a single word — no punctuation, no explanation, no reasoning.",
                "- You MUST NOT repeat any guesses that have already been made.",
                "- You MUST NOT repeat hints as guesses, the answer is never among them.",
                "- If you're unsure, make your best educated guess based on the hints and previous guesses.",
                "",
                "The hints may be metaphorical, descriptive, or associative. Hints could be as additional to previous hints, or completely new.",
                "Pay attention to grammatical cues in the hints — plural or singular, masculine or feminine forms — they can help you infer the correct form of the secret word.",
                $"Your response must be only one word you didn't answer yet. Use {language} language for response.",
            }.Join("\n")),

            ..history.Select((x, i) =>
                x.Type == RoomHistoryType.Hint
                    ? (ChatMessage) new UserChatMessage(i == 0 ? $"My first hint is: `{x.Message}`" : $"No, this is incorrect, one more hint: `{x.Message}`")
                    : new AssistantChatMessage(x.Message)),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var guess = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return guess.Trim();
    }
}
