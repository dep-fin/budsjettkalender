using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnikeInternrunder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    IF EXISTS (SELECT 1 FROM [InternRunder] GROUP BY [Rundetype], [Aar] HAVING COUNT(*) > 1)
                        THROW 51091, N'Internrunder har duplikater for rundetype og år. Ta sikkerhetskopi og avklar hvilke runder som skal bevares før migreringen kjøres på nytt. Ingen runder eller gjøremål blir slettet eller slått sammen automatisk.', 1;
                    """);
            }

            migrationBuilder.DropIndex(
                name: "IX_InternRunder_Rundetype_Aar",
                table: "InternRunder");

            migrationBuilder.CreateIndex(
                name: "IX_InternRunder_Rundetype",
                table: "InternRunder",
                column: "Rundetype",
                unique: true,
                filter: "[Aar] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InternRunder_Rundetype_Aar",
                table: "InternRunder",
                columns: new[] { "Rundetype", "Aar" },
                unique: true,
                filter: "[Aar] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InternRunder_Rundetype",
                table: "InternRunder");

            migrationBuilder.DropIndex(
                name: "IX_InternRunder_Rundetype_Aar",
                table: "InternRunder");

            migrationBuilder.CreateIndex(
                name: "IX_InternRunder_Rundetype_Aar",
                table: "InternRunder",
                columns: new[] { "Rundetype", "Aar" });
        }
    }
}
