using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Producao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RealizacaoFim",
                schema: "nap",
                table: "Compromissos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RealizacaoInicio",
                schema: "nap",
                table: "Compromissos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RealizadoPorId",
                schema: "nap",
                table: "Compromissos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AjustesProducao",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Ocorrencias = table.Column<int>(type: "int", nullable: true),
                    Minutos = table.Column<int>(type: "int", nullable: true),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesProducao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AjustesProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AjustesProducao_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FechamentosSemanais",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semana = table.Column<DateOnly>(type: "date", nullable: false),
                    Fechada = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosSemanais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosSemanais_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosFechamento",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosFechamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosFechamento_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosFechamento_FechamentosSemanais_FechamentoId",
                        column: x => x.FechamentoId,
                        principalSchema: "nap",
                        principalTable: "FechamentosSemanais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_RealizadoPorId",
                schema: "nap",
                table: "Compromissos",
                column: "RealizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesProducao_AutorId",
                schema: "nap",
                table: "AjustesProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesProducao_ProfissionalId_Data",
                schema: "nap",
                table: "AjustesProducao",
                columns: new[] { "ProfissionalId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosFechamento_AutorId",
                schema: "nap",
                table: "EventosFechamento",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosFechamento_FechamentoId",
                schema: "nap",
                table: "EventosFechamento",
                column: "FechamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosSemanais_ProfissionalId_Semana",
                schema: "nap",
                table: "FechamentosSemanais",
                columns: new[] { "ProfissionalId", "Semana" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Compromissos_AspNetUsers_RealizadoPorId",
                schema: "nap",
                table: "Compromissos",
                column: "RealizadoPorId",
                principalSchema: "nap",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Compromissos_AspNetUsers_RealizadoPorId",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropTable(
                name: "AjustesProducao",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "EventosFechamento",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "FechamentosSemanais",
                schema: "nap");

            migrationBuilder.DropIndex(
                name: "IX_Compromissos_RealizadoPorId",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropColumn(
                name: "RealizacaoFim",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropColumn(
                name: "RealizacaoInicio",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropColumn(
                name: "RealizadoPorId",
                schema: "nap",
                table: "Compromissos");
        }
    }
}
