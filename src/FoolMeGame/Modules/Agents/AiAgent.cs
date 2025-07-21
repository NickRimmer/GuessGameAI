using FoolMeGame.Modules.Data.Models;
using OpenAI.Chat;
namespace FoolMeGame.Modules.Agents;

public class AiAgent
{
    private readonly ILogger<AiAgent> _logger;
    private readonly ChatClient _ai;

    public AiAgent(Func<ChatClient> aiProvider, ILogger<AiAgent> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ai = aiProvider.Invoke();
    }

    // public async Task<string> GuessWordAsync(IReadOnlyCollection<string> history, IReadOnlyCollection<string> previousGuesses, string language)
    public async Task<string> GuessWordAsync(List<RoomHistory> roomHistory, string language)
    {
        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI playing a word guessing game.",
                "You will receive a list of one or two-word hints from players, all pointing to the same secret word.",
                "The secret word is always a noun.",
                "",
                "Important rules:",
                "- You must respond with only a single word — no punctuation, no explanation, no reasoning.",
                "- Never repeat a guess that was already tried. Only new guesses are allowed or similar but another form.",
                "- Never return any of the hints as your guess — the answer is never among them.",
                "- If you're unsure, make your best educated guess based on the hints and previous guesses.",
                "",
                "The hints may be metaphorical, descriptive, or associative.",
                "Pay attention to grammatical cues in the hints — plural or singular, masculine or feminine forms — they may help you infer the correct form of the secret word.",
            }.Join("\n")),

            ..roomHistory.Select(x =>
                x.Type == RoomHistoryType.Hint
                    ? (ChatMessage) new UserChatMessage($"Hint is: {x.Message}")
                    : new AssistantChatMessage(x.Message)),

            new UserChatMessage($"Your response must be only one word you didn't answer yet. Use {language} language for response."),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var guess = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return guess.Trim();
    }

    public async Task<bool> ValidateHintAsync(string hint, string secretWord)
    {
        hint = hint.Trim().ToLowerInvariant();
        secretWord = secretWord.Trim().ToLowerInvariant();

        if (string.Equals(hint, secretWord)) return false;

        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI assistant that validates player input for a word guessing game.",
                "Your task is to check whether a given hint is invalid because it contains the target word or any form, variation, or derivative of it.",
                "The hint and the secret word will be provided. The hint must NOT:",
                "- Be equal to the secret word.",
                "- Contain the secret word as a substring.",
                "- Be a plural, verb form, adjective form, or any other derived form of the target word.",
                "- Use synonyms or translations are allowed, but the exact word or its grammatical variants are NOT allowed.",
                "",
                "Respond only with either: \"valid\" or \"invalid\".",
                "Do not explain your answer.",
            }.Join("\n")),

            new UserChatMessage(new[] {
                $"Target word: {secretWord}",
                $"Player hint: {hint}",
            }.Join("\n")),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return result.Replace("\"", string.Empty).Trim().Equals("valid", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsGuessCorrectAsync(string guess, string secretWord)
    {
        guess = guess.Trim().ToLowerInvariant();
        secretWord = secretWord.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(guess))
        {
            _logger.LogWarning("Received empty guess for secret word '{SecretWord}'", secretWord);
            return false;
        }

        // dict comparing
        var isEqual = string.Equals(guess, secretWord);
        if (isEqual) return true;

        ChatMessage[] messages = [
            new SystemChatMessage(new[] {
                "You are an AI assistant validating if a guessed word correctly matches a target word in a word guessing game.",
                "",
                "The guess is considered correct if:",
                "- It exactly matches the target word.",
                "- It is a singular/plural variation.",
                "- It is the same word in a different grammatical form (e.g., past tense, adjective, etc.).",
                "- It is a recognized variant spelling (e.g., US vs UK).",
                "",
                "The guess is NOT correct if it is a synonym, translation, or related concept — only true morphological variations count.",
                "",
                "Respond only with: \"correct\" or \"incorrect\".",
                "Do not explain your answer.",
            }.Join("\n")),

            new UserChatMessage(new[] {
                $"Target word: {secretWord}",
                $"LLM guess: {guess}",
            }.Join("\n")),
        ];

        var response = await _ai.CompleteChatAsync(messages);
        var result = response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        return result.Replace("\"", string.Empty).Trim().Equals("correct", StringComparison.OrdinalIgnoreCase);
    }
}
