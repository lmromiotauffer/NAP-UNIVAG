using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nap.Application.Comum;

namespace Nap.Infrastructure.Persistencia;

/// <summary>
/// Transação manual sem execution strategy: por isso o provedor NÃO usa <c>EnableRetryOnFailure</c>
/// (a estratégia de repetição recusa transações iniciadas pelo usuário e repetiria só parte da operação).
/// </summary>
public sealed class UnidadeDeTrabalho(NapDbContext db) : IUnidadeDeTrabalho
{
    public const string MensagemRegistroAlterado = "O registro foi alterado por outra pessoa. Recarregue e tente novamente";

    public async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(MensagemRegistroAlterado);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            throw new ConflictException(MensagemDeUnicidade(sql.Message));
        }
    }

    public void ExigirVersao(object entidade, string versaoEsperada)
    {
        // A versão original é a lida ao carregar a entidade; o SaveChanges também a usa no WHERE, então isto só antecipa o conflito.
        var atual = (byte[]?)db.Entry(entidade).Property("Versao").OriginalValue;
        Span<byte> esperada = stackalloc byte[8];
        if (!Convert.TryFromBase64String(versaoEsperada.Trim('"'), esperada, out var n)
            || atual is null || !esperada[..n].SequenceEqual(atual))
            throw new ConflictException(MensagemRegistroAlterado);
    }

    public async Task<T> EmTransacaoAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken ct)
    {
        // Já dentro de uma transação: participa dela; quem a abriu faz o commit.
        if (db.Database.CurrentTransaction is not null)
            return await operacao(ct);

        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        T resultado;
        try
        {
            resultado = await operacao(ct);
            await transacao.CommitAsync(ct);
        }
        catch
        {
            // Rollback sem o token: um cancelamento não pode impedir desfazer a transação.
            try { await transacao.RollbackAsync(CancellationToken.None); }
            catch { /* preserva a exceção original */ }
            // Entidades e auditoria da operação desfeita não podem vazar para um próximo SalvarAsync.
            finally { db.ChangeTracker.Clear(); }
            throw;
        }
        return resultado;
    }

    private static string MensagemDeUnicidade(string mensagemSql) =>
        mensagemSql.Contains("IX_Atendimentos_CompromissoId") ? "Este agendamento já possui atendimento registrado"
        : mensagemSql.Contains("IX_AtividadesCurso_CompromissoId") ? "Este agendamento já possui atividade registrada."
        : mensagemSql.Contains("IX_Alunos_Matricula") ? "Matrícula já cadastrada"
        : mensagemSql.Contains("IX_Cursos_Nome") ? Application.Features.Cursos.Commands.RegrasCurso.MensagemNomeRepetido
        : mensagemSql.Contains("IX_Feriados_Data") ? Application.Features.Agenda.Commands.CriarFeriadoHandler.MensagemDuplicado
        : mensagemSql.Contains("IX_Notificacoes_EventoId_DestinatarioId") ? "Notificação já registrada"
        // Duas criações simultâneas de versão esbarram em um dos dois índices.
        : mensagemSql.Contains("IX_VersoesPei_PeiId_Aberta") || mensagemSql.Contains("IX_VersoesPei_PeiId_Numero")
            ? "Já existe uma versão em elaboração."
        : mensagemSql.Contains("IX_AssinaturasPei_VersaoId_Papel") ? "Esta versão já foi assinada neste papel."
        : mensagemSql.Contains("IX_CienciasPei_VersaoId_CoordenadorId") ? Application.Features.Peis.Commands.RegistrarCienciaPeiHandler.MensagemCienciaDuplicada
        : mensagemSql.Contains("IX_AcessosAssinaturaPei_VersaoId_Aberto")
            ? "Outro acesso do aluno foi gerado ao mesmo tempo para esta versão. Gere o acesso novamente."
        : "Registro duplicado";
}
