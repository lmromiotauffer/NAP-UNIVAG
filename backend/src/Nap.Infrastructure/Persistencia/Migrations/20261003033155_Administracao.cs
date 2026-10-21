using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Administracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeveTrocarSenha",
                schema: "nap",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConfiguracaoAlertas",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaltasConsecutivas = table.Column<int>(type: "int", nullable: false),
                    FaltasJanela = table.Column<int>(type: "int", nullable: false),
                    JanelaDias = table.Column<int>(type: "int", nullable: false),
                    DiasSemAcolhimento = table.Column<int>(type: "int", nullable: false),
                    DiasSemAtualizacao = table.Column<int>(type: "int", nullable: false),
                    DiasPeiPendente = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracaoAlertas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalasProfissional",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VigenciaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalasProfissional", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalasProfissional_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Feriados",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feriados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaixasEscala",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time", nullable: false),
                    SemAtendimento = table.Column<bool>(type: "bit", nullable: false),
                    EscalaProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaixasEscala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaixasEscala_EscalasProfissional_EscalaProfissionalId",
                        column: x => x.EscalaProfissionalId,
                        principalSchema: "nap",
                        principalTable: "EscalasProfissional",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "nap",
                table: "ConfiguracaoAlertas",
                columns: new[] { "Id", "DiasPeiPendente", "DiasSemAcolhimento", "DiasSemAtualizacao", "FaltasConsecutivas", "FaltasJanela", "JanelaDias" },
                values: new object[] { new Guid("0198a000-0000-7000-8000-000000000001"), 15, 7, 15, 3, 3, 30 });

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_Nome",
                schema: "nap",
                table: "Cursos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalasProfissional_ProfissionalId_VigenciaInicio",
                schema: "nap",
                table: "EscalasProfissional",
                columns: new[] { "ProfissionalId", "VigenciaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_FaixasEscala_EscalaProfissionalId",
                schema: "nap",
                table: "FaixasEscala",
                column: "EscalaProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_Feriados_Data",
                schema: "nap",
                table: "Feriados",
                column: "Data",
                unique: true,
                filter: "[Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracaoAlertas",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "FaixasEscala",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Feriados",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "EscalasProfissional",
                schema: "nap");

            migrationBuilder.DropIndex(
                name: "IX_Cursos_Nome",
                schema: "nap",
                table: "Cursos");

            migrationBuilder.DropColumn(
                name: "DeveTrocarSenha",
                schema: "nap",
                table: "AspNetUsers");
        }
    }
}
