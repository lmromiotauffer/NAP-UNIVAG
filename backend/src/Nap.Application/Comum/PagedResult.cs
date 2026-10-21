namespace Nap.Application.Comum;

public record PagedResult<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total);
