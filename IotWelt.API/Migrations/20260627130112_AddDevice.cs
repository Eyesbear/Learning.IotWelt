using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Typ = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Standort = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: true),
                    Temperatur = table.Column<double>(type: "float", nullable: true),
                    RelativeFeuchte = table.Column<double>(type: "float", nullable: true),
                    Wassertank = table.Column<bool>(type: "bit", nullable: false),
                    ZuletztGesehen = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
