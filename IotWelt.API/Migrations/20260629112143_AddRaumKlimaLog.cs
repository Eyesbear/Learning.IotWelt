using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRaumKlimaLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelativeFeuchte",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Temperatur",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Wassertank",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "ZuletztGesehen",
                table: "Devices",
                newName: "ZuerstGesehen");

            migrationBuilder.CreateTable(
                name: "RaumKlimaLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Temperatur = table.Column<double>(type: "float", nullable: true),
                    RelativeFeuchte = table.Column<double>(type: "float", nullable: true),
                    Wassertank = table.Column<bool>(type: "bit", nullable: false),
                    Zeitstempel = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaumKlimaLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaumKlimaLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaumKlimaLogs_DeviceId",
                table: "RaumKlimaLogs",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaumKlimaLogs");

            migrationBuilder.RenameColumn(
                name: "ZuerstGesehen",
                table: "Devices",
                newName: "ZuletztGesehen");

            migrationBuilder.AddColumn<double>(
                name: "RelativeFeuchte",
                table: "Devices",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Temperatur",
                table: "Devices",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Wassertank",
                table: "Devices",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
