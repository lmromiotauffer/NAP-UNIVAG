using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrecaoRealizacaoAtivoDesde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AtivoDesde",
                schema: "nap",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorrecoesRealizacao",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InicioAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FimAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompromissoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrecoesRealizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrecoesRealizacao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrecoesRealizacao_Compromissos_CompromissoId",
                        column: x => x.CompromissoId,
                        principalSchema: "nap",
                        principalTable: "Compromissos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrecoesRealizacao_AutorId",
                schema: "nap",
                table: "CorrecoesRealizacao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrecoesRealizacao_CompromissoId",
                schema: "nap",
                table: "CorrecoesRealizacao",
                column: "CompromissoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrecoesRealizacao",
                schema: "nap");

            migrationBuilder.DropColumn(
                name: "AtivoDesde",
                schema: "nap",
                table: "AspNetUsers");
        }
    }
}
