using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Abonnementsutvalg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UtvalgJson",
                table: "Epostabonnementer",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UtvalgJson",
                table: "Epostabonnementer");
        }
    }
}
