using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VarseltyperOgEndringer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NyFristHendelser_FristId",
                table: "NyFristHendelser");

            migrationBuilder.AddColumn<string>(
                name: "EtterJson",
                table: "NyFristHendelser",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FoerJson",
                table: "NyFristHendelser",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Forklaring",
                table: "NyFristHendelser",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "NyFristHendelser",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Ny");

            migrationBuilder.AddColumn<long>(
                name: "Endringssekvens",
                table: "Epostaktiveringer",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_FristId",
                table: "NyFristHendelser",
                column: "FristId",
                unique: true,
                filter: "[Type] = 'Ny'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NyFristHendelser_FristId",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "EtterJson",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "FoerJson",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "Forklaring",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "NyFristHendelser");

            migrationBuilder.DropColumn(
                name: "Endringssekvens",
                table: "Epostaktiveringer");

            migrationBuilder.CreateIndex(
                name: "IX_NyFristHendelser_FristId",
                table: "NyFristHendelser",
                column: "FristId",
                unique: true);
        }
    }
}
