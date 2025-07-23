using FoolMeGame.Shared.Data.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
namespace FoolMeGame.Shared.Data;

public class DbStorage : LiteDatabase
{
    public DbStorage(ILogger<DbStorage> logger) : base(GetPathToDb(logger), new BsonMapper())
    {
    }

    public ILiteCollection<ChatSystemSettings> ChatSystemSettings => GetCollection<ChatSystemSettings>();

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
