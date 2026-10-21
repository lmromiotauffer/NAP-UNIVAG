using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;

namespace Nap.Application.Features.Peis.Queries;

/// <summary>Só o que o aluno assina: nome, número, campos pedagógicos e vigência (sem demanda interna) e o hash.</summary>
public record VersaoParaAssinaturaDto(string Aluno, int Numero, ConteudoPeiDto Conteudo, string HashConteudo,
    DateTimeOffset ExpiraEm, string AvisoAssinatura);

public record AbrirAcessoAssinaturaQuery(string Token, string Codigo);

public class AbrirAcessoAssinaturaValidator : AbstractValidator<AbrirAcessoAssinaturaQuery>
{
    public AbrirAcessoAssinaturaValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Codigo).NotEmpty().WithMessage("Informe o código.").MaximumLength(10);
    }
}

/// <summary>Anônimo: sem usuário autenticado; não grava nada além da tentativa de código errado.</summary>
public sealed class AbrirAcessoAssinaturaHandler(IValidator<AbrirAcessoAssinaturaQuery> validador,
    IAcessoAssinaturaRepository acessos, IPeiRepository peis, IAlunoRepository alunos, IRelogio relogio, IUnidadeDeTrabalho uow)
{
    public async Task<VersaoParaAssinaturaDto> Handle(AbrirAcessoAssinaturaQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var (acesso, pei, versao) = await AcessoAluno.AbrirAsync(acessos, peis, uow, query.Token, query.Codigo, relogio.Agora, ct);
        var aluno = await alunos.ObterAsync(pei.AlunoId, ct) ?? throw AcessoAluno.LinkInvalido();
        var c = versao.Conteudo;
        var conteudo = new ConteudoPeiDto(null, c.NecessidadesEducacionais, c.Adaptacoes, c.OrientacoesProfessores,
            c.RecursosApoio, c.CuidadosPedagogicos, c.Vigencia.Inicio, c.Vigencia.Fim);
        return new VersaoParaAssinaturaDto(aluno.NomeCompleto, versao.Numero, conteudo, acesso.HashConteudo, acesso.ExpiraEm,
            AcessoAluno.AvisoAssinatura);
    }
}
