namespace DonationHub.Application.Features.Auth;

public sealed record RegisterRequest(string Email, string Password, string FullName, string PreferredLanguage = "en");
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record CreateStaffRequest(string Email, string TemporaryPassword, string FullName, string PreferredLanguage, string Role);
public sealed record AccountResponse(Guid Id, string Email, string FullName);
public sealed record StaffResponse(Guid Id, string Email, string FullName, string Role);
public sealed record ProfileResponse(Guid Id, string Email, string FullName, string PreferredLanguage, IReadOnlyList<string> Roles);
public sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);
public sealed record AccountProfile(Guid Id, string Email, string FullName, string PreferredLanguage, bool IsActive, IReadOnlyList<string> Roles);

public enum AuthErrorKind { Validation, Unauthorized, Forbidden, Conflict }
public sealed record AuthError(string Code, string Message, AuthErrorKind Kind);
public sealed record AuthResult<T>(T? Value = default, AuthError? Error = null)
{
    public static AuthResult<T> Success(T value) => new(value);
    public static AuthResult<T> Failure(string code, string message, AuthErrorKind kind) => new(Error: new(code, message, kind));
}
