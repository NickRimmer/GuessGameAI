using FoolMeGame.Modules.Agents.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace FoolMeGame.Modules.Agents;

public static class AgentExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services
            .AddSingleton(BuildChatClient)
            .AddSingleton<AiAgent>()
            .AddSingleton<WordsGeneratorAgent>();

        return services;
    }

    private static Func<ChatClient> BuildChatClient(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IOptions<OpenAISettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new ArgumentException("OpenAI API key is not configured.");

        return () => new ChatClient(model: settings.Model, apiKey: settings.ApiKey);
    }
}
