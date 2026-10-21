using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Peis.Commands;

/// <summary>Assinaturas já colhidas permanecem; a assinatura profissional pendente passa a ser do novo responsável.</summary>
public record TrocarResponsavelPeiCommand(Guid PeiId, Guid NovoResponsavelId, string Justificativa);

public class TrocarResponsavelPeiValidator : AbstractValidator<TrocarResponsavelPeiCommand>
{
    public TrocarResponsavelPeiValidator()
    {
        RuleFor(x => x.PeiId).NotEmpty();
        RuleFor(x => x.NovoResponsavelId).NotEmpty().WithMessage("Informe o novo responsável.");
        RuleFor(x => x.Justificativa).Must(t => t?.Trim().Length is >= 3 and <= 500)
            .WithMessage("A justificativa deve ter de 3 a 500 caracteres.");
    }
}

public sealed class TrocarResponsavelPeiHandler(IValidator<TrocarResponsavelPeiCommand> validador, IPeiRepository peis,
    IUsuarioQueries usuarios, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow,
    ICacheConsultas cache)
{
    public async Task Handle(TrocarResponsavelPeiCommand comando, CancellationToken ct = default)
    {
        // Só a coordenação (que vê todos os PEIs) troca: o 403 vem antes de qualquer busca.
        if (!AutorizacaoPei.PodeRevisarEPublicar(usuario)) throw new ForbiddenException();
        await validador.ValidarAsync(comando, ct);
        var pei = await peis.ObterAsync(comando.PeiId, ct) ?? throw new NotFoundException("PEI");
        if (!await usuarios.EhProfissionalAtivoAsync(comando.NovoResponsavelId, ct))
            throw Validacao.ErroDeCampo("novoResponsavelId", "O responsável deve ser um profissional ativo da equipe NAP.");

        pei.TrocarResponsavel(comando.NovoResponsavelId, comando.Justificativa, usuario.Id, relogio.Agora);
        auditoria.Registrar("pei.responsavel.trocar", "Pei", pei.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
