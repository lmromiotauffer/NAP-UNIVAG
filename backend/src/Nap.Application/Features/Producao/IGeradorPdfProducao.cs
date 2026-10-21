namespace Nap.Application.Features.Producao;

/// <summary>Filtros já resolvidos em texto (nomes, nunca ids); ExportadoPor é o nome do usuário.</summary>
public record CabecalhoExportacao(string Periodo, string Tipo, string Filtros, string ExportadoPor, DateTimeOffset Em);

public interface IGeradorPdfProducao
{
    byte[] Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho);
}
