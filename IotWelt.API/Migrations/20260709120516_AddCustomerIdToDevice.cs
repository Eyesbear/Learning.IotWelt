using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "Devices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Devices");
        }
    }
}
