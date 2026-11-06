using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IotWelt.API.Migrations
{
    /// <inheritdoc />
    public partial class SensorKontakteUndAkku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Von Hand angepasst: EF hat hier ein RenameColumn("Wassertank" → "S2Input") erzeugt, weil beide bit NOT NULL sind.
            // Der Wasseralarm ist aber kein Kontakteingang — alte Werte sollen nicht als "S2 aktiv" erscheinen.
            migrationBuilder.DropColumn(
                name: "Wassertank",
                table: "RaumKlimaLogs");

            migrationBuilder.AddColumn<double>(
                name: "BatterieSpannung",
                table: "RaumKlimaLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "S1Input",
                table: "RaumKlimaLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "S2Input",
                table: "RaumKlimaLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatterieSpannung",
                table: "RaumKlimaLogs");

            migrationBuilder.DropColumn(
                name: "S1Input",
                table: "RaumKlimaLogs");

            migrationBuilder.DropColumn(
                name: "S2Input",
                table: "RaumKlimaLogs");

            migrationBuilder.AddColumn<bool>(
                name: "Wassertank",
                table: "RaumKlimaLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
