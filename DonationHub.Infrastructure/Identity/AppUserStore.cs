using DonationHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Identity;

// Allows CreateAsync + AddToRoleAsync to be committed by one Unit of Work.
public sealed class AppUserStore(AppDbContext context, IdentityErrorDescriber describer)
    : UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid>(context, describer)
{
    public override Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        if (!AutoSaveChanges && Context.Entry(user).State == EntityState.Added)
        {
            // Identity updates the user when assigning a role. A not-yet-saved
            // account must remain Added, so the final save inserts it.
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            return Task.FromResult(IdentityResult.Success);
        }
        return base.UpdateAsync(user, cancellationToken);
    }
}
