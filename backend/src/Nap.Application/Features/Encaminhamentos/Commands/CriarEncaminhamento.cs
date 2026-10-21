using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Encaminhamentos;

namespace Nap.Application.Features.Encaminhamentos.Commands;

public record CriarEncaminhamentoCommand(Guid AlunoId, Especialidade Especialidade, DestinoEncaminhamento Destino,
    DateOnly Data, string MotivoRestrito, string ResumoCompartilhavel, string? Observacoes);

public class CriarEncaminhamentoValidator : AbstractValidator<CriarEncaminhamentoCommand>
{
    public CriarEncaminhamentoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty().WithMessage("Informe o aluno.");
        RuleFor(x => x.Especialidade).IsInEnum();
        RuleFor(x => x.Destino).IsInEnum();
        RuleFor(x => x.Data).NotEqual(default(DateOnly)).WithMessage("Informe a data do encaminhamento.");
        RuleFor(x => x.MotivoRestrito).NotEmpty().WithMessage("Informe o motivo.").Length(3, 4000);
        RuleFor(x => x.ResumoCompartilhavel).NotEmpty().WithMessage("Informe o resumo compartilhável.").Length(3, 2000);
        RuleFor(x => x.Observacoes).MaximumLength(4000);
    }
}

public sealed class CriarEncaminhamentoHandler(IValidator<CriarEncaminhamentoCommand> validador,
    IEncaminhamentoRepository encaminhamentos, IAlunoRepository alunos, IUsuarioAtual usuario, IRelogio relogio,
    IAuditoria auditoria, IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task<Guid> Handle(CriarEncaminhamentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhEquipeNap()) throw new ForbiddenException();

        var aluno = await alunos.ObterAsync(comando.AlunoId, ct)
            ?? throw Validacao.ErroDeCampo("alunoId", "Aluno inexistente.");

        var e = Encaminhamento.Criar(aluno.Id, aluno.CursoAtualId, usuario.Id, comando.Especialidade, comando.Destino,
            comando.Data, comando.MotivoRestrito, comando.ResumoCompartilhavel, comando.Observacoes, relogio.Hoje, relogio.Agora);
        encaminhamentos.Adicionar(e);
        auditoria.Registrar("encaminhamento.criar", "Encaminhamento", e.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
        return e.Id;
    }
}
