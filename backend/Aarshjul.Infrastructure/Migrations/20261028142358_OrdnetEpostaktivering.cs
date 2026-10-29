using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrdnetEpostaktivering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NyFristHendelser_OpprettetUtcTicks",
                table: "NyFristHendelser");

            migrationBuilder.AddColumn<long>(
                name: "Publiseringssekvens",
                table: "NyFristHendelser",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Epostaktiveringer",
                columns: table => new
                {
                    BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sekvens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostaktiveringer", x => x.BrukerId);
                });

            migrationBuilder.CreateTable(
                name: "Epostrekkefolge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Verdi = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostrekkefolge", x => x.Id);
                    table.CheckConstraint("CK_Epostrekkefolge_EnRad", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                table: "Epostrekkefolge",
                columns: new[] { "Id", "Verdi" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_Publiseringssekvens",
                table: "NyFristHendelser",
                column: "Publiseringssekvens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Epostaktiveringer");

            migrationBuilder.DropTable(
                name: "Epostrekkefolge");

            migrationBuilder.DropIndex(
                name: "IX_NyFristHendelser_Publiseringssekvens",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "Publiseringssekvens",
                table: "NyFristHendelser");

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_OpprettetUtcTicks",
                table: "NyFristHendelser",
                column: "OpprettetUtcTicks");
        }
    }
}
