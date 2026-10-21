using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// Índice filtrado para "sem desfecho" (Agendado, sem registro vinculado, já terminado), lido pelo painel, pelo fechamento
    /// semanal e pela produção. Escrito à mão (coluna de tipo complexo), fora do modelo, como os índices de Inicio.
    /// </summary>
    public partial class IndiceCompromissosSemDesfecho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX [IX_Compromissos_SemDesfecho] ON [nap].[Compromissos] ([Fim]) INCLUDE ([ProfissionalId], [AlunoId], [Tipo], [Inicio]) " +
                "WHERE [Estado] = N'Agendado' AND [RegistroVinculadoId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Compromissos_SemDesfecho",
                schema: "nap",
                table: "Compromissos");
        }
    }
}
