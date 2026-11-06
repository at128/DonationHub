using System.ComponentModel.DataAnnotations;

namespace DonationHub.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;
    [Required] public string SigningKey { get; set; } = string.Empty;
    [Range(1, 1440)] public int AccessTokenMinutes { get; set; } = 15;
    [Range(1, 365)] public int RefreshTokenDays { get; set; } = 7;

    public static bool HasValidSigningKey(JwtOptions options)
    {
        try { return Convert.FromBase64String(options.SigningKey).Length >= 32; }
        catch (FormatException) { return false; }
        catch (ArgumentNullException) { return false; }
    }
}
