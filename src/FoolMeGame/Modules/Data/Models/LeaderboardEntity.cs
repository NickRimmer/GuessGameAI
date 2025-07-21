using LiteDB;
namespace FoolMeGame.Modules.Data.Models;

public record LeaderboardEntity
{
    [BsonId]
    public required long UserId { get; init; }

    public required string UserName { get; init; }

    public int Score { get; init; }

    public int PlayedGames { get; init; }
}
