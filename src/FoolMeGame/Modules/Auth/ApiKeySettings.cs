namespace FoolMeGame.Modules.Auth;

public sealed record ApiKeySettings
{
    public IReadOnlyCollection<ApiKey> ApiKeys { get; init; } = [];
}
