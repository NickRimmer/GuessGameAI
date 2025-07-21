using FoolMeGame.Modules.Data.Models;
namespace FoolMeGame.Modules.Game.Models;

public record RoomGameDetails
{
    public required RoomEntity Room { get; init; }
    public required IReadOnlyCollection<PlayerEntity> Players { get; init; }
}
