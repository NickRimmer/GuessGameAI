using System.Diagnostics.CodeAnalysis;
using FoolMeGame.Modules.Agents;
using FoolMeGame.Modules.Data;
using FoolMeGame.Modules.Data.Models;
namespace FoolMeGame.Modules.Game;

public record RoomGameService
{
    private readonly DbStorage _db;
    private readonly WordsGeneratorAgent _wordsAgent;

    public RoomGameService(DbStorage db, WordsGeneratorAgent wordsAgent)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _wordsAgent = wordsAgent ?? throw new ArgumentNullException(nameof(wordsAgent));
    }

    public bool Open(long chatId, long userId, string userName)
    {
        if (_db.Rooms.FindById(chatId) != null)
            return false;

        var room = new RoomEntity {
            Id = chatId,
            CreatorId = userId,
            MaxWords = 0, // will be changed on room start
        };

        var player = new PlayerEntity {
            UserId = userId,
            UserName = userName,
        };

        room.Players.Add(player);

        _db.Rooms.Insert(room);
        return true;
    }

    public bool TryGetRoomForChat(long chatId, [NotNullWhen(true)] out RoomEntity? room)
    {
        room = _db.Rooms.FindById(chatId);
        return room != null;
    }

    public bool SetJoinMessage(long chatId, int messageId)
    {
        var room = _db.Rooms.FindById(chatId);
        if (room == null)
            return false;

        room = room with { JoinMessageId = messageId };
        _db.Rooms.Update(room);

        return true;
    }

    public bool TryClose(long chatId, [NotNullWhen(true)] out RoomEntity? room)
    {
        room = _db.Rooms.FindById(chatId);

        if (room == null)
            return false;

        if (room.IsRunning)
            _wordsAgent.UpdateStats(room.TheWord!, x => x with { CanceledCount = x.CanceledCount + 1 });

        _db.Rooms.Delete(chatId);
        return true;
    }

    public bool TryJoin(long chatId, long userId, string userName)
    {
        var room = _db.Rooms.FindById(chatId);

        if (room == null)
            return false;

        if (room.Players.Any(x => x.UserId == userId))
            return false;

        _db.Rooms.Update(room with {
            Players = room.Players.Append(new PlayerEntity {
                UserId = userId,
                UserName = userName,
            }).ToList(),
        });

        return true;
    }
}
