using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nap.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelo <c>dotnet ef</c> em tempo de design, para gerar migrations sem depender da DI da Api.
/// A conexão vem de <c>NAP_CONEXAO_DESIGN</c>; o valor padrão é um marcador sem segredo, suficiente para
/// <c>migrations add</c> (que não conecta ao banco).
/// </summary>
public sealed class NapDbContextFactory : IDesignTimeDbContextFactory<NapDbContext>
{
    public NapDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("NAP_CONEXAO_DESIGN")
            ?? "Server=127.0.0.1,1433;Database=Nap;User Id=sa;Password=definir-em-NAP_CONEXAO_DESIGN;TrustServerCertificate=True";
        return new NapDbContext(new DbContextOptionsBuilder<NapDbContext>().UseSqlServer(conexao).Options);
    }
}
