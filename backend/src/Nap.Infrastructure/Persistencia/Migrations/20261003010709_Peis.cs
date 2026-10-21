using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Peis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PeiVersaoId",
                schema: "nap",
                table: "Mensagens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Peis",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Peis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Peis_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Peis_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Peis_AspNetUsers_ProfissionalResponsavelId",
                        column: x => x.ProfissionalResponsavelId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VersoesPei",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HashConteudo = table.Column<string>(type: "char(64)", nullable: true),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CriadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnviadaRevisaoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AprovadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AprovadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DevolvidaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    JustificativaDevolucao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublicadaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CursoPublicacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArquivadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MotivoArquivamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Conteudo_Adaptacoes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conteudo_CuidadosPedagogicos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conteudo_DemandaInterna = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conteudo_NecessidadesEducacionais = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conteudo_OrientacoesProfessores = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Conteudo_RecursosApoio = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    VigenciaFim = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaInicio = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersoesPei", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersoesPei_AspNetUsers_AprovadaPorId",
                        column: x => x.AprovadaPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VersoesPei_AspNetUsers_CriadaPorId",
                        column: x => x.CriadaPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VersoesPei_AspNetUsers_PublicadaPorId",
                        column: x => x.PublicadaPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VersoesPei_Cursos_CursoPublicacaoId",
                        column: x => x.CursoPublicacaoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VersoesPei_Peis_PeiId",
                        column: x => x.PeiId,
                        principalSchema: "nap",
                        principalTable: "Peis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcessosAssinaturaPei",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashConteudo = table.Column<string>(type: "char(64)", nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false),
                    CodigoHash = table.Column<string>(type: "char(64)", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InvalidadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcessosAssinaturaPei", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcessosAssinaturaPei_AspNetUsers_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcessosAssinaturaPei_VersoesPei_VersaoId",
                        column: x => x.VersaoId,
                        principalSchema: "nap",
                        principalTable: "VersoesPei",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssinaturasPei",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SignatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeSignatario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Metodo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HashConteudo = table.Column<string>(type: "char(64)", nullable: false),
                    Evidencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VersaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssinaturasPei", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssinaturasPei_AspNetUsers_SignatarioId",
                        column: x => x.SignatarioId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssinaturasPei_VersoesPei_VersaoId",
                        column: x => x.VersaoId,
                        principalSchema: "nap",
                        principalTable: "VersoesPei",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CienciasPei",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoordenadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VersaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CienciasPei", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CienciasPei_AspNetUsers_CoordenadorId",
                        column: x => x.CoordenadorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CienciasPei_VersoesPei_VersaoId",
                        column: x => x.VersaoId,
                        principalSchema: "nap",
                        principalTable: "VersoesPei",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_PeiVersaoId",
                schema: "nap",
                table: "Mensagens",
                column: "PeiVersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAssinaturaPei_CriadoPorId",
                schema: "nap",
                table: "AcessosAssinaturaPei",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAssinaturaPei_TokenHash",
                schema: "nap",
                table: "AcessosAssinaturaPei",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcessosAssinaturaPei_VersaoId",
                schema: "nap",
                table: "AcessosAssinaturaPei",
                column: "VersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPei_SignatarioId",
                schema: "nap",
                table: "AssinaturasPei",
                column: "SignatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasPei_VersaoId_Papel",
                schema: "nap",
                table: "AssinaturasPei",
                columns: new[] { "VersaoId", "Papel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CienciasPei_CoordenadorId",
                schema: "nap",
                table: "CienciasPei",
                column: "CoordenadorId");

            migrationBuilder.CreateIndex(
                name: "IX_CienciasPei_VersaoId_CoordenadorId",
                schema: "nap",
                table: "CienciasPei",
                columns: new[] { "VersaoId", "CoordenadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Peis_AlunoId",
                schema: "nap",
                table: "Peis",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Peis_CriadoPorId",
                schema: "nap",
                table: "Peis",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Peis_ProfissionalResponsavelId",
                schema: "nap",
                table: "Peis",
                column: "ProfissionalResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_AprovadaPorId",
                schema: "nap",
                table: "VersoesPei",
                column: "AprovadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_CriadaPorId",
                schema: "nap",
                table: "VersoesPei",
                column: "CriadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_CursoPublicacaoId_Estado",
                schema: "nap",
                table: "VersoesPei",
                columns: new[] { "CursoPublicacaoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_PeiId_Aberta",
                schema: "nap",
                table: "VersoesPei",
                column: "PeiId",
                unique: true,
                filter: "[Estado] IN (N'Rascunho', N'EmRevisao', N'AguardandoAssinaturas')");

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_PeiId_Numero",
                schema: "nap",
                table: "VersoesPei",
                columns: new[] { "PeiId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersoesPei_PublicadaPorId",
                schema: "nap",
                table: "VersoesPei",
                column: "PublicadaPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mensagens_VersoesPei_PeiVersaoId",
                schema: "nap",
                table: "Mensagens",
                column: "PeiVersaoId",
                principalSchema: "nap",
                principalTable: "VersoesPei",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mensagens_VersoesPei_PeiVersaoId",
                schema: "nap",
                table: "Mensagens");

            migrationBuilder.DropTable(
                name: "AcessosAssinaturaPei",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AssinaturasPei",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "CienciasPei",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "VersoesPei",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Peis",
                schema: "nap");

            migrationBuilder.DropIndex(
                name: "IX_Mensagens_PeiVersaoId",
                schema: "nap",
                table: "Mensagens");

            migrationBuilder.DropColumn(
                name: "PeiVersaoId",
                schema: "nap",
                table: "Mensagens");
        }
    }
}
