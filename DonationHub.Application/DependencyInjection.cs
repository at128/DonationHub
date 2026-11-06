using Microsoft.Extensions.DependencyInjection;
using DonationHub.Application.Features.Categories;

namespace DonationHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CategoryService>();
        return services;
    }
}
