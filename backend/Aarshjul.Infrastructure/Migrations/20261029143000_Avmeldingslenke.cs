using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Avmeldingslenke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvmeldingsHash",
                table: "Epostoppsummeringer",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Avmeldingssekvens",
                table: "Epostoppsummeringer",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Epostoppsummeringer_AvmeldingsHash",
                table: "Epostoppsummeringer",
                column: "AvmeldingsHash",
                filter: "[AvmeldingsHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Epostoppsummeringer_AvmeldingsHash",
                table: "Epostoppsummeringer");

            migrationBuilder.DropColumn(
                name: "AvmeldingsHash",
                table: "Epostoppsummeringer");

            migrationBuilder.DropColumn(
                name: "Avmeldingssekvens",
                table: "Epostoppsummeringer");
        }
    }
}
