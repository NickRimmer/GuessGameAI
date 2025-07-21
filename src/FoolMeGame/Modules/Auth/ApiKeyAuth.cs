using AspNetCore.Authentication.ApiKey;
using Microsoft.Extensions.Options;
namespace FoolMeGame.Modules.Auth;

public class ApiKeyAuth : IApiKeyProvider
{
    private readonly ApiKeySettings _settings;
    public ApiKeyAuth(IOptions<ApiKeySettings> settings, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings.Value;
    }

    public Task<IApiKey?> ProvideAsync(string key)
    {
        IApiKey? apiKey = _settings.ApiKeys.FirstOrDefault(x => x.Key == key);
        return Task.FromResult(apiKey);
    }
}
