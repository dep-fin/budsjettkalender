using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HistoriskSlettingssynlighet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Livssyklussekvens",
                table: "Epostaktiveringer",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Eksisterende frister er etablerte ved installasjon av EV07. Dette
            // oppretter ingen hendelser og endrer ingen av de eksisterende typegrensene.
            migrationBuilder.Sql("UPDATE Epostaktiveringer SET Livssyklussekvens = (SELECT Verdi FROM Epostrekkefolge WHERE Id = 1)");

            migrationBuilder.CreateTable(
                name: "FristHendelsessynlighet",
                columns: table => new
                {
                    HendelseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GruppeKode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FristHendelsessynlighet", x => new { x.HendelseId, x.GruppeKode });
                    table.ForeignKey(
                        name: "FK_FristHendelsessynlighet_NyFristHendelser_HendelseId",
                        column: x => x.HendelseId,
                        principalTable: "NyFristHendelser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FristHendelsessynlighet");

            migrationBuilder.DropColumn(
                name: "Livssyklussekvens",
                table: "Epostaktiveringer");
        }
    }
}
