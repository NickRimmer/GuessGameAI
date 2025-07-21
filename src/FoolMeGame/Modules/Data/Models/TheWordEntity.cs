using LiteDB;
namespace FoolMeGame.Modules.Data.Models;

public record TheWordEntity
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public required string Word { get; init; }

    public int ShowedCount { get; init; } = 1;

    public int GuessedCount { get; init; }

    public int CanceledCount { get; init; }
}
