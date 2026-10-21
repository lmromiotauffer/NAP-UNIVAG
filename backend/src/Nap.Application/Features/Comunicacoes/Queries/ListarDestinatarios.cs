using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Comunicacoes.Queries;

/// <summary>Quem pode receber uma mensagem com estes vínculos: mesma validação e mesma regra do envio.</summary>
public record ListarDestinatariosQuery(Guid? AlunoId = null, Guid? EncaminhamentoId = null);

public sealed class ListarDestinatariosHandler(IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos,
    ICursoQueries cursos, IUsuarioQueries usuarios, IUsuarioAtual usuario)
{
    public async Task<IReadOnlyList<DestinatarioDto>> Handle(ListarDestinatariosQuery query, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        var v = await RegrasComunicacao.ValidarVinculosAsync(usuario, null, query.AlunoId, query.EncaminhamentoId, alunos,
            encaminhamentos, cursos, ct);
        // ponytail: filtra em memória todos os usuários ativos; vira consulta filtrada se o cadastro passar de alguns milhares.
        return (await usuarios.ListarAtivosAsync(ct))
            .Where(d => RegrasComunicacao.PodeReceber(usuario.Id, d, v))
            .Select(d => new DestinatarioDto(d.Id, d.Nome, d.Perfil))
            .ToList();
    }
}
