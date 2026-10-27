using System.Globalization;
using System.Text;
using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Peis;

namespace Nap.Application.Features.Peis.Commands;

public record AssinarPeiAlunoCommand(string Token, string Codigo, string NomeCompleto, bool Aceite);

public class AssinarPeiAlunoValidator : AbstractValidator<AssinarPeiAlunoCommand>
{
    public AssinarPeiAlunoValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Codigo).NotEmpty().WithMessage("Informe o código.").MaximumLength(10);
        RuleFor(x => x.NomeCompleto).Must(n => n?.Trim().Length is >= 3 and <= 200)
            .WithMessage(AssinarPeiAlunoHandler.MensagemNome);
        RuleFor(x => x.Aceite).Equal(true).WithMessage("Confirme que leu e concorda com o PEI.");
    }
}

/// <summary>Anônimo: o acesso temporário substitui a autenticação; auditoria só com o id do acesso.</summary>
public sealed class AssinarPeiAlunoHandler(IValidator<AssinarPeiAlunoCommand> validador, IAcessoAssinaturaRepository acessos,
    IPeiRepository peis, IAlunoRepository alunos, IServicoAssinatura assinaturas, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public const string MensagemNome = "Digite seu nome completo como está no cadastro.";

    public async Task Handle(AssinarPeiAlunoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var agora = relogio.Agora;
        var (acesso, pei, versao) = await AcessoAluno.AbrirAsync(acessos, peis, uow, comando.Token, comando.Codigo, agora, ct);
        // Só depois do código certo: sem ele, o nome do aluno não pode ser sondado.
        var aluno = await alunos.ObterAsync(pei.AlunoId, ct) ?? throw AcessoAluno.LinkInvalido();
        if (Normalizar(comando.NomeCompleto) != Normalizar(aluno.NomeCompleto))
            throw Validacao.ErroDeCampo("nomeCompleto", MensagemNome);

        var hash = acesso.HashConteudo;
        var evidencia = await assinaturas.AssinarAsync(versao.Id, PapelAssinatura.Aluno, aluno.NomeCompleto, hash, agora, ct);
        versao.RegistrarAssinatura(PapelAssinatura.Aluno, null, aluno.NomeCompleto, hash, evidencia.Evidencia, agora, evidencia.Metodo);
        acesso.MarcarUsado(agora);
        auditoria.Registrar("pei.assinar", "AcessoAssinaturaPei", acesso.Id);
        for (var repeticao = 0; ; repeticao++)
        {
            try
            {
                await uow.SalvarAsync(ct);
                break;
            }
            catch (ConflictException)
            {
                // Outra assinatura com o mesmo link gravou antes: para quem chega depois, o link já foi usado.
                if (!await acessos.RecarregarAsync(acesso, ct) || !acesso.Valido(agora)) throw AcessoAluno.LinkInvalido();
                // Ainda válido: uma tentativa errada simultânea gravou o acesso. O código já conferiu; a releitura desfez
                // o uso, então marca de novo e grava uma vez mais.
                if (repeticao > 0) throw new ConflictException("Não foi possível registrar agora. Tente novamente.");
                acesso.MarcarUsado(agora);
            }
        }
        await cache.InvalidarAreaAsync(ct);
    }

    // Sem diferença de caixa, acentos, espaços extras, apóstrofos tipográficos (teclado do celular) ou travessões.
    private static string Normalizar(string nome)
    {
        var decomposto = string.Join(' ', nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormD)
            .Replace('\u2019', '\'').Replace('\u2018', '\'').Replace('\u02BC', '\'')
            .Replace('\u2013', '-').Replace('\u2014', '-').Replace('\u2010', '-');
        var semAcentos = new StringBuilder(decomposto.Length);
        foreach (var ch in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) semAcentos.Append(ch);
        return semAcentos.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
