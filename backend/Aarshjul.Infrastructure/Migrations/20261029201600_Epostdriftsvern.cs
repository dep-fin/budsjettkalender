using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aarshjul.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Epostdriftsvern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "NesteOppslagUtcTicks", table: "Epostoppsummeringer", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "AntallForsoek",
                table: "Epostoppsummeringer",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ForsoekStartetUtcTicks",
                table: "Epostoppsummeringer",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NesteForsoekUtcTicks",
                table: "Epostoppsummeringer",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Epostdrift",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Stanset = table.Column<bool>(type: "bit", nullable: false),
                    AktoerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EndretUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    Versjon = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostdrift", x => x.Id);
                    table.CheckConstraint("CK_Epostdrift_EnRad", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "Epostsendeforsoek",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OppsummeringId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nummer = table.Column<int>(type: "int", nullable: false),
                    StartetUtcTicks = table.Column<long>(type: "bigint", nullable: false),
                    AvsluttetUtcTicks = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Epostsendeforsoek", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Epostsendeforsoek_Epostoppsummeringer_OppsummeringId",
                        column: x => x.OppsummeringId,
                        principalTable: "Epostoppsummeringer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Epostdrift",
                columns: new[] { "Id", "AktoerId", "EndretUtcTicks", "Stanset", "Versjon" },
                values: new object[] { 1, null, null, false, new Guid("e3110000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "IX_Epostsendeforsoek_OppsummeringId_Nummer",
                table: "Epostsendeforsoek",
                columns: new[] { "OppsummeringId", "Nummer" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NesteOppslagUtcTicks", table: "Epostoppsummeringer");
            migrationBuilder.DropTable(
                name: "Epostdrift");

            migrationBuilder.DropTable(
                name: "Epostsendeforsoek");

            migrationBuilder.DropColumn(
                name: "AntallForsoek",
                table: "Epostoppsummeringer");

            migrationBuilder.DropColumn(
                name: "ForsoekStartetUtcTicks",
                table: "Epostoppsummeringer");

            migrationBuilder.DropColumn(
                name: "NesteForsoekUtcTicks",
                table: "Epostoppsummeringer");
        }
    }
}
