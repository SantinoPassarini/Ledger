using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferGroupId",
                table: "Transactions",
                column: "TransferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferGroupId_Type",
                table: "Transactions",
                columns: new[] { "TransferGroupId", "Type" },
                unique: true,
                filter: "\"TransferGroupId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_Category_Transfer",
                table: "Transactions",
                sql: "(\"Type\" IN ('Income', 'Expense') AND \"CategoryId\" IS NOT NULL AND \"TransferGroupId\" IS NULL) OR (\"Type\" IN ('TransferOut', 'TransferIn') AND \"CategoryId\" IS NULL AND \"TransferGroupId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferGroupId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferGroupId_Type",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Category_Transfer",
                table: "Transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
