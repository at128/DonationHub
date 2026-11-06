using System.ComponentModel.DataAnnotations;

namespace DonationHub.Application.Options;

public sealed class DonationOptions
{
    public const string SectionName = "Donation";
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal MinAmount { get; set; } = 1.00m;
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal MaxAmount { get; set; } = 10000.00m;
    [Range(1, 1440)] public int PendingTimeoutMinutes { get; set; } = 30;
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "USD";

    public static bool HasValidLimits(DonationOptions options) => options.MaxAmount >= options.MinAmount &&
        decimal.Round(options.MinAmount, 2) == options.MinAmount && decimal.Round(options.MaxAmount, 2) == options.MaxAmount;
}
