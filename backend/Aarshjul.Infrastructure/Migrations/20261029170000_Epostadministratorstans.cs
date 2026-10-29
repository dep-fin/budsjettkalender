using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations;

public partial class Epostadministratorstans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Epostadministratorstanser",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BrukerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                AktoerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Tidspunkt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Handling = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Epostadministratorstanser", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_Epostadministratorstanser_BrukerId_Tidspunkt",
            table: "Epostadministratorstanser", columns: new[] { "BrukerId", "Tidspunkt" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "Epostadministratorstanser");
}
