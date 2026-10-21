using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_Acao_Em",
                schema: "nap",
                table: "EventosAuditoria",
                columns: new[] { "Acao", "Em" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_UsuarioId_Em",
                schema: "nap",
                table: "EventosAuditoria",
                columns: new[] { "UsuarioId", "Em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventosAuditoria_Acao_Em",
                schema: "nap",
                table: "EventosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_EventosAuditoria_UsuarioId_Em",
                schema: "nap",
                table: "EventosAuditoria");
        }
    }
}
