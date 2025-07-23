using FoolMeGame.Shared.Levels;
using LiteDB;
namespace FoolMeGame.Shared.Data.Models;

public record ChatSystemSettings
{
    [BsonId]
    public required long ChatId { get; init; }

    public string CurrentLevelName { get; init; } = LevelActionAttribute.OnBaseLevel;
}
