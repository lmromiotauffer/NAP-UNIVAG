using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CompromissoGrupoAutorizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrupoId",
                schema: "nap",
                table: "Compromissos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutorizacoesForaEscala",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AutorizadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevogadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Fim = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizacoesForaEscala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutorizacoesForaEscala_AspNetUsers_AutorizadoPorId",
                        column: x => x.AutorizadoPorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutorizacoesForaEscala_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_GrupoId",
                schema: "nap",
                table: "Compromissos",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_AutorizacoesForaEscala_AutorizadoPorId",
                schema: "nap",
                table: "AutorizacoesForaEscala",
                column: "AutorizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AutorizacoesForaEscala_ProfissionalId_RevogadaEm",
                schema: "nap",
                table: "AutorizacoesForaEscala",
                columns: new[] { "ProfissionalId", "RevogadaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutorizacoesForaEscala",
                schema: "nap");

            migrationBuilder.DropIndex(
                name: "IX_Compromissos_GrupoId",
                schema: "nap",
                table: "Compromissos");

            migrationBuilder.DropColumn(
                name: "GrupoId",
                schema: "nap",
                table: "Compromissos");
        }
    }
}
