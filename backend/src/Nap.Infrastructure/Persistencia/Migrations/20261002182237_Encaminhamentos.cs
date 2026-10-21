using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Encaminhamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Encaminhamentos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Especialidade = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataEncaminhamento = table.Column<DateOnly>(type: "date", nullable: false),
                    MotivoRestrito = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResumoCompartilhavel = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DataAcolhimento = table.Column<DateOnly>(type: "date", nullable: true),
                    DataPrimeiroAtendimento = table.Column<DateOnly>(type: "date", nullable: true),
                    DataEncerramento = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encaminhamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encaminhamentos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encaminhamentos_AspNetUsers_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encaminhamentos_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LidaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EstadoEmail = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacoes_AspNetUsers_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Conteudo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UltimoErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvolucoesEncaminhamento",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstadoNovo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: true),
                    Texto = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EncaminhamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucoesEncaminhamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvolucoesEncaminhamento_Encaminhamentos_EncaminhamentoId",
                        column: x => x.EncaminhamentoId,
                        principalSchema: "nap",
                        principalTable: "Encaminhamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encaminhamentos_AlunoId",
                schema: "nap",
                table: "Encaminhamentos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Encaminhamentos_CursoId_Estado",
                schema: "nap",
                table: "Encaminhamentos",
                columns: new[] { "CursoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Encaminhamentos_Destino_Estado",
                schema: "nap",
                table: "Encaminhamentos",
                columns: new[] { "Destino", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Encaminhamentos_SolicitanteId_Estado",
                schema: "nap",
                table: "Encaminhamentos",
                columns: new[] { "SolicitanteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_EvolucoesEncaminhamento_EncaminhamentoId",
                schema: "nap",
                table: "EvolucoesEncaminhamento",
                column: "EncaminhamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_DestinatarioId_LidaEm_CriadaEm",
                schema: "nap",
                table: "Notificacoes",
                columns: new[] { "DestinatarioId", "LidaEm", "CriadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_EventoId_DestinatarioId",
                schema: "nap",
                table: "Notificacoes",
                columns: new[] { "EventoId", "DestinatarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ProximaTentativaEm_OcorridoEm",
                schema: "nap",
                table: "Outbox",
                columns: new[] { "ProximaTentativaEm", "OcorridoEm" },
                filter: "[ProcessadoEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolucoesEncaminhamento",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Notificacoes",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Encaminhamentos",
                schema: "nap");
        }
    }
}
