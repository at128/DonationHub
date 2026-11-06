using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace DonationHub.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    [Required] public string DefaultConnection { get; set; } = string.Empty;

    public static bool IsValid(DatabaseOptions options)
    {
        try
        {
            var connection = new SqlConnectionStringBuilder(options.DefaultConnection);
            return !string.IsNullOrWhiteSpace(connection.DataSource) &&
                !string.IsNullOrWhiteSpace(connection.InitialCatalog) &&
                !options.DefaultConnection.Contains('<');
        }
        catch (ArgumentException) { return false; }
    }
}
