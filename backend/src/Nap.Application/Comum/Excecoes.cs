namespace Nap.Application.Comum;

public class ValidationException(IDictionary<string, string[]> erros) : Exception("Há campos inválidos.")
{
    public IDictionary<string, string[]> Erros { get; } = erros;
}

public class UnauthorizedException(string mensagem = "Autenticação necessária.") : Exception(mensagem);

public class ForbiddenException(string mensagem = "Você não tem permissão para realizar esta ação.") : Exception(mensagem);

/// <summary>Com <paramref name="mensagem"/>, o 404 usa a mensagem exata; sem ela, "{recurso} não encontrado/encontrada." pelo gênero do recurso.</summary>
public class NotFoundException(string recurso, string? mensagem = null)
    : Exception(mensagem ?? $"{recurso} não {(RecursosFemininos.Contains(recurso) ? "encontrada" : "encontrado")}.")
{
    private static readonly HashSet<string> RecursosFemininos =
        ["Mensagem", "Versão do PEI", "Atividade", "Notificação", "Escala", "Autorização"];
}

public class ConflictException(string mensagem) : Exception(mensagem);
