using System.Reflection;
using FoolMeGame.Modules.Telegram.Services;
using FoolMeGame.Shared.Levels;
using GuessTheWord.Business;
namespace FoolMeGame.Modules.Telegram;

public static class TelegramServicesExtension
{
    public static IServiceCollection AddTelegramCommands(this IServiceCollection services)
    {
        typeof(Program)
            .Assembly
            .GetTypes()
            .Where(x => x.GetCustomAttributes<LevelActionAttribute>().Any() && x.GetInterfaces().Contains(typeof(ILevelAction)))
            .ToList()
            .ForEach(x => services
                .AddScoped(typeof(ILevelAction), x)
                .AddScoped(x));

        typeof(GameInfo)
            .Assembly
            .GetTypes()
            .Where(x => x.GetCustomAttributes<LevelActionAttribute>().Any() && x.GetInterfaces().Contains(typeof(ILevelAction)))
            .ToList()
            .ForEach(x => services
                .AddScoped(typeof(ILevelAction), x)
                .AddScoped(x));

        return services;
    }

    public static WebApplication UseTelegramWebhook(this WebApplication app)
    {
        app.MapPost("/api/tg/webhook", (WebhookService service, HttpRequest request) => service.OnUpdatesReceivedAsync(request))
            .RequireAuthorization(options => options.RequireRole("TelegramBot"));

        // var bot = app.Services.GetRequiredService<TelegramBotClient>();
        // bot.SetWebhook("", allowedUpdates: []);

        return app;
    }
}
