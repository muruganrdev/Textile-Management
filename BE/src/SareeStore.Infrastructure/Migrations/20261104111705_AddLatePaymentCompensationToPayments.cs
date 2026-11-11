using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SareeStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLatePaymentCompensationToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLateSuccess",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresRefund",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLateSuccess",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RequiresRefund",
                table: "Payments");
        }
    }
}
