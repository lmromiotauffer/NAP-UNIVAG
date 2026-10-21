using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nap.Application.Comum;
using Nap.Application.Features.Producao;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Cursos;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Persistencia;

namespace Nap.Infrastructure.Seed;

/// <summary>
/// Dados fictícios de demonstração (nada aqui é pessoa real). Idempotente: não faz nada se <c>admin@nap.demo</c> já existe.
/// Tudo é criado pelas fábricas do domínio, numa única transação. A senha vem de <c>Seed:SenhaPadrao</c>.
/// </summary>
public sealed class SemeadorDemonstracao(NapDbContext db, UserManager<UsuarioIdentidade> usuarios, IRelogio relogio,
    IConfiguration configuracao, IHostEnvironment ambiente, ILogger<SemeadorDemonstracao> logger)
{
    private const string Marca = "Dado fictício de demonstração";

    private static readonly string[] NomesCursos = ["Psicologia", "Direito", "Enfermagem", "Administração", "Engenharia Civil", "Medicina"];

    private static readonly (string Login, string Nome, string Perfil)[] Contas =
    [
        ("admin", "Administração do Sistema", Perfis.Administrador),
        ("coordenacao", "Helena Duarte", Perfis.CoordenadorNap),
        ("ana.beatriz", "Ana Beatriz Rocha", Perfis.ProfissionalNap),
        ("carlos.mendes", "Carlos Mendes", Perfis.ProfissionalNap),
        ("fernanda.lima", "Fernanda Lima", Perfis.ProfissionalNap),
        ("rafael.torres", "Rafael Torres", Perfis.ProfissionalNap),
        ("coord.direito", "Marcos Vieira", Perfis.CoordenadorCurso),
        ("sip", "Equipe SIP", Perfis.Sip),
        ("gestao", "Gestão Acadêmica", Perfis.Gestao),
    ];

    private static readonly string[] NomesAlunos =
    [
        "Lucas Andrade Pires", "Mariana Couto Sales", "Gabriel Nogueira Lins", "Beatriz Fontes Amaral", "Pedro Henrique Saraiva",
        "Juliana Prado Mota", "Thiago Bastos Leal", "Camila Ribeiro Neves", "Rodrigo Teles Barros", "Larissa Monteiro Dias",
        "Felipe Arantes Cruz", "Isabela Quintana Reis", "Vinícius Campelo Faria", "Natália Siqueira Brum", "Eduardo Valente Pina",
        "Sofia Carvalhal Melo", "Matheus Ourique Lago", "Letícia Paranhos Viana", "André Lacerda Moura", "Yasmin Bragança Tavares",
    ];

    private static readonly TipoAtendimento[] TiposIndividuais =
        [TipoAtendimento.Acompanhamento, TipoAtendimento.Psicopedagogico, TipoAtendimento.OrientacaoAcademica];

    public async Task ExecutarAsync(CancellationToken ct = default)
    {
        if (ambiente.IsProduction())
            throw new InvalidOperationException("Semeador abortado: dados de demonstração nunca são criados em produção.");
        if (await usuarios.FindByEmailAsync("admin@nap.demo") is not null)
            return;
        var senha = configuracao["Seed:SenhaPadrao"];
        if (string.IsNullOrWhiteSpace(senha))
            throw new InvalidOperationException(
                "Semeador abortado: defina Seed:SenhaPadrao (variável de ambiente ou user-secrets) com a senha dos usuários de demonstração.");

        var agora = relogio.Agora;
        var hoje = relogio.Hoje;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var cursos = NomesCursos.Select(Curso.Criar).ToList();
        db.Cursos.AddRange(cursos);
        await db.SaveChangesAsync(ct);
        var direito = cursos[1].Id;

        var ids = new Dictionary<string, Guid>();
        foreach (var (login, nome, perfil) in Contas)
        {
            var usuario = new UsuarioIdentidade
            {
                Id = Guid.CreateVersion7(), UserName = $"{login}@nap.demo", Email = $"{login}@nap.demo", EmailConfirmed = true,
                NomeCompleto = nome, Perfil = perfil, CursoId = perfil == Perfis.CoordenadorCurso ? direito : null, Ativo = true,
            };
            var resultado = await usuarios.CreateAsync(usuario, senha);
            if (!resultado.Succeeded)
                throw new InvalidOperationException(
                    $"Semeador abortado: não foi possível criar {usuario.Email}: {string.Join("; ", resultado.Errors.Select(e => e.Description))}");
            ids[login] = usuario.Id;
        }
        var admin = ids["admin"];
        Guid[] profissionais = [ids["ana.beatriz"], ids["carlos.mendes"], ids["fernanda.lima"], ids["rafael.torres"]];

        // Aluno 0 fica fora do rodízio: recebe uma sequência própria terminando em 3 faltas seguidas (aciona o alerta).
        var alunos = NomesAlunos.Select((nome, i) => Aluno.Criar(nome, Matricula.Criar($"DEMO-{i + 1:000}"), cursos[i % cursos.Count].Id,
            i % 10 + 1, hoje.AddMonths(-6), profissionais[i % profissionais.Length], null, Marca, admin, agora)).ToList();
        db.Alunos.AddRange(alunos);

        var aleatorio = new Random(2026);
        var segundaAtual = ServicoFechamento.SegundaDe(hoje);
        for (var semana = -21; semana <= 0; semana++)
        for (var diaDaSemana = 0; diaDaSemana < 4; diaDaSemana++) // segunda a quinta; sexta é sem atendimento
        for (var p = 0; p < profissionais.Length; p++)
        foreach (var hora in (int[])[9, 14])
        {
            if (aleatorio.NextDouble() > 0.6) continue;
            var doProfissional = alunos.Where((_, i) => i > 0 && i % profissionais.Length == p).ToList();
            var aluno = doProfissional[aleatorio.Next(doProfissional.Count)];
            var dia = segundaAtual.AddDays(semana * 7 + diaDaSemana);
            var sorteio = aleatorio.NextDouble();
            // Compromissos passados sem desfecho só nas últimas 3 semanas (esquecimentos recentes).
            EstadoAtendimento? desfecho = sorteio < 0.78 ? EstadoAtendimento.Realizado
                : sorteio < 0.93 ? EstadoAtendimento.Ausente
                : semana < -3 ? EstadoAtendimento.Realizado : null;
            Agendar(aluno, profissionais[p], dia, hora, desfecho);
        }
        for (var semana = -8; semana <= -1; semana++)
            Agendar(alunos[0], profissionais[0], segundaAtual.AddDays(semana * 7 + 2), 11,
                semana >= -3 ? EstadoAtendimento.Ausente : EstadoAtendimento.Realizado);

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        logger.LogInformation("Dados de demonstração criados: {Cursos} cursos, {Usuarios} usuários, {Alunos} alunos",
            cursos.Count, Contas.Length, alunos.Count);

        // Atendimento só é registrado para horário já encerrado; os demais ficam Agendados (futuros ou sem desfecho).
        void Agendar(Aluno aluno, Guid profissional, DateOnly dia, int hora, EstadoAtendimento? desfecho)
        {
            var inicio = relogio.Fuso.InicioDoDia(dia).AddHours(hora);
            var compromisso = Compromisso.Criar(TipoCompromisso.Atendimento, $"Atendimento — {aluno.NomeCompleto}",
                IntervaloHorario.Criar(inicio, inicio.AddMinutes(50)), profissional, aluno.Id, null, profissional, agora);
            db.Compromissos.Add(compromisso);
            if (desfecho is not { } estado || inicio.AddMinutes(50) > agora) return;
            var dados = estado == EstadoAtendimento.Realizado ? new DadosClinicos(Marca, null, null, null) : null;
            db.Atendimentos.Add(Atendimento.RegistrarDeCompromisso(compromisso,
                TiposIndividuais[aleatorio.Next(TiposIndividuais.Length)], estado, dados, profissional, agora));
        }
    }
}
