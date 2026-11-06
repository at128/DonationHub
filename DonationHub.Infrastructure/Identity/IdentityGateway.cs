using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Features.Auth;
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Identity;

public sealed class IdentityGateway(UserManager<ApplicationUser> users, AppUserStore store,
    IUnitOfWork unitOfWork, AppDbContext db) : IIdentityGateway
{
    public async Task<AuthResult<AccountResponse>> CreateAsync(RegisterRequest request, string role, DateTime nowUtc, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null) return Duplicate();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(), UserName = email, Email = email, FullName = request.FullName.Trim(),
            PreferredLanguage = request.PreferredLanguage, IsActive = true, CreatedAt = nowUtc
        };
        // Identity validates/hashes; its store stages both the user and role link.
        // A single UoW save persists them atomically.
        var autoSave = store.AutoSaveChanges;
        store.AutoSaveChanges = false;
        try
        {
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                    ? Duplicate() : Invalid(string.Join(" ", result.Errors.Select(e => e.Description)));
            result = await users.AddToRoleAsync(user, role);
            if (!result.Succeeded) throw new InvalidOperationException("Could not assign the account role.");
            await unitOfWork.SaveChangesAsync(ct);
            return AuthResult<AccountResponse>.Success(new(user.Id, email, user.FullName));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Duplicate();
        }
        finally { store.AutoSaveChanges = autoSave; }
    }

    public async Task<AccountProfile?> VerifyCredentialsAsync(string email, string password)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.CheckPasswordAsync(user, password)) return null;
        return await Profile(user);
    }

    public async Task<AccountProfile?> FindAsync(Guid userId)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.FullName, u.PreferredLanguage, u.IsActive }).SingleOrDefaultAsync();
        if (user is null) return null;
        var roles = await db.UserRoles.AsNoTracking().Where(link => link.UserId == userId)
            .Join(db.Roles.AsNoTracking(), link => link.RoleId, role => role.Id, (_, role) => role.Name!)
            .ToArrayAsync();
        return new(user.Id, user.Email!, user.FullName, user.PreferredLanguage, user.IsActive, roles);
    }

    private async Task<AccountProfile> Profile(ApplicationUser user) => new(user.Id, user.Email!, user.FullName,
        user.PreferredLanguage, user.IsActive, (await users.GetRolesAsync(user)).ToArray());

    private static AuthResult<AccountResponse> Invalid(string message) => AuthResult<AccountResponse>.Failure(
        "Validation.Failed", message, AuthErrorKind.Validation);
    private static AuthResult<AccountResponse> Duplicate() => AuthResult<AccountResponse>.Failure(
        "Auth.EmailAlreadyExists", "Email is already registered.", AuthErrorKind.Conflict);
}
