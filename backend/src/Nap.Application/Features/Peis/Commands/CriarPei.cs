using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum.ValueObjects;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis.Commands;

/// <summary>Conteúdo editável de uma versão; limites e vigência são validados pelo domínio (422).</summary>
public record ConteudoPeiInput(string? DemandaInterna, string? NecessidadesEducacionais, string? Adaptacoes,
    string? OrientacoesProfessores, string? RecursosApoio, string? CuidadosPedagogicos, DateOnly VigenciaInicio,
    DateOnly VigenciaFim)
{
    public ConteudoPei ParaDominio() => new(DemandaInterna, NecessidadesEducacionais, Adaptacoes, OrientacoesProfessores,
        RecursosApoio, CuidadosPedagogicos, PeriodoVigencia.Criar(VigenciaInicio, VigenciaFim));
}

public record CriarPeiCommand(Guid AlunoId, Guid ProfissionalResponsavelId, ConteudoPeiInput Conteudo);

public class CriarPeiValidator : AbstractValidator<CriarPeiCommand>
{
    public CriarPeiValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty().WithMessage("Informe o aluno.");
        RuleFor(x => x.ProfissionalResponsavelId).NotEmpty().WithMessage("Informe o profissional responsável.");
        RuleFor(x => x.Conteudo).NotNull().WithMessage("Informe o conteúdo do PEI.");
    }
}

public sealed class CriarPeiHandler(IValidator<CriarPeiCommand> validador, IPeiRepository peis, IAlunoRepository alunos,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow,
    ICacheConsultas cache)
{
    /// <returns>O id da versão 1.</returns>
    public async Task<Guid> Handle(CriarPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        if (!AutorizacaoPei.PodeElaborar(usuario)) throw new ForbiddenException();

        var aluno = await alunos.ObterAsync(comando.AlunoId, ct)
            ?? throw Validacao.ErroDeCampo("alunoId", "Aluno inexistente.");
        if (!await usuarios.EhProfissionalAtivoAsync(comando.ProfissionalResponsavelId, ct))
            throw Validacao.ErroDeCampo("profissionalResponsavelId", "O responsável deve ser um profissional ativo da equipe NAP.");

        var pei = Pei.Criar(aluno.Id, comando.ProfissionalResponsavelId, comando.Conteudo.ParaDominio(), usuario.Id,
            relogio.Agora);
        peis.Adicionar(pei);
        auditoria.Registrar("pei.criar", "Pei", pei.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return pei.Versoes.Single().Id;
    }
}
