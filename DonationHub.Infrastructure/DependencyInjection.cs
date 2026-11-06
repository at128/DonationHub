using System.Security.Claims;
using DonationHub.Application.Abstractions.Authentication;
using DonationHub.Application.Abstractions.Persistence;
using DonationHub.Application.Features.Categories;
using DonationHub.Application.Options;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DonationHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations().Validate(DatabaseOptions.IsValid, "Configure a valid DefaultConnection through secrets or environment variables.")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations().Validate(JwtOptions.HasValidSigningKey, "Jwt:SigningKey must be Base64 encoding at least 32 random bytes.")
            .ValidateOnStart();
        services.AddOptions<SeedAdminOptions>()
            .Bind(configuration.GetSection(SeedAdminOptions.LegacySectionName))
            .Bind(configuration.GetSection(SeedAdminOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DonationOptions>().Bind(configuration.GetSection(DonationOptions.SectionName))
            .ValidateDataAnnotations().Validate(DonationOptions.HasValidLimits, "Donation limits must have two decimal places and MinAmount must not exceed MaxAmount.")
            .ValidateOnStart();
        if (isDevelopment)
        {
            services.AddOptions<DevelopmentSeedOptions>().Bind(configuration.GetSection(DevelopmentSeedOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate<IOptions<SeedAdminOptions>>((demo, admin) =>
                    !SameEmail(demo.ManagerEmail, demo.DonorEmail) &&
                    !SameEmail(demo.ManagerEmail, admin.Value.Email) && !SameEmail(demo.DonorEmail, admin.Value.Email),
                    "Development seed emails must be distinct from each other and from the Admin email.")
                .ValidateOnStart();
        }

        services.AddScoped<AuditingInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.DefaultConnection)
                .AddInterceptors(provider.GetRequiredService<AuditingInterceptor>()));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<AppUserStore>();
        services.AddScoped<IUserStore<ApplicationUser>>(provider => provider.GetRequiredService<AppUserStore>());

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddScoped<IRefreshTokenReader, RefreshTokenReader>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true, RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero, RoleClaimType = ClaimTypes.Role
                };
            });
        services.AddAuthorization();
        return services;
    }

    private static bool SameEmail(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
