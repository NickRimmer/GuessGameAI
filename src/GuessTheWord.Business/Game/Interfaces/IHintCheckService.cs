using GuessTheWord.Business.Data.Models;
namespace GuessTheWord.Business.Game.Interfaces;

public interface IHintCheckService
{
    Task<ValidationResult> ValidateHintAsync(string hint, string secretPhrase);
    Task<IReadOnlyCollection<GuessValidationWord>> ValidateGuessAsync(string guess, IReadOnlyCollection<WordEntity> secretPhrase);
    Task<string> GuessTheWord(IReadOnlyCollection<RoomHistory> history, string language, IReadOnlyCollection<WordEntity> phrase);
}

public record ValidationResult(bool IsValid, string Reason = "")
{
    public static ValidationResult Invalid { get; } = new (false);
}

public record GuessValidationWord
{
    public required string Word { get; init; }
    public required bool IsCorrect { get; init; }
}