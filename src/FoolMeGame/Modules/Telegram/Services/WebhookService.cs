using System.Text.Json;
using FoolMeGame.Shared.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
namespace FoolMeGame.Modules.Telegram.Services;

public class WebhookService
{
    private readonly IServiceProvider _services;

    public WebhookService(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task<IResult> OnUpdatesReceivedAsync(HttpRequest request)
    {
        var logger = _services.GetRequiredService<ILogger<WebhookService>>();
        try
        {
            // read content body as string
            using var sr = new StreamReader(request.Body);
            var body = await sr.ReadToEndAsync();
            var update = JsonSerializer.Deserialize<Update>(body, JsonBotAPI.Options);

            var telegramHelper = _services.GetRequiredService<TelegramHelper>();
            if (!telegramHelper.SetContext(update))
            {
                logger.LogError("Cannot deserialize update message");
                return Results.Ok("Cannot deserialize update message");
            }

            if (telegramHelper.HasMessage())
                return await _services.GetRequiredService<MessageReceivedService>().HandleAsync();

            if (telegramHelper.HasCallback())
                return await _services.GetRequiredService<QueryCallbackService>().HandleAsync();

            logger.LogWarning("Unexpected update type");
            return Results.Ok();
        }
        catch (Exception ex)
        {
            try
            {
                await _services.GetRequiredService<TelegramHelper>().SendMessageBackAsync("Oops, something went wrong 😕");
            }
            catch
            {
                // we tried...
            }
            logger.LogError(ex, "Error while processing update");
            return Results.Ok(ex.Message);
        }
    }
}
