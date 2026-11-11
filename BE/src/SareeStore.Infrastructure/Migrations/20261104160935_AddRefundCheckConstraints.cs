using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SareeStore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Refunds_Amount_Positive",
                table: "Refunds",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Refunds_Currency_INR",
                table: "Refunds",
                sql: "[Currency] = 'INR'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Refunds_Amount_Positive",
                table: "Refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Refunds_Currency_INR",
                table: "Refunds");
        }
    }
}
