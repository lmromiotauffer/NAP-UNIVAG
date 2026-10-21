using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Sessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokensRefresh",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevogadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubstituidoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRefresh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRefresh_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensRefresh_FamiliaId",
                schema: "nap",
                table: "TokensRefresh",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensRefresh_HashSha256",
                schema: "nap",
                table: "TokensRefresh",
                column: "HashSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensRefresh_UsuarioId",
                schema: "nap",
                table: "TokensRefresh",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokensRefresh",
                schema: "nap");
        }
    }
}
