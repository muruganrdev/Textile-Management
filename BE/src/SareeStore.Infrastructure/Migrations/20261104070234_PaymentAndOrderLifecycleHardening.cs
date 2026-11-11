using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SareeStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAndOrderLifecycleHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Simulated");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockRestored",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockRestoredAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_IdempotencyKey",
                table: "Payments",
                columns: new[] { "OrderId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Paid",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "[PaymentStatus] = 'Paid'");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Processing",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "[PaymentStatus] = 'Processing'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_StockRestored_Timestamp",
                table: "Orders",
                sql: "[IsStockRestored] = 0 OR [StockRestoredAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Paid",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Processing",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_StockRestored_Timestamp",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentProvider",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsStockRestored",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StockRestoredAtUtc",
                table: "Orders");
        }
    }
}
