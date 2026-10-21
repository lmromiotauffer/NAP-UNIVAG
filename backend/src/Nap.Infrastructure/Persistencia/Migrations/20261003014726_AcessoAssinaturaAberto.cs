using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AcessoAssinaturaAberto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AcessosAssinaturaPei_VersaoId_Aberto",
                schema: "nap",
                table: "AcessosAssinaturaPei",
                column: "VersaoId",
                unique: true,
                filter: "[UsadoEm] IS NULL AND [InvalidadoEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcessosAssinaturaPei_VersaoId_Aberto",
                schema: "nap",
                table: "AcessosAssinaturaPei");
        }
    }
}
