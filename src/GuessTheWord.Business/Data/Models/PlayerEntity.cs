using LiteDB;

namespace GuessTheWord.Business.Data.Models;

public record PlayerEntity
{
    [BsonId]
    public required long UserId { get; init; }

    public required string UserName { get; init; }

    public int Score { get; init; }

    public int PlayedGames { get; init; }

    public DateTime LastGameAtUtc { get; set; }
}
