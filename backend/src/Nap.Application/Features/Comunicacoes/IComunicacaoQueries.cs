using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes;

/// <summary>Toda consulta parte do recorte do usuário: rascunho só do autor; enviada só dos participantes.</summary>
public interface IComunicacaoQueries
{
    Task<PagedResult<MensagemListaDto>> ListarAsync(Guid usuarioId, CaixaMensagens caixa, string? busca, Guid? alunoId,
        int pagina, int tamanho, CancellationToken ct);

    /// <summary><c>null</c> quando não existe ou o usuário não a pode ver.</summary>
    Task<MensagemDetalheDto?> ObterAsync(Guid id, Guid usuarioId, CancellationToken ct);

    /// <summary>Recebidas, enviadas, não lidas e não arquivadas pelo destinatário.</summary>
    Task<int> ContarNaoLidasAsync(Guid usuarioId, CancellationToken ct);
}
