using Nap.Application.Features.Producao;
using Nap.Domain.Producao;

namespace Nap.Infrastructure.Persistencia.Repositorios;

public sealed class AjusteProducaoRepository(NapDbContext db) : IAjusteProducaoRepository
{
    public void Adicionar(AjusteProducao a) => db.AjustesProducao.Add(a);
}
