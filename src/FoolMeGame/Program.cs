using AspNetCore.Authentication.ApiKey;
using FoolMeGame.Modules.Ai;
using FoolMeGame.Modules.Auth;
using FoolMeGame.Modules.Telegram;
using FoolMeGame.Modules.Telegram.Services;
using FoolMeGame.Shared.Data;
using FoolMeGame.Shared.Levels;
using FoolMeGame.Shared.Telegram;
using GuessTheWord.Business;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using Telegram.Bot;
using WebhookService = FoolMeGame.Modules.Telegram.Services.WebhookService;
var builder = WebApplication.CreateBuilder(args);

// settings
 builder.Configuration
    .AddJsonFile("appsettings.local.json5", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings2.local.json5", optional: true, reloadOnChange: true);

builder.Services
    .AddOptions<ApiKeySettings>(builder.Configuration)
    .AddOptions<TelegramSettings>(builder.Configuration)
    .AddOptions<OpenAISettings>(builder.Configuration);

// logging
builder.Logging.ClearProviders();
builder.Logging.AddNLog(builder.Configuration);

// services
builder.Services
    // scoped
    .AddScoped<TelegramHelper>()
    .AddScoped<WebhookService>()
    .AddScoped<QueryCallbackService>()
    .AddScoped<MessageReceivedService>()
    .AddScoped<LevelsManager>()
    .AddScoped<LevelsProvider>()

    // singletons
    .AddSingleton<DbStorage>()

    // services by areas
    .AddTelegramCommands()
    .AddGameServices()
    .AddAiServices();

builder.Services
    .AddHttpClient("telegram")
    .AddTypedClient<ITelegramBotClient>((client, services) => new TelegramBotClient(services.GetRequiredService<IOptions<TelegramSettings>>().Value.Token, client));

// auth
builder.Services
    .AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
    .AddApiKeyInHeader<ApiKeyAuth>(options =>
    {
        options.Realm = "Telegram";
        options.KeyName = "X-Telegram-Bot-Api-Secret-Token";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseTelegramWebhook();

app.Services.GetRequiredService<ILogger<Program>>().LogInformation("Starting FoolMeGame...");
app.Run();

NLog.LogManager.Shutdown();
