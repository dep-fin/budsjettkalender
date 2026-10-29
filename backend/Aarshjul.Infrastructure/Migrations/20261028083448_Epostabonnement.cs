using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Epostabonnement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Epostabonnementer",
                columns: table => new
                {
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NyeFrister = table.Column<bool>(type: "bit", nullable: false),
                    VesentligeEndringer = table.Column<bool>(type: "bit", nullable: false),
                    AlleBudsjettaar = table.Column<bool>(type: "bit", nullable: false),
                    AlleKategorier = table.Column<bool>(type: "bit", nullable: false),
                    Opprettet = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Endret = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Aktivert = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BekreftelsesHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BekreftelseUtloeper = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Bekreftelsesforsoek = table.Column<int>(type: "int", nullable: false),
                    SistSendt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SendevinduStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AntallSendt = table.Column<int>(type: "int", nullable: false),
                    Versjon = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostabonnementer", x => x.BrukerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Epostabonnementer");
        }
    }
}
