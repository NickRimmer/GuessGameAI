using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace FoolMeGame.Shared.Common;

public static class OptionsExtension
{
    public static IServiceCollection AddOptions<T>(this IServiceCollection services, IConfiguration configuration) where T : class => services
        .Configure<T>(configuration.GetSection(typeof(T).Name));
}
