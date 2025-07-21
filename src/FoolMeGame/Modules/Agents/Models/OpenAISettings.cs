namespace FoolMeGame.Modules.Agents.Models;

public record OpenAISettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4o-mini";
}
