using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nap.Api.Contratos;
using Nap.Application.Comum;

namespace Nap.Api.Infra;

/// <summary>
/// Header <c>Idempotency-Key</c> nos POST de criação. Sem header → executa normalmente. Repetição de uma chave já concluída
/// devolve 201 com o mesmo <c>{id}</c> e Location; chave em andamento → 409. Falha da ação libera a chave para nova tentativa.
/// </summary>
/// <param name="acaoLocal">Ação apontada pelo Location (a mesma usada no <c>CreatedAtAction</c> da ação). Valores de rota:
/// <c>{id}</c>, ou os do corpo quando ele implementa <see cref="IComLocalCriacao"/>.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotenteAttribute(string acaoLocal) : Attribute, IAsyncActionFilter
{
    public const string Cabecalho = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
    {
        var http = contexto.HttpContext;
        var valores = http.Request.Headers[Cabecalho];
        if (valores.Count > 1)
            throw Validacao.ErroDeCampo("idempotencyKey", "Envie um único header Idempotency-Key.");
        var chave = valores.ToString();
        if (string.IsNullOrWhiteSpace(chave))
        {
            await proximo();
            return;
        }
        if (chave.Length > 100)
            throw Validacao.ErroDeCampo("idempotencyKey", "O header Idempotency-Key deve ter no máximo 100 caracteres.");

        var chaves = http.RequestServices.GetRequiredService<IChavesIdempotencia>();
        var usuarioId = http.RequestServices.GetRequiredService<IUsuarioAtual>().Id;
        var reserva = await chaves.ReservarAsync(usuarioId, chave, $"{http.Request.Method} {http.Request.Path}", http.RequestAborted);
        switch (reserva.Resultado)
        {
            case ResultadoReserva.Concluida:
                var id = reserva.RecursoId!.Value;
                var rota = contexto.ActionArguments.Values.OfType<IComLocalCriacao>().FirstOrDefault()
                    ?.ValoresRota(http.RequestServices.GetRequiredService<IRelogio>().Fuso) ?? new { id };
                contexto.Result = new CreatedAtActionResult(acaoLocal, null, rota, new IdCriado(id));
                return;
            case ResultadoReserva.EmAndamento:
                throw new ConflictException("Uma requisição com esta chave de idempotência ainda está em processamento");
        }

        // Concluir/Liberar sem o token da requisição: o recurso pode ter sido criado mesmo com o cliente desconectado.
        var executado = await proximo();
        if (executado.Exception is null && executado.Result is CreatedAtActionResult { Value: IdCriado criado })
            await ConcluirAsync(http, chaves, usuarioId, chave, criado.Id);
        else
            await chaves.LiberarAsync(usuarioId, chave, CancellationToken.None);
    }

    /// <summary>
    /// O recurso já existe: falhar aqui não pode virar erro para o cliente. Tenta concluir a chave duas vezes; se ainda
    /// falhar, registra em Critical (rota e id, sem corpo) e devolve o 201 mesmo assim. A chave fica "em andamento": uma
    /// repetição recebe 409, mas depois de considerada abandonada (5 min) poderia recriar o recurso — daí o Critical.
    /// </summary>
    private static async Task ConcluirAsync(HttpContext http, IChavesIdempotencia chaves, Guid usuarioId, string chave, Guid recursoId)
    {
        try
        {
            await chaves.ConcluirAsync(usuarioId, chave, recursoId, CancellationToken.None);
        }
        catch (Exception)
        {
            try
            {
                await chaves.ConcluirAsync(usuarioId, chave, recursoId, CancellationToken.None);
            }
            catch (Exception e)
            {
                http.RequestServices.GetRequiredService<ILogger<IdempotenteAttribute>>().LogCritical(e,
                    "Falha ao concluir chave de idempotência em {Metodo} {Caminho}; recurso {RecursoId} já criado",
                    http.Request.Method, http.Request.Path, recursoId);
            }
        }
    }
}
