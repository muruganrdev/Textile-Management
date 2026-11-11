using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SareeStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderAndInventoryHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitBasePrice",
                table: "OrderItems",
                newName: "UnitPrice");

            migrationBuilder.AlterColumn<string>(
                name: "FromStatus",
                table: "OrderStatusHistories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularPrice",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Populate existing rows to preserve price data
            migrationBuilder.Sql("UPDATE [OrderItems] SET [RegularPrice] = [UnitPrice] WHERE [RegularPrice] = 0;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventories_StockQuantity_NonNegative",
                table: "Inventories",
                sql: "[StockQuantity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventories_StockQuantity_NonNegative",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "RegularPrice",
                table: "OrderItems");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "OrderItems",
                newName: "UnitBasePrice");

            migrationBuilder.AlterColumn<string>(
                name: "FromStatus",
                table: "OrderStatusHistories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}
