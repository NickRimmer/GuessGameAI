using System.Text.Json;
using GuessTheWord.Business.Data.Models;
using GuessTheWord.Business.Game.Interfaces;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
namespace GuessTheWord.Business.Game.Modes.TwoWords;

public class TwoWordsCheckService : IHintCheckService
{
    private static readonly JsonSerializerOptions _jsonSettings = new () {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<TwoWordsCheckService> _logger;
    private readonly ChatClient _ai;

    public TwoWordsCheckService(Func<ChatClient> aiProvider, ILogger<TwoWordsCheckService> logger)
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
                "You are an AI assistant that validates a player's hint in a word guessing game.",
                "",
                "The target is a two-word secret phrase: an adjective and a noun.",
                "Your task is to check whether user hint contains either of the target secret words or any of their grammatical forms.",
                "",
                "The hint is considered **invalid** if it contains:",
                "- The exact adjective or noun from the secret target phrase.",
                "- Any grammatical form of words from secret phrase (e.g., plural, gender).",
                "",
                "Otherwise, the hint is **valid**.",
                "- Antonyms, synonyms of the target words are allowed.",
            }.Join("\n")),

            new UserChatMessage($"Secret phrase `{secretPhrase}`. Is this hint `{hint}` valid?"),
        ];

        var response = await _ai.CompleteChatAsync(messages, new ChatCompletionOptions {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(jsonSchemaFormatName: "HintValidation", jsonSchema: BinaryData.FromBytes(
                """
                    {
                      "type": "object",
                      "properties": {
                        "IsValid": {
                          "type": "boolean",
                          "description": "The result of the validation."
                        },
                        "Reason": {
                          "type": "string",
                          "description": "A brief reason for the validation result. Use same language as phrase for validation. Provide as empty string, when validation result is valid."
                        }
                      },
                      "required": ["IsValid", "Reason"],
                      "additionalProperties": false
                    }
                    """u8.ToArray())),
        });
        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

        try
        {
            var validationResult = JsonSerializer.Deserialize<HintValidationJson>(result, _jsonSettings) ?? new HintValidationJson { IsValid = false };
            return new ValidationResult(validationResult.IsValid, validationResult.Reason);
        }
        catch
        {
            return ValidationResult.Invalid;
        }
    }

    public async Task<IReadOnlyCollection<GuessValidationWord>> ValidateGuessAsync(string guess, IReadOnlyCollection<WordEntity> secretPhrase)
    {
        guess = guess.Trim().ToLowerInvariant();

        var targetWords = secretPhrase
            .Select(x => new GuessValidationWord {
                Word = x.Word,
                IsCorrect = !x.IsSecret,
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(guess) || secretPhrase is not { Count: > 0 })
            return targetWords;

        // direct match
        var phrase = secretPhrase.Select(x => x.Word).Join(" ").Trim();
        if (string.Equals(guess, phrase, StringComparison.OrdinalIgnoreCase))
            return targetWords.Select(x => x with { IsCorrect = true }).ToList();

        // ai much
        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI assistant validating whether a guessed phrase correctly matches a target phrase in a word guessing game.",
                "",
                "Both the guess and the target consist of exactly two words: an adjective and a noun.",
                "",
                "The guess is considered correct if all of the following conditions are met:",
                "- Both the adjective and noun match the target phrase exactly, or are valid grammatical forms or variants (e.g., gender, number, case, diminutive).",
                "- The noun may differ in singular/plural form, or in gender if appropriate.",
                "- The adjective may differ in gender, case, or number as long as it agrees with the noun.",
                "- Minor spelling differences (e.g., UK/Russian orthographic variants) are acceptable.",
                "- The guess must be in the same language as the target phrase.",
                "- The order of words in the phrase does not matter — adjective and noun can appear in any order.",
                "",
                "Synonyms or alternative meanings are NOT considered correct — only true morphological variations of the original phrase.",
                string.Empty,
                "Your output must be a JSON with `words` array with objects for each word of guess phrase.",
                "Each object must contain two fields: 'word' (the guessed word) and 'isCorrect' (true or false). Set it as correct if the word existing in the target phrase.",

            }.Join("\n")),

            new UserChatMessage($"Target phrase is `{phrase}`. Is the guess `{guess}` correct?"),
        ];

        var response = await _ai.CompleteChatAsync(messages, new ChatCompletionOptions {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(jsonSchemaFormatName: "GuessValidation", jsonSchema: BinaryData.FromBytes(
                """
                    {
                      "type": "object",
                      "properties": {
                        "words": {
                            "type": "array",
                            "items" : {
                                "type": "object",
                                "properties": {
                                    "word": {
                                        "type": "string",
                                        "description": "The word in the guess, either adjective or noun."
                                    },
                                    "isCorrect": {
                                        "type": "boolean",
                                        "description": "Indicates whether the word is correct in the context of the target phrase."
                                    }
                                }
                            }
                        }
                      },
                      "required": ["validationResult", "Reason"],
                      "additionalProperties": false
                    }
                    """u8.ToArray())),
        });

        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        var validationResult = JsonSerializer.Deserialize<GuessValidationJson>(result, _jsonSettings);
        return targetWords
            .Select(x => x with {
                IsCorrect = x.IsCorrect || validationResult?.Words.FirstOrDefault(y => y.Word.Equals(x.Word, StringComparison.OrdinalIgnoreCase))?.IsCorrect == true,
            })
            .ToList();
    }

    public async Task<string> GuessTheWord(IReadOnlyCollection<RoomHistory> history, string language, IReadOnlyCollection<WordEntity> phrase)
    {
        var guess = await GetBestGuessAsync(history, language, phrase);
        if (!history.Any(x => x.Message.Equals(guess, StringComparison.OrdinalIgnoreCase))) return guess;

        // try again if repeated
        _logger.LogWarning("Guess `{Guess}` was already made, trying to get a new phrase", guess);
        history = history
            .Concat([
                new RoomHistory(guess, RoomHistoryType.Guess),
                new RoomHistory("Guess was already made, avoid repeating.", RoomHistoryType.Hint),
            ])
            .ToList();

        return await GetBestGuessAsync(history, language, phrase);
    }

    private async Task<string> GetBestGuessAsync(IReadOnlyCollection<RoomHistory> history, string language, IReadOnlyCollection<WordEntity> phrase)
    {
        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI playing a word guessing game.",
                "You will receive a list of hints from players, all pointing to the same secret phrase.",
                "The secret phrase always consists of multiple words — typically an adjective and a noun.",
                "",
                "You will also receive a masked version of the phrase, where some words are shown as `_` to indicate which parts are still hidden.",
                "Use the masked phrase to guide your guess: only guess the parts that are still hidden.",
                "",
                "Important rules:",
                "- Your response must match the structure of the masked phrase: the same number of words, using known words as-is and replacing `_` with your guesses.",
                "- Do not use punctuation, explanation, or reasoning.",
                "- Do not repeat any of your previous guesses.",
                "- Do not guess any of the hints directly — the secret phrase is never among the hints.",
                "- Do not use words from the hints as guesses, they are not part of the secret phrase.",
                "- Make your best educated guess based on the hints, previous guesses, and what is already revealed.",
                "",
                "Hints may be metaphorical, descriptive, or associative.",
                "Some hints may point to a specific word, while others may suggest changing or refining just one part of the phrase.",
                "Pay attention to language, gender, number, and form — these may help you infer the correct words.",
                "",
                $"Use {language} for your response. Return only the guessed phrase: no punctuation, no explanation — just words.",
            }.Join("\n")),

            ..history.Select(x =>
                x.Type == RoomHistoryType.Hint
                    ? (ChatMessage) new UserChatMessage(x.Message)
                    : new AssistantChatMessage(x.Message)),

            new UserChatMessage($"Masked secret phrase: `{phrase.Select(x => x.IsSecret ? "_" : x.Word).Join(" ")}`. Please guess what is this secret phrase, replace `_` with words you think should be here."),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var guess = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return guess.Trim();
    }

    private record HintValidationJson
    {
        public bool IsValid { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    private record GuessValidationJson
    {
        public required List<GuessValidationWordJson> Words { get; init; }

        public record GuessValidationWordJson
        {
            public string Word { get; init; } = string.Empty;
            public bool IsCorrect { get; [UsedImplicitly] init; }
        }
    }
}
