using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialPlatform.TransactionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pan",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinBlock",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Pan",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PinBlock",
                table: "Transactions");
        }
    }
}
