namespace Nap.Application.Features.Auditoria;

/// <summary>Linha da trilha; <c>Usuario</c> é o nome (nulo quando o evento é do sistema ou o usuário não existe mais).</summary>
public record EventoAuditoriaDto(Guid Id, DateTimeOffset Em, Guid? UsuarioId, string? Usuario, string Acao, string Recurso, Guid? RecursoId,
    string Resultado);
