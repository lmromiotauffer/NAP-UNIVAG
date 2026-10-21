using Nap.Application.Comum;
using Nap.Application.Features.Notificacoes;

namespace Nap.Application.Features.Comunicacoes.Queries;

public sealed class ContarMensagensNaoLidasHandler(IComunicacaoQueries consultas, IUsuarioAtual usuario)
{
    public async Task<ContagemNaoLidasDto> Handle(CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        return new(await consultas.ContarNaoLidasAsync(usuario.Id, ct));
    }
}
