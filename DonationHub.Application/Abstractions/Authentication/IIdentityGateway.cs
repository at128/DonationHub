using DonationHub.Application.Features.Auth;
using DonationHub.Domain.Entities;

namespace DonationHub.Application.Abstractions.Authentication;

public interface IIdentityGateway
{
    Task<AuthResult<AccountResponse>> CreateAsync(RegisterRequest request, string role, DateTime nowUtc, CancellationToken ct);
    Task<AccountProfile?> VerifyCredentialsAsync(string email, string password);
    Task<AccountProfile?> FindAsync(Guid userId);
}

public interface IRefreshTokenReader
{
    // Tracked read for rotation/logout; saves must use the original concurrency snapshot.
    Task<RefreshToken?> FindAsync(string tokenHash, Guid? userId, CancellationToken ct);
}
