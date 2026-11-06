using System.ComponentModel.DataAnnotations;

namespace DonationHub.Infrastructure.Identity;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";
    public const string LegacySectionName = "AdminSeed";
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$")]
    public string Password { get; set; } = string.Empty;
}
