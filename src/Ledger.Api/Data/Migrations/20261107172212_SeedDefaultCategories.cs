using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ledger.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAtUtc", "Kind", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Income", "Salary" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Income", "Freelance" },
                    { new Guid("11111111-1111-1111-1111-111111111103"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Income", "Investments" },
                    { new Guid("11111111-1111-1111-1111-111111111104"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Income", "Other Income" },
                    { new Guid("11111111-1111-1111-1111-111111111201"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Food" },
                    { new Guid("11111111-1111-1111-1111-111111111202"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Groceries" },
                    { new Guid("11111111-1111-1111-1111-111111111203"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Transport" },
                    { new Guid("11111111-1111-1111-1111-111111111204"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Housing" },
                    { new Guid("11111111-1111-1111-1111-111111111205"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Utilities" },
                    { new Guid("11111111-1111-1111-1111-111111111206"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Health" },
                    { new Guid("11111111-1111-1111-1111-111111111207"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Entertainment" },
                    { new Guid("11111111-1111-1111-1111-111111111208"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Education" },
                    { new Guid("11111111-1111-1111-1111-111111111209"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Clothing" },
                    { new Guid("11111111-1111-1111-1111-111111111210"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Subscriptions" },
                    { new Guid("11111111-1111-1111-1111-111111111211"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Expense", "Other Expenses" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111201"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111202"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111203"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111204"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111205"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111206"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111207"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111208"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111209"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111210"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111211"));
        }
    }
}
