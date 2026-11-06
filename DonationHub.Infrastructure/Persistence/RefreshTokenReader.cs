using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DonationHub.Infrastructure.Persistence;

public sealed class RefreshTokenReader(AppDbContext db) : IRefreshTokenReader
{
    public Task<RefreshToken?> FindAsync(string tokenHash, Guid? userId, CancellationToken ct) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash &&
            (!userId.HasValue || t.UserId == userId.Value), ct);
}
