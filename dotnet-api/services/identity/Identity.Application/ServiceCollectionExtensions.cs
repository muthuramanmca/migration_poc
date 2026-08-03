using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddValidatorsFromAssemblyContaining<IdentityService>();

        return services;
    }
}
