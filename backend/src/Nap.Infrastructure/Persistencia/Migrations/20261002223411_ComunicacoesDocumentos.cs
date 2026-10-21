using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ComunicacoesDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documentos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoConteudo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChaveArmazenamento = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Visibilidade = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompartilhadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompartilhadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompartilhadoJustificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevogadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevogadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevogadoJustificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArquivadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArquivadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArquivadoMotivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documentos_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documentos_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mensagens",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Assunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Corpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncaminhamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RespostaDeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnviadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LidaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArquivadaPeloAutorEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArquivadaPeloDestinatarioEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensagens_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagens_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagens_AspNetUsers_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagens_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mensagens_Encaminhamentos_EncaminhamentoId",
                        column: x => x.EncaminhamentoId,
                        principalSchema: "nap",
                        principalTable: "Encaminhamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_AlunoId_ArquivadoEm",
                schema: "nap",
                table: "Documentos",
                columns: new[] { "AlunoId", "ArquivadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ChaveArmazenamento",
                schema: "nap",
                table: "Documentos",
                column: "ChaveArmazenamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CriadoPorId",
                schema: "nap",
                table: "Documentos",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CursoId_Visibilidade",
                schema: "nap",
                table: "Documentos",
                columns: new[] { "CursoId", "Visibilidade" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_AlunoId",
                schema: "nap",
                table: "Mensagens",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_AutorId_Estado",
                schema: "nap",
                table: "Mensagens",
                columns: new[] { "AutorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_CursoId",
                schema: "nap",
                table: "Mensagens",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_DestinatarioId_Estado_LidaEm",
                schema: "nap",
                table: "Mensagens",
                columns: new[] { "DestinatarioId", "Estado", "LidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_EncaminhamentoId",
                schema: "nap",
                table: "Mensagens",
                column: "EncaminhamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_FioId",
                schema: "nap",
                table: "Mensagens",
                column: "FioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documentos",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Mensagens",
                schema: "nap");
        }
    }
}
