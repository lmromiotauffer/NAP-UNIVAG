using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AtividadesCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtividadesCurso",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompromissoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    JustificativaAvulso = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Fim = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtividadesCurso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtividadesCurso_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtividadesCurso_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtividadesCurso_Compromissos_CompromissoId",
                        column: x => x.CompromissoId,
                        principalSchema: "nap",
                        principalTable: "Compromissos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtividadesCurso_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrecoesAtividade",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InicioAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FimAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CursoIdAnterior = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoAnterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DescricaoAnterior = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrecoesAtividade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrecoesAtividade_AtividadesCurso_AtividadeId",
                        column: x => x.AtividadeId,
                        principalSchema: "nap",
                        principalTable: "AtividadesCurso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_CompromissoId",
                schema: "nap",
                table: "AtividadesCurso",
                column: "CompromissoId",
                unique: true,
                filter: "[CompromissoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_CriadoPorId",
                schema: "nap",
                table: "AtividadesCurso",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_CursoId",
                schema: "nap",
                table: "AtividadesCurso",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_ProfissionalId",
                schema: "nap",
                table: "AtividadesCurso",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrecoesAtividade_AtividadeId",
                schema: "nap",
                table: "CorrecoesAtividade",
                column: "AtividadeId");

            // Escritos à mão: o EF não aceita HasIndex com membro de tipo complexo (Intervalo.Inicio).
            // Fora do modelo/snapshot; as tabelas são removidas no Down, levando os índices junto.
            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_ProfissionalId_Inicio",
                schema: "nap",
                table: "AtividadesCurso",
                columns: new[] { "ProfissionalId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesCurso_CursoId_Inicio",
                schema: "nap",
                table: "AtividadesCurso",
                columns: new[] { "CursoId", "Inicio" },
                filter: "[CursoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrecoesAtividade",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AtividadesCurso",
                schema: "nap");
        }
    }
}
