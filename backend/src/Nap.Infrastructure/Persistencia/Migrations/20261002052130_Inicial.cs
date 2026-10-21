using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nap.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nap");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Perfil = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChavesIdempotencia",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rota = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChavesIdempotencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cursos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventosAuditoria",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Recurso = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "nap",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "nap",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "nap",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "nap",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "nap",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alunos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Matricula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CursoAtualId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semestre = table.Column<int>(type: "int", nullable: false),
                    InicioAcompanhamento = table.Column<DateOnly>(type: "date", nullable: false),
                    ProfissionalResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NecessidadesEducacionais = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservacoesAcademicas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Situacao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alunos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alunos_AspNetUsers_ProfissionalResponsavelId",
                        column: x => x.ProfissionalResponsavelId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alunos_Cursos_CursoAtualId",
                        column: x => x.CursoAtualId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Compromissos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemarcadoDeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemarcadoParaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistroVinculadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Fim = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compromissos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Compromissos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Compromissos_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MudancasSituacao",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    De = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Para = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MudancasSituacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MudancasSituacao_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VinculosCurso",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Ate = table.Column<DateOnly>(type: "date", nullable: true),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosCurso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosCurso_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VinculosCurso_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalSchema: "nap",
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Atendimentos",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompromissoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    JustificativaAvulso = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    DemandaPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dados_Discriminator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservacoesPrivadas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProximosPassos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecomendacoesEncaminhamento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fim = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Inicio = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atendimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Alunos_AlunoId",
                        column: x => x.AlunoId,
                        principalSchema: "nap",
                        principalTable: "Alunos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atendimentos_AspNetUsers_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalSchema: "nap",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Compromissos_CompromissoId",
                        column: x => x.CompromissoId,
                        principalSchema: "nap",
                        principalTable: "Compromissos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrecoesAtendimento",
                schema: "nap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstadoNovo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TipoAnterior = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InicioAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FimAnterior = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnteriorDemandaPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DadosAnteriores_Discriminator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnteriorObservacoesPrivadas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnteriorProximosPassos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnteriorRecomendacoesEncaminhamento = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrecoesAtendimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrecoesAtendimento_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalSchema: "nap",
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_CursoAtualId",
                schema: "nap",
                table: "Alunos",
                column: "CursoAtualId");

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_Matricula",
                schema: "nap",
                table: "Alunos",
                column: "Matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alunos_ProfissionalResponsavelId",
                schema: "nap",
                table: "Alunos",
                column: "ProfissionalResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "nap",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "nap",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "nap",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "nap",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "nap",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "nap",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "nap",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_AlunoId",
                schema: "nap",
                table: "Atendimentos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_CompromissoId",
                schema: "nap",
                table: "Atendimentos",
                column: "CompromissoId",
                unique: true,
                filter: "[CompromissoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_ProfissionalId",
                schema: "nap",
                table: "Atendimentos",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_ChavesIdempotencia_UsuarioId_Chave",
                schema: "nap",
                table: "ChavesIdempotencia",
                columns: new[] { "UsuarioId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_AlunoId",
                schema: "nap",
                table: "Compromissos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_ProfissionalId",
                schema: "nap",
                table: "Compromissos",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrecoesAtendimento_AtendimentoId",
                schema: "nap",
                table: "CorrecoesAtendimento",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_Em",
                schema: "nap",
                table: "EventosAuditoria",
                column: "Em");

            migrationBuilder.CreateIndex(
                name: "IX_MudancasSituacao_AlunoId",
                schema: "nap",
                table: "MudancasSituacao",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosCurso_AlunoId",
                schema: "nap",
                table: "VinculosCurso",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosCurso_CursoId",
                schema: "nap",
                table: "VinculosCurso",
                column: "CursoId");

            // Escritos à mão: o EF não aceita HasIndex com membro de tipo complexo (Intervalo.Inicio).
            // Fora do modelo/snapshot; as tabelas são removidas no Down, levando os índices junto.
            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_ProfissionalId_Inicio",
                schema: "nap",
                table: "Compromissos",
                columns: new[] { "ProfissionalId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Compromissos_AlunoId_Inicio",
                schema: "nap",
                table: "Compromissos",
                columns: new[] { "AlunoId", "Inicio" },
                filter: "[AlunoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_AlunoId_Inicio",
                schema: "nap",
                table: "Atendimentos",
                columns: new[] { "AlunoId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_ProfissionalId_Inicio",
                schema: "nap",
                table: "Atendimentos",
                columns: new[] { "ProfissionalId", "Inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "ChavesIdempotencia",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "CorrecoesAtendimento",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "EventosAuditoria",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "MudancasSituacao",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "VinculosCurso",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Atendimentos",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Compromissos",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Alunos",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "nap");

            migrationBuilder.DropTable(
                name: "Cursos",
                schema: "nap");
        }
    }
}
