using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class OneOwnerPerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccountMemberships_OneOwnerPerAccount",
                table: "AccountMemberships",
                column: "AccountId",
                unique: true,
                filter: "[Role] = 'Owner'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMemberships_OneOwnerPerAccount",
                table: "AccountMemberships");
        }
    }
}
