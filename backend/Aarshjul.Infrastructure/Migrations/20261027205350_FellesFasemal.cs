using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FellesFasemal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fasemaler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Innhold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Versjon = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fasemaler", x => x.Id);
                    table.CheckConstraint("CK_Fasemaler_EnFellesMal", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                table: "Fasemaler",
                columns: new[] { "Id", "Innhold", "Versjon" },
                values: new object[] { 1, "{\"Faser\":[{\"Navn\":\"Forberedelser\",\"Startmaaned\":-15},{\"Navn\":\"Utarbeidelse av budsjettforslag\",\"Startmaaned\":-10},{\"Navn\":\"Fremleggelse og vedtak\",\"Startmaaned\":-3},{\"Navn\":\"Gjennomf\\u00F8ring og revidering\",\"Startmaaned\":0},{\"Navn\":\"Regnskap\",\"Startmaaned\":12}],\"SisteMaanedInkludert\":15}", new Guid("2467c0d7-5575-45ba-b658-79293feff364") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fasemaler");
        }
    }
}
