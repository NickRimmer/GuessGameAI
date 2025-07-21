using FoolMeGame.Modules.Data.Models;
using LiteDB;
namespace FoolMeGame.Modules.Data;

public class DbStorage : LiteDatabase
{
    public DbStorage(ILogger<DbStorage> logger) : base(GetPathToDb(logger), new BsonMapper())
    {
    }

    public ILiteCollection<RoomEntity> Rooms => GetCollection<RoomEntity>();
    public ILiteCollection<LeaderboardEntity> Leaderboard => GetCollection<LeaderboardEntity>();
    public ILiteCollection<TheWordEntity> Words => GetCollection<TheWordEntity>();
    public ILiteCollection<ChatSettingsEntity> Chats => GetCollection<ChatSettingsEntity>();

    private static string GetPathToDb(ILogger<DbStorage> logger)
    {
        var dataFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(dataFolderPath);

        var dbPath = Path.Combine(dataFolderPath, "server.db");
        logger.LogInformation("Db path: {DbPath}", dbPath);
        #if DEBUG
        return $"Filename={dbPath}; Connection=shared";
        #else
        return $"Filename={dbPath}; Connection=direct";
        #endif
    }
}
