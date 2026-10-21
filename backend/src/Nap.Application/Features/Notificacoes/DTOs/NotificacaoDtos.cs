namespace Nap.Application.Features.Notificacoes;

public record NotificacaoDto(Guid Id, string Tipo, string Titulo, string Texto, string Link, DateTimeOffset CriadaEm, bool Lida);

public record ContagemNaoLidasDto(int NaoLidas);
