using System.Security.Cryptography;
using System.Text;
using Nap.Application.Comum;
using Nap.Domain.Peis;

namespace Nap.Infrastructure.Assinatura;

/// <summary>Assinatura demonstrativa, NÃO certificada (pendência institucional): a evidência é só o SHA-256 dos dados assinados.</summary>
public sealed class AssinaturaDemonstrativa : IServicoAssinatura
{
    public Task<EvidenciaAssinatura> AssinarAsync(Guid versaoId, PapelAssinatura papel, string nomeSignatario,
        string hashConteudo, DateTimeOffset instante, CancellationToken ct)
    {
        // Cada campo com o tamanho à frente: um "|" no nome não consegue simular a fronteira entre campos.
        var dados = string.Concat(new[] { versaoId.ToString(), papel.ToString(), nomeSignatario, hashConteudo,
            instante.ToUniversalTime().ToString("O") }.Select(c => $"{c.Length}:{c};"));
        var evidencia = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(dados)));
        return Task.FromResult(new EvidenciaAssinatura("Demonstrativa", evidencia));
    }
}
