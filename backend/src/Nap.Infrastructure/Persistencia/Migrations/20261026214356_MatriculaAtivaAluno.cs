using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MatriculaAtivaAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatriculaAtiva",
                schema: "nap",
                table: "Alunos",
                type: "bit",
                nullable: false,
                // Alunos já cadastrados seguem com matrícula ativa.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatriculaAtiva",
                schema: "nap",
                table: "Alunos");
        }
    }
}
