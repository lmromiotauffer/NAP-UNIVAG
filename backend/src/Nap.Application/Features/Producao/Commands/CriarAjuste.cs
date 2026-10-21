using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Features.Producao.Commands;

public record CriarAjusteCommand(Guid ProfissionalId, DateOnly Data, CategoriaProducao Categoria, int? Ocorrencias, int? Minutos,
    string Justificativa);

public class CriarAjusteValidator : AbstractValidator<CriarAjusteCommand>
{
    public CriarAjusteValidator()
    {
        RuleFor(x => x.ProfissionalId).NotEmpty();
        RuleFor(x => x.Categoria).IsInEnum().WithMessage("Categoria inválida.");
        RuleFor(x => x.Data).Must(d => d.Year is >= 2000 and <= 2100).WithMessage("A data deve estar entre 2000 e 2100.");
        RuleFor(x => x.Justificativa).NotEmpty().WithMessage("Informe a justificativa do ajuste.")
            .Must(j => j is null || j.Trim().Length is >= 5 and <= 500).WithMessage("A justificativa deve ter de 5 a 500 caracteres.");
    }
}

public sealed class CriarAjusteHandler(IValidator<CriarAjusteCommand> validador, IAjusteProducaoRepository ajustes,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow,
    ICacheConsultas cache, ServicoFechamento fechamento)
{
    public async Task<Guid> Handle(CriarAjusteCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        if (!usuario.EhCoordenacao()) throw new ForbiddenException();

        // Ativo, ou inativo ajustado em data anterior à desativação.
        var profissional = (await usuarios.ListarProfissionaisDaProducaoAsync(comando.Data, comando.Data, relogio.Fuso, comando.ProfissionalId, ct))
            .FirstOrDefault();
        if (profissional is null || (!profissional.Ativo && comando.Data >= (profissional.DesativadoEm ?? DateOnly.MinValue)))
            throw Validacao.ErroDeCampo("profissionalId", "O profissional deve ser um membro da equipe NAP ativo na data do ajuste.");

        var ajuste = AjusteProducao.Criar(comando.ProfissionalId, comando.Data, comando.Categoria, comando.Ocorrencias, comando.Minutos,
            comando.Justificativa, usuario.Id, relogio.Agora);
        await uow.EmTransacaoAsync(async tct =>
        {
            await fechamento.ExigirSemanaAbertaAsync(comando.ProfissionalId, comando.Data, tct);
            ajustes.Adicionar(ajuste);
            auditoria.Registrar("producao.ajuste", "AjusteProducao", ajuste.Id);
            await uow.SalvarAsync(tct);
            return ajuste.Id;
        }, ct);
        await cache.InvalidarAreaAsync(ct);
        return ajuste.Id;
    }
}
