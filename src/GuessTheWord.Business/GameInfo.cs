using FoolMeGame.Shared.Settings;
using GuessTheWord.Business.Data;
using GuessTheWord.Business.Game.Interfaces;
using GuessTheWord.Business.Game.Modes.TwoWords;
using GuessTheWord.Business.Settings;
using Microsoft.Extensions.DependencyInjection;
namespace GuessTheWord.Business;

public static class GameInfo
{
    public const long GlobalAdminUserId = 214820691; // Mandatory for each game to have a global admin user ID

    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services
            // .AddSingleton<IWordsProvider, SingleWordProvider>()
            .AddSingleton<IWordsProvider, TwoWordsProvider>()
            .AddSingleton<DbStorageGame>()
            .AddSingleton<ISettingsManager, GameSettingsManager>(); // Mandatory for each game to implement its own settings manager

        services
            // .AddScoped<IHintCheckService, SingleWordCheckService>()
            .AddScoped<IHintCheckService, TwoWordsCheckService>()
            .AddScoped<Game.GameInfo>();

        return services;
    }
}
