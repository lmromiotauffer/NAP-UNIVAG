using Nap.Application.Features.Peis;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;

namespace Nap.Application.Tests;

/// <summary>Garante que os botões do detalhe (<see cref="AutorizacaoPei.Acoes"/>) acompanham o que o domínio de fato aceita.</summary>
public class PeiAcoesParidadeTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Curso = Guid.NewGuid();

    private static ConteudoPei Conteudo() => new("Demanda", "Necessidades", null, null, null, null,
        PeriodoVigencia.Criar(new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31)));

    private static void Publicar(Pei pei, VersaoPei v, Guid prof, Guid coord, DateTimeOffset t, bool soAte = false)
    {
        v.EnviarParaRevisao(t);
        var hash = CalculadoraHashPei.Calcular(v.Conteudo);
        v.AprovarRevisao(coord, hash, t);
        v.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "P", hash, "e", t);
        v.RegistrarAssinatura(PapelAssinatura.Aluno, null, "A", hash, "e", t);
        if (!soAte) pei.Publicar(v, coord, Curso, t);
    }

    /// <summary>PEI cuja versão alvo está no estado pedido; <paramref name="profAssinou"/> deixa só a assinatura do profissional.</summary>
    private static (Pei Pei, VersaoPei Versao) Montar(EstadoVersaoPei estado, Guid prof, Guid coord, bool profAssinou = false)
    {
        var pei = Pei.Criar(Guid.NewGuid(), prof, Conteudo(), prof, Agora);
        var v = pei.Versoes.Single();
        var hash = CalculadoraHashPei.Calcular(v.Conteudo);
        switch (estado)
        {
            case EstadoVersaoPei.EmRevisao: v.EnviarParaRevisao(Agora); break;
            case EstadoVersaoPei.AguardandoAssinaturas:
                v.EnviarParaRevisao(Agora);
                v.AprovarRevisao(coord, hash, Agora);
                if (profAssinou) v.RegistrarAssinatura(PapelAssinatura.Profissional, prof, "P", hash, "e", Agora);
                break;
            case EstadoVersaoPei.Assinada: Publicar(pei, v, prof, coord, Agora, soAte: true); break;
            case EstadoVersaoPei.Publicada: Publicar(pei, v, prof, coord, Agora); break;
            case EstadoVersaoPei.Arquivada:
                Publicar(pei, v, prof, coord, Agora);
                var v2 = pei.NovaVersao(prof, Agora);
                Publicar(pei, v2, prof, coord, Agora);
                break;
        }
        return (pei, v);
    }

    private static bool Funciona(Action acao)
    {
        try { acao(); return true; }
        catch (DomainException) { return false; }
    }

    public static IEnumerable<object[]> Casos() =>
        Enum.GetValues<EstadoVersaoPei>().SelectMany(e => new[] { new object[] { e, false }, new object[] { e, true } });

    [Theory]
    [MemberData(nameof(Casos))]
    public void Flags_do_detalhe_igualam_o_que_o_dominio_aceita(EstadoVersaoPei estado, bool profAssinou)
    {
        // Coordenação (também Equipe NAP) e responsável: as permissões de perfil ficam todas liberadas.
        var usuario = new UsuarioAtualFalso(Perfis.CoordenadorNap);
        var coordCurso = new UsuarioAtualFalso(Perfis.CoordenadorCurso, Curso);
        var coord = Guid.NewGuid();

        (Pei Pei, VersaoPei Versao) Novo() => Montar(estado, usuario.Id, coord, profAssinou);
        var (pei, v) = Novo();
        var situacao = new AutorizacaoPei.SituacaoVersao(pei.ProfissionalResponsavelId, v.CursoPublicacaoId, v.Estado,
            v.Conteudo.TemConteudoPedagogico, v.Assinaturas.Any(a => a.Papel == PapelAssinatura.Profissional),
            v.Assinaturas.Any(a => a.Papel == PapelAssinatura.Aluno), Pei.EhMaisRecente(v.Numero, pei.Versoes.Max(x => x.Numero)),
            pei.VersaoAberta is not null, false);
        var acoes = AutorizacaoPei.Acoes(usuario, situacao);
        var hash = CalculadoraHashPei.Calcular(v.Conteudo);

        Assert.Equal(Funciona(() => Novo().Versao.AtualizarConteudo(Conteudo(), Agora)), acoes.PodeEditar);
        Assert.Equal(Funciona(() => Novo().Versao.EnviarParaRevisao(Agora)), acoes.PodeEnviarRevisao);
        Assert.Equal(Funciona(() => Novo().Versao.AprovarRevisao(coord, hash, Agora)), acoes.PodeAprovar);
        Assert.Equal(Funciona(() => Novo().Versao.Devolver("Ajustar o texto", Agora)), acoes.PodeDevolver);
        Assert.Equal(Funciona(() => Novo().Versao.RegistrarAssinatura(PapelAssinatura.Profissional, usuario.Id, "P", hash, "e", Agora)),
            acoes.PodeAssinarComoProfissional);
        Assert.Equal(Funciona(() => Novo().Versao.RegistrarAssinatura(PapelAssinatura.Aluno, null, "A", hash, "e", Agora)),
            acoes.PodeGerarAcessoAluno);
        Assert.Equal(Funciona(() => { var n = Novo(); n.Pei.Publicar(n.Versao, coord, Curso, Agora); }), acoes.PodePublicar);
        Assert.Equal(Funciona(() => Novo().Versao.Arquivar("Motivo válido", Agora)), acoes.PodeArquivar);
        Assert.Equal(Funciona(() => Novo().Pei.NovaVersao(usuario.Id, Agora)), acoes.PodeCriarNovaVersao);
        var comCiencia = AutorizacaoPei.Acoes(coordCurso, situacao with { CursoPublicacaoId = v.CursoPublicacaoId ?? Curso });
        Assert.Equal(v.CursoPublicacaoId is not null && Funciona(() => Novo().Versao.RegistrarCiencia(Guid.NewGuid(), Agora)),
            comCiencia.PodeRegistrarCiencia);
    }
}
