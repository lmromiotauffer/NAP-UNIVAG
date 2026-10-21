using Nap.Application.Features.Agenda.Commands;

namespace Nap.Api.Contratos;

public record CriarUsuarioRequest(string Email, string NomeCompleto, string Perfil, Guid? CursoId);

public record AtualizarUsuarioRequest(string NomeCompleto, string Perfil, Guid? CursoId);

public record AlterarSituacaoRequest(bool Ativo);

public record CriarCursoRequest(string Nome);

public record RenomearCursoRequest(string Nome);

public record CriarEscalaRequest(Guid ProfissionalId, DateOnly VigenciaInicio, DateOnly? VigenciaFim, IReadOnlyList<FaixaEscalaInput> Faixas);

public record AtualizarVigenciaEscalaRequest(DateOnly? VigenciaFim);

public record CriarFeriadoRequest(DateOnly Data, string Descricao);

public record AtualizarConfiguracaoAlertasRequest(int FaltasConsecutivas, int FaltasJanela, int JanelaDias, int DiasSemAcolhimento,
    int DiasSemAtualizacao, int DiasPeiPendente);

public record CriarAutorizacaoForaEscalaRequest(Guid ProfissionalId, DateTimeOffset Inicio, DateTimeOffset Fim, string Justificativa);
