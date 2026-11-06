using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DonationHub.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, bool development = false)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var store = services.GetRequiredService<AppUserStore>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var admin = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
        foreach (var role in new[] { "Admin", "CampaignManager", "Donor" })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole<Guid>(role)));
        await SeedUser(admin.Email, admin.Password, "System Administrator", "Admin");
        if (!development) return;

        var demo = services.GetRequiredService<IOptions<DevelopmentSeedOptions>>().Value;
        await SeedUser(demo.ManagerEmail, demo.ManagerPassword, "Demo Campaign Manager", "CampaignManager");
        await SeedUser(demo.DonorEmail, demo.DonorPassword, "Demo Donor", "Donor");
        var repository = services.GetRequiredService<IGenericRepository<Category>>();
        foreach (var (ar, en) in new[] { ("الصحة", "Health"), ("التعليم", "Education"), ("الإغاثة", "Relief"), ("الأيتام", "Orphans"), ("البيئة", "Environment") })
        {
            if (!await db.Categories.AsNoTracking().AnyAsync(c => c.NameAr == ar || c.NameEn == en))
                await repository.AddAsync(Category.Create(ar, en, ar, en));
        }
        await unitOfWork.SaveChangesAsync();

        async Task SeedUser(string email, string password, string name, string role)
        {
            email = email.Trim();
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null)
            {
                if (!await users.IsInRoleAsync(existing, role))
                    throw new InvalidOperationException("A seed email belongs to an existing account with a different role.");
                return;
            }
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(), UserName = email, Email = email, EmailConfirmed = true,
                FullName = name, PreferredLanguage = "en", IsActive = true, CreatedAt = now
            };
            var autoSave = store.AutoSaveChanges;
            store.AutoSaveChanges = false;
            try
            {
                Ensure(await users.CreateAsync(user, password));
                Ensure(await users.AddToRoleAsync(user, role));
                await unitOfWork.SaveChangesAsync();
            }
            finally { store.AutoSaveChanges = autoSave; }
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
