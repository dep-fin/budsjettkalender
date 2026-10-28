using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DagligEpostoppsummering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Epostoppsummeringer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LokalDato = table.Column<DateOnly>(type: "date", nullable: false),
                    AktivertUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Utsettelsesgrunn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BekreftetUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    Versjon = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostoppsummeringer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NyFristHendelser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FristId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpprettetUtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NyFristHendelser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Oppsummeringshendelser",
                columns: table => new
                {
                    OppsummeringId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HendelseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oppsummeringshendelser", x => new { x.OppsummeringId, x.HendelseId });
                    table.ForeignKey(
                        name: "FK_Oppsummeringshendelser_Epostoppsummeringer_OppsummeringId",
                        column: x => x.OppsummeringId,
                        principalTable: "Epostoppsummeringer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Epostoppsummeringer_BrukerId_LokalDato",
                table: "Epostoppsummeringer",
                columns: new[] { "BrukerId", "LokalDato" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_FristId",
                table: "NyFristHendelser",
                column: "FristId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_OpprettetUtcTicks",
                table: "NyFristHendelser",
                column: "OpprettetUtcTicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NyFristHendelser");

            migrationBuilder.DropTable(
                name: "Oppsummeringshendelser");

            migrationBuilder.DropTable(
                name: "Epostoppsummeringer");
        }
    }
}
