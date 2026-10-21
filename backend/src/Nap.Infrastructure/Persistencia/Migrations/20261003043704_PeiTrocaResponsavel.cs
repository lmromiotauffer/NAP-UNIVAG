using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PeiTrocaResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrocasResponsavelPei",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponsavelAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NovoResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrocasResponsavelPei", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrocasResponsavelPei_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrocasResponsavelPei_AspNetUsers_NovoResponsavelId",
                        column: x => x.NovoResponsavelId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrocasResponsavelPei_AspNetUsers_ResponsavelAnteriorId",
                        column: x => x.ResponsavelAnteriorId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrocasResponsavelPei_Peis_PeiId",
                        column: x => x.PeiId,
                        principalSchema: "nap",
                        principalTable: "Peis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrocasResponsavelPei_AutorId",
                schema: "nap",
                table: "TrocasResponsavelPei",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TrocasResponsavelPei_NovoResponsavelId",
                schema: "nap",
                table: "TrocasResponsavelPei",
                column: "NovoResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_TrocasResponsavelPei_PeiId",
                schema: "nap",
                table: "TrocasResponsavelPei",
                column: "PeiId");

            migrationBuilder.CreateIndex(
                name: "IX_TrocasResponsavelPei_ResponsavelAnteriorId",
                schema: "nap",
                table: "TrocasResponsavelPei",
                column: "ResponsavelAnteriorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrocasResponsavelPei",
                schema: "nap");
        }
    }
}
