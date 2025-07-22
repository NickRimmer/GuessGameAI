using LiteDB;
namespace GuessTheWord.Business.Data.Models;

public record GameStateEntity
{
    [BsonId]
    public required string Key { get; init; }

    public string Value { get; init; } = string.Empty;
}
