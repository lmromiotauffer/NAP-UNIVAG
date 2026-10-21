using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AutoriaVinculoCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutorId",
                schema: "nap",
                table: "VinculosCurso",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                schema: "nap",
                table: "VinculosCurso",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegistradoEm",
                schema: "nap",
                table: "VinculosCurso",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<string>(
                name: "DadosAnteriores_Discriminator",
                schema: "nap",
                table: "CorrecoesAtendimento",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Dados_Discriminator",
                schema: "nap",
                table: "Atendimentos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutorId",
                schema: "nap",
                table: "VinculosCurso");

            migrationBuilder.DropColumn(
                name: "Motivo",
                schema: "nap",
                table: "VinculosCurso");

            migrationBuilder.DropColumn(
                name: "RegistradoEm",
                schema: "nap",
                table: "VinculosCurso");

            migrationBuilder.AlterColumn<string>(
                name: "DadosAnteriores_Discriminator",
                schema: "nap",
                table: "CorrecoesAtendimento",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Dados_Discriminator",
                schema: "nap",
                table: "Atendimentos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);
        }
    }
}
