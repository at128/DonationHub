using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonationHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Campaigns",
                type: "datetime2",
                nullable: true);

            // Earlier deletions did not record their own timestamp. Retain the
            // existing last-update time as the best available historical value.
            migrationBuilder.Sql("""
                UPDATE [Campaigns]
                SET [DeletedAtUtc] = [UpdatedAt]
                WHERE [IsDeleted] = 1 AND [DeletedAtUtc] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Campaigns");
        }
    }
}
