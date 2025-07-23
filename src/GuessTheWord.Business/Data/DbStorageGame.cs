using FoolMeGame.Shared.Data;
using GuessTheWord.Business.Data.Models;
using LiteDB;
namespace GuessTheWord.Business.Data;

public class DbStorageGame
{
    private readonly DbStorage _db;

    public DbStorageGame(DbStorage db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public ILiteCollection<ChatSettingsEntity> ChatSettings => _db.GetCollection<ChatSettingsEntity>();
    public ILiteCollection<PlayerEntity> Players => _db.GetCollection<PlayerEntity>();
    public ILiteCollection<RoomEntity> Rooms => _db.GetCollection<RoomEntity>();
    public ILiteCollection<GameStateEntity> GameStates => _db.GetCollection<GameStateEntity>();
}
