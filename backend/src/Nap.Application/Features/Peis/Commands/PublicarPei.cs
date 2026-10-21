using System.Globalization;
using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Application.Features.Comunicacoes;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;
using Nap.Domain.Comunicacoes;

namespace Nap.Application.Features.Peis.Commands;

public record PublicarPeiCommand(Guid VersaoId, string Versao);

public class PublicarPeiValidator : AbstractValidator<PublicarPeiCommand>
{
    public PublicarPeiValidator() => RuleFor(x => x.VersaoId).NotEmpty();
}

public sealed class PublicarPeiHandler(IValidator<PublicarPeiCommand> validador, IPeiRepository peis, IAlunoRepository alunos,
    IUsuarioQueries usuarios, IMensagemRepository mensagens, IUsuarioAtual usuario, IRelogio relogio, IAuditoria auditoria,
    IUnidadeDeTrabalho uow, ICacheConsultas cache)
{
    public async Task Handle(PublicarPeiCommand comando, CancellationToken ct = default)
    {
        AutorizacaoPei.ExigirAcesso(usuario);
        await validador.ValidarAsync(comando, ct);
        var (pei, versao) = await AutorizacaoPei.ObterVersaoAsync(peis, usuario, comando.VersaoId, ct);
        if (!AutorizacaoPei.PodeRevisarEPublicar(usuario)) throw new ForbiddenException();
        uow.ExigirVersao(versao, comando.Versao);

        var aluno = await alunos.ObterAsync(pei.AlunoId, ct) ?? throw new NotFoundException("Aluno");
        var agora = relogio.Agora;
        pei.Publicar(versao, usuario.Id, aluno.CursoAtualId, agora);

        // Sem coordenador ativo publica do mesmo jeito; o painel da coordenação NAP acusa o PEI sem destinatário.
        var coordenadores = await usuarios.ListarAtivosPorPerfilAsync(Perfis.CoordenadorCurso, aluno.CursoAtualId, ct);
        var assunto = Texto.Truncar($"PEI publicado: {aluno.NomeCompleto} (versão {versao.Numero})", 200);
        var vigencia = versao.Conteudo.Vigencia;
        var inicio = vigencia.Inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var fim = vigencia.Fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var corpo = $"O PEI de {aluno.NomeCompleto}, vigente de {inicio} a {fim}, foi publicado. "
            + "Abra a ficha do aluno, aba PEI, para ler e registrar ciência.";
        foreach (var coordenador in coordenadores)
            mensagens.Adicionar(Mensagem.CriarEnvioPei(usuario.Id, coordenador, assunto, corpo, aluno.CursoAtualId, aluno.Id,
                versao.Id, agora));

        auditoria.Registrar("pei.publicar", "VersaoPei", versao.Id);
        await uow.SalvarAsync(ct);
        await cache.InvalidarAreaAsync(ct);
    }
}
