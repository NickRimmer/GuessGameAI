using System.Reflection;
using FoolMeGame.Modules.Telegram.Services;
namespace FoolMeGame.Modules.Telegram;

public static class TelegramServicesExtension
{
    public static IServiceCollection AddTelegramCommands(this IServiceCollection services)
    {
        typeof(IMessageCommandHandler)
            .Assembly
            .GetTypes()
            .Where(x => x.GetCustomAttribute<CommandNamesAttribute>() != null && x.GetInterfaces().Contains(typeof(IMessageCommandHandler)))
            .ToList()
            .ForEach(x => services
                .AddScoped(typeof(IMessageCommandHandler), x)
                .AddScoped(x));

        typeof(IMessageCommandHandler)
            .Assembly
            .GetTypes()
            .Where(x => x.GetCustomAttribute<CommandNamesAttribute>() != null && x.GetInterfaces().Contains(typeof(ICallbackCommandHandler)))
            .ToList()
            .ForEach(x => services
                .AddScoped(typeof(ICallbackCommandHandler), x)
                .AddScoped(x));

        typeof(IMessageCommandHandler)
            .Assembly
            .GetTypes()
            .Where(x => x.GetInterfaces().Contains(typeof(IMessageTextHandler)))
            .ToList()
            .ForEach(x => services
                .AddScoped(typeof(IMessageTextHandler), x)
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
