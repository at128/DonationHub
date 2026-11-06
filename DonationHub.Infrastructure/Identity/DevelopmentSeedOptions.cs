using System.ComponentModel.DataAnnotations;

namespace DonationHub.Infrastructure.Identity;

public sealed class DevelopmentSeedOptions
{
    public const string SectionName = "DevelopmentSeed";
    [Required, EmailAddress] public string ManagerEmail { get; set; } = string.Empty;
    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$")]
    public string ManagerPassword { get; set; } = string.Empty;
    [Required, EmailAddress] public string DonorEmail { get; set; } = string.Empty;
    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$")]
    public string DonorPassword { get; set; } = string.Empty;
}
