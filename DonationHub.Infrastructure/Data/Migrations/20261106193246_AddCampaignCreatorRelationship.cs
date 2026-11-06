using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonationHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignCreatorRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [Campaigns] c
                    LEFT JOIN [AspNetUsers] u ON u.[Id] = c.[CreatedByUserId]
                    WHERE u.[Id] IS NULL
                )
                BEGIN
                    ;THROW 51000, 'Campaigns contain creator references with no matching user. Repair those references before applying this migration.', 1;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CreatedByUserId",
                table: "Campaigns",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_AspNetUsers_CreatedByUserId",
                table: "Campaigns",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_AspNetUsers_CreatedByUserId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_CreatedByUserId",
                table: "Campaigns");
        }
    }
}
