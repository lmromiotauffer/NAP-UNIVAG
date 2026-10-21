namespace Nap.Application.Features.Producao;

public interface IGeradorExcelProducao
{
    byte[] Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho);
}
