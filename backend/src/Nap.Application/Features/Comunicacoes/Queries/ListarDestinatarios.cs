using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Encaminhamentos;
using Nap.Application.Features.Usuarios;

namespace Nap.Application.Features.Comunicacoes.Queries;

/// <summary>Quem pode receber uma mensagem com estes vínculos: mesma validação e mesma regra do envio.</summary>
public record ListarDestinatariosQuery(Guid? AlunoId = null, Guid? EncaminhamentoId = null, Guid? CursoId = null);

public sealed class ListarDestinatariosHandler(IAlunoRepository alunos, IEncaminhamentoRepository encaminhamentos,
    ICursoQueries cursos, IUsuarioQueries usuarios, IUsuarioAtual usuario)
{
    public async Task<IReadOnlyList<DestinatarioDto>> Handle(ListarDestinatariosQuery query, CancellationToken ct = default)
    {
        RegrasComunicacao.ExigirAcesso(usuario);
        var v = await RegrasComunicacao.ValidarVinculosAsync(usuario, query.CursoId, query.AlunoId, query.EncaminhamentoId, alunos,
            encaminhamentos, cursos, ct);
        return (await usuarios.ListarAtivosAsync(ct))
            .Where(d => RegrasComunicacao.PodeReceber(usuario.Id, d, v))
            .Select(d => new DestinatarioDto(d.Id, d.Nome, d.Perfil))
            .ToList();
    }
}
