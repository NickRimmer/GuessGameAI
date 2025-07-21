using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNetCore.Authentication.ApiKey;
namespace FoolMeGame.Modules.Auth;

public record ApiKey : IApiKey
{
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public required string Key { get; init; }
    public required string OwnerName { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<Claim> Claims => Roles.Select(x => new Claim(ClaimTypes.Role, x)).ToList();
}
