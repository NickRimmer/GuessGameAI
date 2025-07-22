using LiteDB;
namespace GuessTheWord.Business.Data.Models;

public record RoomEntity
{
    [BsonId]
    public required long Id { get; init; }

    public required long CreatorId { get; init; }

    public required int MaxWords { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public int? OnboardingMessageId { get; init; }

    public bool IsRunning { get; init; }

    public IReadOnlyCollection<WordEntity>? Phrase { get; init; }

    public int RandomSeed { get; init; } = Random.Shared.Next();

    public int Round { get; init; }

    public List<RoomPlayerEntity> Players { get; init; } = [];

    public List<RoomHistory> History { get; init; } = [];
}

public record RoomPlayerEntity
{
    [BsonId]
    public required long Id { get; init; }

    public required string UserName { get; init; }

    public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;
}

public record RoomHistory(string Message, RoomHistoryType Type);

public record WordEntity(string Word, bool IsSecret = true);

public enum RoomHistoryType
{
    Hint,
    Guess,
}
