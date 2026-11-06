using Microsoft.Extensions.DependencyInjection;
using DonationHub.Application.Features.Categories;
using DonationHub.Application.Features.Auth;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DonationHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CategoryService>();
        services.AddScoped<AuthService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
