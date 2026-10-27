using FluentValidation;
using Nap.Application.Comum;

namespace Nap.Application.Features.Documentos.Queries;

/// <summary><c>IncluirArquivados</c> só vale para Administrador/CoordenadorNap; os demais perfis o têm ignorado.</summary>
public record ListarDocumentosQuery(Guid AlunoId, bool IncluirArquivados = false, int Pagina = 1, int Tamanho = 20);

public class ListarDocumentosValidator : AbstractValidator<ListarDocumentosQuery>
{
    public ListarDocumentosValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.Pagina).InclusiveBetween(1, 100_000);
        RuleFor(x => x.Tamanho).InclusiveBetween(1, 100);
    }
}

public sealed class ListarDocumentosHandler(IValidator<ListarDocumentosQuery> validador, IDocumentoQueries consultas,
    IUsuarioAtual usuario, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<PagedResult<DocumentoDto>> Handle(ListarDocumentosQuery query, CancellationToken ct = default)
    {
        await validador.ValidarAsync(query, ct);
        var escopo = EscopoDocumentos.Para(usuario, query.IncluirArquivados);
        var pagina = await consultas.ListarAsync(query.AlunoId, escopo, query.Pagina, query.Tamanho, ct)
            ?? throw new NotFoundException("Aluno");
        // Descrições e nomes de arquivo (laudos, relatórios) são sensíveis: a leitura fica no rastro, como na linha do tempo.
        auditoria.Registrar("documento.lista.consulta", "Aluno", query.AlunoId);
        await uow.SalvarAsync(ct);
        return pagina;
    }
}
