using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PeiUnicoPorAlunoEUsuarioReativadoEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Peis_AlunoId",
                schema: "nap",
                table: "Peis");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReativadoEm",
                schema: "nap",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Peis_AlunoId",
                schema: "nap",
                table: "Peis",
                column: "AlunoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Peis_AlunoId",
                schema: "nap",
                table: "Peis");

            migrationBuilder.DropColumn(
                name: "ReativadoEm",
                schema: "nap",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Peis_AlunoId",
                schema: "nap",
                table: "Peis",
                column: "AlunoId");
        }
    }
}
