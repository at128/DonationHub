using System.Security.Claims;
using System.Text;
using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Infrastructure.Authentication;
using DonationHub.Infrastructure.Data;
using DonationHub.Infrastructure.Data.Interceptors;
using DonationHub.Infrastructure.Identity;
using DonationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DonationHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
    {
        var connectionString = configuration
            .GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection is not configured.");

                
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString);

            options.AddInterceptors(
                sp.GetRequiredService<AuditingInterceptor>());
        });


        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<AdminSeedOptions>()
            .Bind(configuration.GetSection(AdminSeedOptions.SectionName))
            .Validate(x=> 
            !string.IsNullOrWhiteSpace(x.Email) &&
            !string.IsNullOrWhiteSpace(x.Password),
            "AdminSeed:Email and AdminSeed:Password must be provided.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.Issuer) &&
                    !string.IsNullOrWhiteSpace(options.Audience) &&
                    IsValidSigningKey(options.SigningKey),
                "Invalid JWT configuration.")
            .Validate(
                options =>
                    options.AccessTokenMinutes > 0 &&
                    options.RefreshTokenDays > 0,
                "JWT lifetimes must be positive.")
            .ValidateOnStart();


        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Convert.FromBase64String(jwt.SigningKey)),

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    ClockSkew = TimeSpan.Zero,

                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization();

        services.AddScoped(
            typeof(IRepository<>),
            typeof(EfRepository<>));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
    private static bool IsValidSigningKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        try
        {
            return Convert.FromBase64String(key).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}