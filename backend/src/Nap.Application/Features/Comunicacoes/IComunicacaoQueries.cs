using Nap.Application.Comum;

namespace Nap.Application.Features.Comunicacoes;

/// <summary>Toda consulta parte do recorte do usuário: rascunho só do autor; enviada só dos participantes que ainda alcançam
/// os vínculos dela com o perfil e o curso de hoje (<see cref="RegrasComunicacao.AlcancaVinculos"/>).</summary>
public interface IComunicacaoQueries
{
    Task<PagedResult<MensagemListaDto>> ListarAsync(IUsuarioAtual leitor, CaixaMensagens caixa, string? busca, Guid? alunoId, Guid? encaminhamentoId,
        int pagina, int tamanho, CancellationToken ct);

    /// <summary><c>null</c> quando não existe ou o usuário não a pode ver.</summary>
    Task<MensagemDetalheDto?> ObterAsync(Guid id, IUsuarioAtual leitor, CancellationToken ct);

    /// <summary>Recebidas, enviadas, não lidas e não arquivadas pelo destinatário.</summary>
    Task<int> ContarNaoLidasAsync(IUsuarioAtual leitor, CancellationToken ct);
}
