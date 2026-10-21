using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nap.Application.Comum;
using Nap.Application.Features.Agenda;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Atendimentos;
using Nap.Application.Features.Atividades;
using Nap.Application.Features.Auditoria;
using Nap.Application.Features.Auth;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Documentos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Notificacoes;
using Nap.Application.Features.Painel;
using Nap.Application.Features.Peis;
using Nap.Application.Features.Usuarios;
using Nap.Infrastructure.Arquivos;
using Nap.Infrastructure.Assinatura;
using Nap.Infrastructure.Cache;
using Nap.Infrastructure.Excel;
using Nap.Infrastructure.Comum;
using Nap.Infrastructure.Identidade;
using Nap.Infrastructure.Notificacoes;
using Nap.Infrastructure.Pdf;
using Nap.Infrastructure.Persistencia;
using Nap.Infrastructure.Persistencia.Consultas;
using Nap.Infrastructure.Persistencia.Repositorios;
using Nap.Infrastructure.Seed;
using StackExchange.Redis;

namespace Nap.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Persistência, Identity, sessão/JWT e Redis. A autenticação JwtBearer é configurada pela Api.</summary>
    public static IServiceCollection AdicionarInfrastructure(this IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.AddOptions<OpcoesOutbox>().Bind(configuracao.GetSection("Nap:Outbox"))
            .Validate(OpcoesOutbox.Valida, "Nap:Outbox:IntervaloSegundos deve ser maior que zero.")
            .ValidateOnStart();
        servicos.AddOptions<OpcoesArquivos>().Bind(configuracao.GetSection("Nap:Arquivos"))
            .Validate(o => o.LimiteBytes > 0, "Nap:Arquivos:LimiteBytes deve ser maior que zero.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Diretorio), "Nap:Arquivos:Diretorio deve ser configurado.")
            .ValidateOnStart();
        // A Application recebe a instância (sem dependência de IOptions); os validadores acima rodam no primeiro .Value.
        servicos.AddSingleton(sp => sp.GetRequiredService<IOptions<OpcoesArquivos>>().Value);
        servicos.AddOptions<OpcoesJwt>().Bind(configuracao.GetSection("Jwt"))
            .Validate(OpcoesJwt.ChaveValida, "Jwt:Chave deve ter pelo menos 32 bytes; configure-a fora do código (segredo do ambiente).")
            .ValidateOnStart();

        // Sem EnableRetryOnFailure: incompatível com as transações manuais de UnidadeDeTrabalho.
        servicos.AddDbContext<NapDbContext>(o => o.UseSqlServer(configuracao.GetConnectionString("Nap")
            ?? throw new InvalidOperationException("Connection string 'Nap' não configurada.")));

        servicos.AddIdentityCore<UsuarioIdentidade>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<NapDbContext>();

        // Uma única conexão Redis, compartilhada pelo cache de consultas e pelo IDistributedCache. A string é exigida já
        // no registro: sem ela, o cache de consultas (singleton) não resolveria e toda escrita e o painel dariam 500.
        var stringRedis = configuracao.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' não configurada.");
        servicos.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redis = ConfigurationOptions.Parse(stringRedis);
            redis.AbortOnConnectFail = false;
            // Desconectado, falha na hora (sem fila de 5 s): o chamador cai no banco/fonte em vez de travar a requisição.
            redis.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(redis);
        });
        servicos.AddStackExchangeRedisCache(_ => { });
        servicos.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IServiceProvider>((o, sp) => o.ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>()));

        servicos.AddHttpContextAccessor();
        servicos.AddSingleton<IRelogio, Relogio>();
        servicos.AddSingleton<ICacheConsultas, CacheConsultasRedis>();
        servicos.AddScoped<IUsuarioAtual, UsuarioAtual>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        servicos.AddScoped<IAuditoria, Auditoria>();
        servicos.AddScoped<IChavesIdempotencia, ChavesIdempotencia>();
        servicos.AddSingleton<IEnvioEmail, EnvioEmailNaoConfigurado>();
        servicos.AddSingleton<IServicoAssinatura, AssinaturaDemonstrativa>();
        ResolvedorFontes.Configurar();
        servicos.AddSingleton<IGeradorPdfPei, GeradorPdfPei>();
        servicos.AddSingleton<IGeradorPdfProducao, GeradorPdfProducao>();
        servicos.AddSingleton<IGeradorExcelProducao, GeradorExcelProducao>();
        // Relativo ao content root, nunca ao diretório corrente do processo.
        servicos.AddSingleton<IArmazenamentoArquivos>(sp => new ArmazenamentoEmDisco(Path.GetFullPath(Path.Combine(
            sp.GetRequiredService<IHostEnvironment>().ContentRootPath, sp.GetRequiredService<IOptions<OpcoesArquivos>>().Value.Diretorio)),
            sp.GetRequiredService<ILogger<ArmazenamentoEmDisco>>()));

        servicos.AddScoped<IConfiguracaoAlertas, ConfiguracaoAlertasRepositorio>();
        servicos.AddScoped<IAlunoRepository, AlunoRepository>();
        servicos.AddScoped<IAgendaRepository, AgendaRepository>();
        servicos.AddScoped<IEscalaRepository, EscalaRepository>();
        servicos.AddScoped<IFeriadoRepository, FeriadoRepository>();
        servicos.AddScoped<ICursoRepository, CursoRepository>();
        servicos.AddScoped<IAtendimentoRepository, AtendimentoRepository>();
        servicos.AddScoped<IAtividadeRepository, AtividadeRepository>();
        servicos.AddScoped<IFechamentoRepository, FechamentoRepository>();
        servicos.AddScoped<IAjusteProducaoRepository, AjusteProducaoRepository>();
        servicos.AddScoped<IEncaminhamentoRepository, EncaminhamentoRepository>();
        servicos.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        servicos.AddScoped<IMensagemRepository, MensagemRepository>();
        servicos.AddScoped<IDocumentoRepository, DocumentoRepository>();
        servicos.AddScoped<IPeiRepository, PeiRepository>();
        servicos.AddScoped<IAcessoAssinaturaRepository, AcessoAssinaturaRepository>();
        servicos.AddScoped<IAlunoQueries, AlunoQueries>();
        servicos.AddScoped<IAgendaQueries, AgendaQueries>();
        servicos.AddScoped<IAtendimentoQueries, AtendimentoQueries>();
        servicos.AddScoped<IAtividadeQueries, AtividadeQueries>();
        servicos.AddScoped<IAuditoriaQueries, AuditoriaQueries>();
        servicos.AddScoped<IComunicacaoQueries, ComunicacaoQueries>();
        servicos.AddScoped<ICursoQueries, CursoQueries>();
        servicos.AddScoped<IDocumentoQueries, DocumentoQueries>();
        servicos.AddScoped<IEncaminhamentoQueries, EncaminhamentoQueries>();
        servicos.AddScoped<INotificacaoQueries, NotificacaoQueries>();
        servicos.AddScoped<IPeiQueries, PeiQueries>();
        servicos.AddScoped<IPainelQueries, PainelQueries>();
        servicos.AddScoped<IProducaoQueries, ProducaoQueries>();
        servicos.AddScoped<IUsuarioQueries, UsuarioQueries>();

        servicos.AddSingleton<ServicoTokens>();
        servicos.AddScoped<ValidadorCarimboSeguranca>();
        servicos.AddScoped<ServicoSessao>();
        servicos.AddScoped<IServicoSessao>(sp => sp.GetRequiredService<ServicoSessao>());
        servicos.AddScoped<IGestaoUsuarios, GestaoUsuarios>();
        servicos.AddScoped<SemeadorDemonstracao>();
        return servicos;
    }
}
