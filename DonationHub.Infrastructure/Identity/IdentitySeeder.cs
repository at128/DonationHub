
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DonationHub.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        string[] roles =
        [
            "Admin",
            "CampaignManager",
            "Donor"
        ];

        foreach (var role in roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(
                new IdentityRole<Guid>(role));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Failed to seed role '{role}': {errors}");
            }
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var adminSeedOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<AdminSeedOptions>>()
            .Value;
        
        var adminUser = await userManager.FindByEmailAsync(adminSeedOptions.Email);

        if(adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                Id=Guid.CreateVersion7(),
                UserName = adminSeedOptions.Email,
                Email = adminSeedOptions.Email,
                EmailConfirmed = true,
                FullName = "System Administrator",
                PreferredLanguage = "en",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, adminSeedOptions.Password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            result = await userManager.AddToRoleAsync(adminUser,"Admin");

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

        }else if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            throw new InvalidOperationException(
                "Admin seed email already belongs to a non-admin user.");
        }
    }
}
