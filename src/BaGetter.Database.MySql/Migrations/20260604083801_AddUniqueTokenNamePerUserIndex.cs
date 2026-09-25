using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaGetter.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueTokenNamePerUserIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the new index first: MySQL refuses to drop an index that the UserId foreign key still needs.
            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccessTokens_UserId_Name",
                table: "PersonalAccessTokens",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_PersonalAccessTokens_UserId",
                table: "PersonalAccessTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccessTokens_UserId",
                table: "PersonalAccessTokens",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_PersonalAccessTokens_UserId_Name",
                table: "PersonalAccessTokens");
        }
    }
}
