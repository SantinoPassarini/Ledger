using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndAuthentication : Migration
    {
        private const string OwnerUserId = "11111111-1111-1111-1111-111111111001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // The provisioned owner keeps the existing data. The password hash is filled in by
            // UserProvisioningService on the first API start using Ledger:Owner configuration.
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAtUtc", "DisplayName", "Email", "IsActive", "NormalizedEmail", "PasswordHash" },
                values: new object[]
                {
                    new Guid(OwnerUserId),
                    new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                    "Ledger Owner",
                    "owner@ledger.local",
                    true,
                    "OWNER@LEDGER.LOCAL",
                    string.Empty
                });

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name_Kind",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_CategoryId_Month",
                table: "Budgets");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            // Existing rows belong to the owner user. This also covers categories or accounts
            // created after the initial seed.
            migrationBuilder.Sql(
                $"UPDATE \"Transactions\" SET \"UserId\" = '{OwnerUserId}' WHERE \"UserId\" IS NULL;");
            migrationBuilder.Sql(
                $"UPDATE \"Categories\" SET \"UserId\" = '{OwnerUserId}' WHERE \"UserId\" IS NULL;");
            migrationBuilder.Sql(
                $"UPDATE \"Budgets\" SET \"UserId\" = '{OwnerUserId}' WHERE \"UserId\" IS NULL;");
            migrationBuilder.Sql(
                $"UPDATE \"Accounts\" SET \"UserId\" = '{OwnerUserId}' WHERE \"UserId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Budgets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Accounts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_Date",
                table: "Transactions",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Name_Kind",
                table: "Categories",
                columns: new[] { "UserId", "Name", "Kind" },
                unique: true,
                filter: "\"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId",
                table: "Budgets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_CategoryId_Month",
                table: "Budgets",
                columns: new[] { "UserId", "CategoryId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserId_Name",
                table: "Accounts",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_UserId",
                table: "Accounts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_Users_UserId",
                table: "Budgets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_UserId",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_Users_UserId",
                table: "Budgets");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_Date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_Name_Kind",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_CategoryId",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_CategoryId_Month",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_UserId_Name",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Accounts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name_Kind",
                table: "Categories",
                columns: new[] { "Name", "Kind" },
                unique: true,
                filter: "\"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CategoryId_Month",
                table: "Budgets",
                columns: new[] { "CategoryId", "Month" },
                unique: true);
        }
    }
}
