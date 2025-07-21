using LiteDB;
namespace FoolMeGame.Modules.Data.Models;

public record RoomEntity
{
    [BsonId]
    public required long Id { get; init; }

    public required long CreatorId { get; init; }

    public required int MaxWords { get; init; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public int? JoinMessageId { get; init; }

    public bool IsRunning { get; init; }

    public string? TheWord { get; init; }

    public int RandomSeed { get; init; } = Random.Shared.Next();

    public int Round { get; init; } = 0;

    public List<PlayerEntity> Players { get; init; } = [];

    public List<RoomHistory> History { get; init; } = [];
}

public record PlayerEntity
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public required long UserId { get; init; }

    public required string UserName { get; init; }

    public DateTime JoinedAtUtc { get; init; } = DateTime.UtcNow;
}

public record RoomHistory(string Message, RoomHistoryType Type);

public enum RoomHistoryType
{
    Hint,
    Guess,
}
