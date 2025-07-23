using LiteDB;
namespace GuessTheWord.Business.Data.Models;

public record ChatSettingsEntity
{
    [BsonId]
    public long Id { get; init; }

    public string Language { get; init; } = "Russian";

    public int MaxWordsHint { get; init; } = 10;

    public int MinPlayersToStart { get; init; } = 2;
    public int MaxPlayersToPlay { get; init; } = 5;
    public int MaxTurns { get; init; } = 15;

    public long AdminId { get; init; } = GameInfo.GlobalAdminUserId;
}
