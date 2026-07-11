using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptionAndOwnerInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Caption",
                table: "Devices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "CustomerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "CustomerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Caption",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "CustomerProfiles");
        }
    }
}
