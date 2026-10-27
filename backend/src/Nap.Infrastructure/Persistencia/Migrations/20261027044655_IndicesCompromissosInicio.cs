using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// Índices de Inicio e RealizacaoInicio sem profissional: a agenda da equipe inteira (e o "hoje" do painel) filtra por eles
    /// num OR; sem ambos, o SQL Server varre Compromissos. O de Inicio é escrito à mão (coluna de tipo complexo), como os da Inicial.
    /// </summary>
    public partial class IndicesCompromissosInicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_RealizacaoInicio",
                schema: "nap",
                table: "Compromissos",
                column: "RealizacaoInicio");

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_Inicio",
                schema: "nap",
                table: "Compromissos",
                column: "Inicio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Compromissos_Inicio",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropIndex(
                name: "IX_Compromissos_RealizacaoInicio",
                schema: "nap",
                table: "Compromissos");
        }
    }
}
