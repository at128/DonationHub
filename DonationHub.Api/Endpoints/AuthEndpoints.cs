using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Domain.Entities;
using DonationHub.Infrastructure.Authentication;
using DonationHub.Infrastructure.Data;
using DonationHub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DonationHub.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/auth")
                        .WithTags("Authentication");

        app.MapPost("/api/v1/admin/staff", CreateStaff)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithTags("Admin");

        group.MapPost("/register",Register);
        group.MapPost("/login",Login);
        group.MapGet("/me",Me).RequireAuthorization();
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout).RequireAuthorization();
        return group;
    }

    private static async Task<IResult> Register(RegisterRequest request,UserManager<ApplicationUser> userManager)
    {
        if(string.IsNullOrWhiteSpace(request.FullName) ||
            request.FullName.Trim().Length > 150 ||
            request.PreferredLanguage is not ("ar" or "en"))
        {
            return Results.BadRequest(
                new { error = "Invalid profile information." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(
                new { error = "Email and password are required." });
        }

        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Results.Conflict(
                new { error = "Email is already registered." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(
            user, request.Password);

        if (!result.Succeeded)
        {
            return Results.BadRequest(
                result.Errors.Select(e => e.Description));
        }

        result = await userManager.AddToRoleAsync(user, "Donor");

        if (!result.Succeeded)
        {
            // Roll back the newly created account
            await userManager.DeleteAsync(user);

            return Results.Problem(
                "Failed to assign the Donor role.");
        }

        return Results.Json(
            new { user.Id, user.Email, user.FullName },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string invalidCredentials = "Invalid email or password.";

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Json(
                new { error = invalidCredentials },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null ||
            !user.IsActive ||
            !await userManager.CheckPasswordAsync(
                user, request.Password))
        {
            return Results.Json(
                new { error = invalidCredentials },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var roles = await userManager.GetRolesAsync(user);

        var accessToken = tokenService.CreateAccessToken(
            user.Id,
            user.Email!,
            roles);

        var refreshToken = tokenService.GenerateRefreshToken();

        var now = DateTime.UtcNow;

        var tokenEntity = RefreshToken.Create(
            user.Id,
            tokenService.HashRefreshToken(refreshToken),
            now,
            now.AddDays(jwtOptions.Value.RefreshTokenDays));

        dbContext.RefreshTokens.Add(tokenEntity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = jwtOptions.Value.AccessTokenMinutes * 60
        });
    }
    private static async Task<IResult> Me(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var id = principal.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(id, out var userId))
            return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(
            userId.ToString());

        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.PreferredLanguage,
            Roles = roles
        });
    }

    private static async Task<IResult> Refresh(
    RefreshRequest request,
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.Unauthorized();

        var now = DateTime.UtcNow;
        var tokenHash = tokenService.HashRefreshToken(
            request.RefreshToken);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);

        var oldToken = await db.RefreshTokens
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash, ct);

        if (oldToken is null || !oldToken.IsActive(now))
            return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(
            oldToken.UserId.ToString());

        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        var roles = await userManager.GetRolesAsync(user);

        var accessToken = tokenService.CreateAccessToken(
            user.Id, user.Email!, roles);

        var refreshToken = tokenService.GenerateRefreshToken();

        var newToken = RefreshToken.Create(
            user.Id,
            tokenService.HashRefreshToken(refreshToken),
            now,
            now.AddDays(jwtOptions.Value.RefreshTokenDays));

        // Revoke the old token and link it to the new one
        oldToken.Revoke(now, newToken.Id);

        db.RefreshTokens.Add(newToken);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Results.Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = jwtOptions.Value.AccessTokenMinutes * 60
        });
    }
    

private static async Task<IResult> Logout(
    LogoutRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    ITokenService tokenService,
    CancellationToken ct)
    {
        var id = principal.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(id, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Results.BadRequest();

        var tokenHash = tokenService.HashRefreshToken(
            request.RefreshToken);

        var token = await db.RefreshTokens
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash &&
                    t.UserId == userId, ct);

        if (token is null)
            return Results.Unauthorized();

        var now = DateTime.UtcNow;

        if (token.IsActive(now))
        {
            token.Revoke(now);
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record LogoutRequest(string RefreshToken);


private static async Task<IResult> CreateStaff(
    CreateStaffRequest request,
    UserManager<ApplicationUser> userManager)
    {
        if (request.Role is not ("Admin" or "CampaignManager"))
            return Results.BadRequest(
                new { error = "Invalid staff role." });

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            request.FullName.Trim().Length > 150 ||
            request.PreferredLanguage is not ("ar" or "en"))
        {
            return Results.BadRequest(
                new { error = "Invalid profile information." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            return Results.BadRequest(
                new { error = "Email and password are required." });
        }

        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
            return Results.Conflict(
                new { error = "Email is already registered." });

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(
            user, request.TemporaryPassword);

        if (!result.Succeeded)
            return Results.BadRequest(
                result.Errors.Select(e => e.Description));

        result = await userManager.AddToRoleAsync(
            user, request.Role);

        if (!result.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return Results.Problem(
                "Failed to assign the staff role.");
        }

        return Results.Json(
            new
            {
                user.Id,
                user.Email,
                user.FullName,
                Role = request.Role
            },
            statusCode: StatusCodes.Status201Created);
    }

}




public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string PreferredLanguage);


public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record CreateStaffRequest(
    string Email,
    string TemporaryPassword,
    string FullName,
    string PreferredLanguage,
    string Role);