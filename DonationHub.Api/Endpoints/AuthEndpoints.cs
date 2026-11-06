using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DonationHub.Application.Features.Auth;

namespace DonationHub.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/register", async (RegisterRequest request, AuthService service, CancellationToken ct) =>
            ToHttp(await service.RegisterAsync(request, ct), value => Results.Json(value, statusCode: 201)));
        group.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken ct) =>
            ToHttp(await service.LoginAsync(request, ct), value => Results.Ok(value)));
        group.MapPost("/refresh", async (RefreshRequest request, AuthService service, CancellationToken ct) =>
            ToHttp(await service.RefreshAsync(request, ct), value => Results.Ok(value)));
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/logout", Logout).RequireAuthorization();
        app.MapPost("/api/v1/admin/staff", async (CreateStaffRequest request, AuthService service, CancellationToken ct) =>
            ToHttp(await service.CreateStaffAsync(request, ct), value => Results.Json(value, statusCode: 201)))
            .RequireAuthorization(policy => policy.RequireRole("Admin")).WithTags("Admin");
        return group;
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, AuthService service)
    {
        var userId = UserId(principal);
        return userId is null ? Results.Unauthorized() :
            ToHttp(await service.MeAsync(userId.Value), value => Results.Ok(value));
    }

    private static async Task<IResult> Logout(LogoutRequest request, ClaimsPrincipal principal, AuthService service, CancellationToken ct)
    {
        var userId = UserId(principal);
        return userId is null ? Results.Unauthorized() :
            ToHttp(await service.LogoutAsync(userId.Value, request, ct), _ => Results.NoContent());
    }

    private static Guid? UserId(ClaimsPrincipal principal) => Guid.TryParse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
        out var userId) ? userId : null;

    private static IResult ToHttp<T>(AuthResult<T> result, Func<T, IResult> success)
    {
        if (result.Error is null) return success(result.Value!);
        var error = new { code = result.Error.Code, error = result.Error.Message };
        return result.Error.Kind switch
        {
            AuthErrorKind.Validation => Results.BadRequest(error),
            AuthErrorKind.Unauthorized => Results.Json(error, statusCode: 401),
            AuthErrorKind.Forbidden => Results.Json(error, statusCode: 403),
            AuthErrorKind.Conflict => Results.Conflict(error),
            _ => throw new InvalidOperationException("Unknown authentication error kind.")
        };
    }
}
