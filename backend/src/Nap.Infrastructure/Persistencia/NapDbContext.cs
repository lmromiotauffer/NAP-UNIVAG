using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nap.Domain.Agenda;
using Nap.Domain.Alunos;
using Nap.Domain.Atendimentos;
using Nap.Domain.Atividades;
using Nap.Domain.Producao;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;
using Nap.Domain.Configuracoes;
using Nap.Domain.Cursos;
using Nap.Domain.Documentos;
using Nap.Domain.Encaminhamentos;
using Nap.Domain.Notificacoes;
using Nap.Domain.Peis;
using Nap.Infrastructure.Identidade;

namespace Nap.Infrastructure.Persistencia;

public class NapDbContext(DbContextOptions<NapDbContext> options)
    // Sem papéis do Identity: o perfil é a coluna Perfil e a claim role do JWT (migração RemoverPapeisIdentity).
    : IdentityUserContext<UsuarioIdentidade, Guid>(options)
{
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<EscalaProfissional> EscalasProfissional => Set<EscalaProfissional>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<ConfiguracaoAlertas> ConfiguracoesAlertas => Set<ConfiguracaoAlertas>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Compromisso> Compromissos => Set<Compromisso>();
    public DbSet<AutorizacaoForaEscala> AutorizacoesForaEscala => Set<AutorizacaoForaEscala>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<AtividadeCurso> AtividadesCurso => Set<AtividadeCurso>();
    public DbSet<FechamentoSemanal> FechamentosSemanais => Set<FechamentoSemanal>();
    public DbSet<AjusteProducao> AjustesProducao => Set<AjusteProducao>();
    public DbSet<ChaveIdempotencia> ChavesIdempotencia => Set<ChaveIdempotencia>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<TokenRefresh> TokensRefresh => Set<TokenRefresh>();
    public DbSet<Encaminhamento> Encaminhamentos => Set<Encaminhamento>();
    public DbSet<MensagemOutbox> Outbox => Set<MensagemOutbox>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Pei> Peis => Set<Pei>();
    public DbSet<AcessoAssinaturaPei> AcessosAssinaturaPei => Set<AcessoAssinaturaPei>();

    // Os eventos viram linhas da outbox no mesmo SaveChanges (e na mesma transação) que grava o agregado.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GravarEventosNaOutbox();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GravarEventosNaOutbox();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Limpa os eventos já ao enfileirar: se o salvamento falhar, as linhas continuam rastreadas (e o
    // EmTransacaoAsync limpa o rastreador no rollback), então um novo SaveChanges não as duplica.
    private void GravarEventosNaOutbox()
    {
        foreach (var agregado in ChangeTracker.Entries<AgregadoComEventos>().Select(e => e.Entity).ToList())
        {
            foreach (var evento in agregado.Eventos)
                Outbox.Add(new MensagemOutbox(evento));
            agregado.LimparEventos();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("nap");
        builder.ApplyConfigurationsFromAssembly(typeof(NapDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums como texto legível (nvarchar(40)), inclusive os anuláveis.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        // Instantes sempre gravados em UTC, qualquer que seja o deslocamento recebido.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<ConversorUtc>();
    }

    private sealed class ConversorUtc() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v.ToUniversalTime());
}
