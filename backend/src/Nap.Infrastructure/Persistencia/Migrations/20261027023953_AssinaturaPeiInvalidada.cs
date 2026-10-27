using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AssinaturaPeiInvalidada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssinaturasPei_VersaoId_Papel",
                schema: "nap",
                table: "AssinaturasPei");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InvalidadaEm",
                schema: "nap",
                table: "AssinaturasPei",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPei_VersaoId_Papel",
                schema: "nap",
                table: "AssinaturasPei",
                columns: new[] { "VersaoId", "Papel" },
                unique: true,
                filter: "[InvalidadaEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssinaturasPei_VersaoId_Papel",
                schema: "nap",
                table: "AssinaturasPei");

            migrationBuilder.DropColumn(
                name: "InvalidadaEm",
                schema: "nap",
                table: "AssinaturasPei");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPei_VersaoId_Papel",
                schema: "nap",
                table: "AssinaturasPei",
                columns: new[] { "VersaoId", "Papel" },
                unique: true);
        }
    }
}
