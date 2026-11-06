using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Application.Options;
using DonationHub.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DonationHub.Application.Features.Auth;

public sealed class AuthService(IIdentityGateway identity, ITokenService tokens, IRefreshTokenReader reader,
    IGenericRepository<RefreshToken> repository, IUnitOfWork unitOfWork, IOptions<JwtOptions> options, TimeProvider clock)
{
    public Task<AuthResult<AccountResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct) =>
        CreateAccountAsync(request, "Donor", ct);

    public async Task<AuthResult<StaffResponse>> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct)
    {
        if (request.Role is not ("Admin" or "CampaignManager"))
            return AuthResult<StaffResponse>.Failure("Validation.Failed", "Invalid staff role.", AuthErrorKind.Validation);
        var result = await CreateAccountAsync(new(request.Email, request.TemporaryPassword,
            request.FullName, request.PreferredLanguage), request.Role, ct);
        return result.Error is not null ? new(Error: result.Error) : AuthResult<StaffResponse>.Success(
            new(result.Value!.Id, result.Value.Email, result.Value.FullName, request.Role));
    }

    public async Task<AuthResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)
            ? null : await identity.VerifyCredentialsAsync(request.Email.Trim(), request.Password);
        if (user is null) return InvalidCredentials<TokenResponse>();
        // A disabled-account response is only returned after a correct password.
        if (!user.IsActive) return Deactivated<TokenResponse>();

        var (entity, raw) = NewRefreshToken(user.Id, clock.GetUtcNow().UtcDateTime);
        var response = TokenPair(user, raw);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return AuthResult<TokenResponse>.Success(response);
    }

    public async Task<AuthResult<ProfileResponse>> MeAsync(Guid userId)
    {
        var user = await identity.FindAsync(userId);
        if (user is null) return InvalidCredentials<ProfileResponse>();
        if (!user.IsActive) return Deactivated<ProfileResponse>();
        return AuthResult<ProfileResponse>.Success(new(user.Id, user.Email, user.FullName, user.PreferredLanguage, user.Roles));
    }

    public async Task<AuthResult<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return InvalidRefresh<TokenResponse>();
        var now = clock.GetUtcNow().UtcDateTime;
        var old = await reader.FindAsync(tokens.HashRefreshToken(request.RefreshToken), null, ct);
        if (old is null || !old.IsActive(now)) return InvalidRefresh<TokenResponse>();
        var user = await identity.FindAsync(old.UserId);
        if (user is null) return InvalidRefresh<TokenResponse>();
        if (!user.IsActive) return Deactivated<TokenResponse>();

        var (replacement, raw) = NewRefreshToken(user.Id, now);
        var response = TokenPair(user, raw);
        old.Revoke(now, replacement.Id);
        await repository.AddAsync(replacement, ct);
        // EF commits both changes in one transaction. RevokedAt is a concurrency
        // token: a racing replay cannot commit a second replacement.
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (PersistenceConcurrencyException) { return InvalidRefresh<TokenResponse>(); }
        return AuthResult<TokenResponse>.Success(response);
    }

    public async Task<AuthResult<bool>> LogoutAsync(Guid userId, LogoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return AuthResult<bool>.Failure("Validation.Failed", "Refresh token is required.", AuthErrorKind.Validation);
        var token = await reader.FindAsync(tokens.HashRefreshToken(request.RefreshToken), userId, ct);
        if (token is null) return InvalidRefresh<bool>();
        if (token.RevokedAt is not null) return AuthResult<bool>.Success(true);
        token.Revoke(clock.GetUtcNow().UtcDateTime);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (PersistenceConcurrencyException) { return InvalidRefresh<bool>(); }
        return AuthResult<bool>.Success(true);
    }

    private Task<AuthResult<AccountResponse>> CreateAccountAsync(RegisterRequest request, string role, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150 ||
            request.PreferredLanguage is not ("ar" or "en"))
            return Task.FromResult(AuthResult<AccountResponse>.Failure("Validation.Failed", "Invalid profile information.", AuthErrorKind.Validation));
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Task.FromResult(AuthResult<AccountResponse>.Failure("Validation.Failed", "Email and password are required.", AuthErrorKind.Validation));
        return identity.CreateAsync(request, role, clock.GetUtcNow().UtcDateTime, ct);
    }

    private (RefreshToken Entity, string Raw) NewRefreshToken(Guid userId, DateTime nowUtc)
    {
        var raw = tokens.GenerateRefreshToken();
        return (RefreshToken.Create(userId, tokens.HashRefreshToken(raw), nowUtc,
            nowUtc.AddDays(options.Value.RefreshTokenDays)), raw);
    }

    private TokenResponse TokenPair(AccountProfile user, string raw) => new(
        tokens.CreateAccessToken(user.Id, user.Email, user.Roles), raw, "Bearer", options.Value.AccessTokenMinutes * 60);

    private static AuthResult<T> InvalidCredentials<T>() => AuthResult<T>.Failure(
        "Auth.InvalidCredentials", "Invalid email or password.", AuthErrorKind.Unauthorized);
    private static AuthResult<T> InvalidRefresh<T>() => AuthResult<T>.Failure(
        "Auth.RefreshTokenInvalid", "Refresh token is invalid, expired or revoked.", AuthErrorKind.Unauthorized);
    private static AuthResult<T> Deactivated<T>() => AuthResult<T>.Failure(
        "Auth.AccountDeactivated", "Account is deactivated.", AuthErrorKind.Forbidden);
}
