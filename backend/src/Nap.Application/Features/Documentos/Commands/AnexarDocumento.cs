using System.Security.Cryptography;
using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Alunos;
using Nap.Domain.Comum;
using Nap.Domain.Documentos;

namespace Nap.Application.Features.Documentos.Commands;

/// <summary>O chamador é dono do <c>Conteudo</c>; o handler só o lê.</summary>
public record AnexarDocumentoCommand(Guid AlunoId, CategoriaDocumento Categoria, string Descricao, string NomeArquivo, Stream Conteudo);

public class AnexarDocumentoValidator : AbstractValidator<AnexarDocumentoCommand>
{
    public AnexarDocumentoValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.Categoria).IsInEnum();
        // Antes de gravar o arquivo: evita escrever e compensar por um erro de entrada.
        RuleFor(x => x.Descricao).Must(d => d?.Trim().Length is >= 3 and <= 500)
            .WithMessage("A descrição deve ter de 3 a 500 caracteres.");
        RuleFor(x => x.NomeArquivo).NotEmpty().OverridePropertyName("arquivo").WithMessage("Envie um arquivo.");
        RuleFor(x => x.Conteudo).NotNull().OverridePropertyName("arquivo").WithMessage("Envie um arquivo.");
    }
}

public sealed class AnexarDocumentoHandler(IValidator<AnexarDocumentoCommand> validador, IDocumentoRepository documentos,
    IAlunoRepository alunos, IArmazenamentoArquivos armazenamento, OpcoesArquivos opcoes, IUsuarioAtual usuario,
    IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    public async Task<Guid> Handle(AnexarDocumentoCommand comando, CancellationToken ct = default)
    {
        await validador.ValidarAsync(comando, ct);
        var coordenadorCurso = usuario.Perfil == Perfis.CoordenadorCurso;
        if (!usuario.EhEquipeNap() && !(coordenadorCurso && usuario.CursoId is not null)) throw new ForbiddenException();
        var aluno = await alunos.ObterAsync(comando.AlunoId, ct) ?? throw new NotFoundException("Aluno");
        // Mesmo 404 das consultas: o coordenador de curso não distingue aluno inexistente de aluno de outro curso.
        if (coordenadorCurso && aluno.CursoAtualId != usuario.CursoId) throw new NotFoundException("Aluno");

        // Um byte além do limite basta para a validação acusar o excesso sem trazer o corpo inteiro para a memória.
        var conteudo = await LerAteAsync(comando.Conteudo, opcoes.LimiteBytes + 1, ct);
        var arquivo = ValidacaoArquivo.Validar(comando.NomeArquivo, conteudo, opcoes.LimiteBytes);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo));

        string chave;
        using (var origem = new MemoryStream(conteudo, writable: false))
            chave = await armazenamento.SalvarAsync(origem, ct);
        var d = Documento.Criar(aluno.Id, aluno.CursoAtualId, comando.Categoria, comando.Descricao, comando.NomeArquivo,
            arquivo.TipoConteudo, conteudo.Length, sha256, chave, usuario.Id, relogio.Agora);
        // Restrito ao NAP ficaria invisível ao próprio autor (EscopoDocumentos): o que a coordenação envia já nasce compartilhado.
        if (coordenadorCurso) d.Compartilhar("Enviado pela coordenação do curso.", usuario.Id, aluno.CursoAtualId, relogio.Agora);
        try
        {
            documentos.Adicionar(d);
            auditoria.Registrar("documento.anexar", "Documento", d.Id);
            await uow.SalvarAsync(ct);
            return d.Id;
        }
        catch
        {
            // Compensação: sem o registro ninguém alcança o arquivo. Sem token da requisição (cancelada, abortaria a limpeza).
            // O commit pode ter acontecido com a resposta perdida: só remove se o registro de fato não existe; na dúvida
            // (banco fora ao conferir), um arquivo órfão é melhor que um documento sem arquivo.
            try
            {
                if (!await documentos.ExisteAsync(d.Id, CancellationToken.None))
                    await armazenamento.RemoverAsync(chave, CancellationToken.None);
            }
            catch (Exception) { } // o armazenamento já loga a falha; ela não pode mascarar o erro original
            throw;
        }
    }

    private static async Task<byte[]> LerAteAsync(Stream origem, long maximo, CancellationToken ct)
    {
        using var destino = new MemoryStream();
        var buffer = new byte[81920];
        int lidos;
        while (destino.Length < maximo
               && (lidos = await origem.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maximo - destino.Length)), ct)) > 0)
            destino.Write(buffer, 0, lidos);
        return destino.ToArray();
    }
}
