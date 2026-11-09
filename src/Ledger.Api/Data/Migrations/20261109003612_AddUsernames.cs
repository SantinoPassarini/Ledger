using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Existing users keep a stable username derived from the local part of their email.
            // The provisioning service replaces it with the configured username on the next start.
            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"Username\" = SPLIT_PART(\"Email\", '@', 1), " +
                "\"NormalizedUsername\" = UPPER(SPLIT_PART(\"Email\", '@', 1)) " +
                "WHERE \"Username\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");
        }
    }
}
