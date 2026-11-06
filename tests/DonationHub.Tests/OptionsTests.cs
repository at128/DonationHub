using System.Security.Cryptography;
using DonationHub.Application;
using DonationHub.Application.Options;
using DonationHub.Infrastructure;
using DonationHub.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DonationHub.Tests;

public sealed class OptionsTests
{
    [Theory]
    [InlineData("Jwt:Issuer", " ")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:SigningKey", "not-base64")]
    [InlineData("Jwt:SigningKey", "c2hvcnQ=")]
    [InlineData("Jwt:AccessTokenMinutes", "0")]
    [InlineData("Jwt:RefreshTokenDays", "0")]
    [InlineData("AdminSeed:Email", "invalid")]
    [InlineData("AdminSeed:Password", "lowercaseonly")]
    [InlineData("Donation:MinAmount", "0")]
    [InlineData("Donation:MaxAmount", "0.50")]
    [InlineData("Donation:MinAmount", "1.001")]
    [InlineData("Donation:PendingTimeoutMinutes", "0")]
    [InlineData("Donation:Currency", "usd")]
    [InlineData("ConnectionStrings:DefaultConnection", "")]
    [InlineData("DevelopmentSeed:DonorPassword", "")]
    [InlineData("DevelopmentSeed:ManagerEmail", "admin@example.test")]
    [InlineData("DevelopmentSeed:ManagerEmail", "donor@example.test")]
    public void Invalid_settings_fail_startup_without_connecting_to_database(string key, string value)
    {
        var settings = ValidSettings();
        settings[key] = value;
        using var provider = Provider(settings, development: true);
        var error = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.NotNull(error);
        var failures = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [error];
        Assert.All(failures, failure => Assert.IsType<OptionsValidationException>(failure));
    }

    [Fact]
    public void Defaults_and_legacy_admin_secrets_are_supported()
    {
        using var provider = Provider(ValidSettings(), development: true);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal("admin@example.test", provider.GetRequiredService<IOptions<SeedAdminOptions>>().Value.Email);
        var jwt = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal(15, jwt.AccessTokenMinutes);
        Assert.Equal(7, jwt.RefreshTokenDays);
        var donation = provider.GetRequiredService<IOptions<DonationOptions>>().Value;
        Assert.Equal(1m, donation.MinAmount);
        Assert.Equal(10000m, donation.MaxAmount);
        Assert.Equal(30, donation.PendingTimeoutMinutes);
        Assert.Equal("USD", donation.Currency);
    }

    [Fact]
    public void Canonical_admin_settings_override_legacy_settings()
    {
        var settings = ValidSettings();
        settings["SeedAdmin:Email"] = "canonical@example.test";
        using var provider = Provider(settings);
        Assert.Equal("canonical@example.test", provider.GetRequiredService<IOptions<SeedAdminOptions>>().Value.Email);
    }

    [Fact]
    public void Production_does_not_require_development_credentials()
    {
        var settings = ValidSettings();
        foreach (var key in settings.Keys.Where(key => key.StartsWith("DevelopmentSeed:")).ToArray()) settings.Remove(key);
        using var provider = Provider(settings);
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings, bool development = false)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration, development);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=UnusedOptionsTest;Integrated Security=True;TrustServerCertificate=True",
        ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests",
        ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["AdminSeed:Email"] = "admin@example.test", ["AdminSeed:Password"] = NewPassword(),
        ["DevelopmentSeed:ManagerEmail"] = "manager@example.test", ["DevelopmentSeed:ManagerPassword"] = NewPassword(),
        ["DevelopmentSeed:DonorEmail"] = "donor@example.test", ["DevelopmentSeed:DonorPassword"] = NewPassword()
    };

    private static string NewPassword() => "Aa9!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
