using System.Globalization;
using FluentValidation;
using Nap.Application.Comum;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Usuarios;
using Nap.Domain.Comum;

namespace Nap.Application.Features.Producao.Queries;

public record ExportarProducaoQuery(string Formato, TipoPeriodo Tipo, DateOnly Referencia, Guid? ProfissionalId, Guid? CursoId);

public record ArquivoExportado(byte[] Conteudo, string NomeArquivo, string ContentType);

public class ExportarProducaoValidator : AbstractValidator<ExportarProducaoQuery>
{
    public ExportarProducaoValidator() =>
        RuleFor(x => x.Formato).Must(f => f is "pdf" or "xlsx").WithMessage("Informe o formato: pdf ou xlsx.");
}

/// <summary>Exporta a produção pelo mesmo <see cref="ObterProducaoHandler"/> da tela, então o recorte por perfil é idêntico.</summary>
public sealed class ExportarProducaoHandler(IValidator<ExportarProducaoQuery> validador, ObterProducaoHandler producao,
    IGeradorPdfProducao pdf, IGeradorExcelProducao excel, IUsuarioQueries usuarios, ICursoQueries cursos, IUsuarioAtual usuario,
    IRelogio relogio, IAuditoria auditoria, IUnidadeDeTrabalho uow)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ArquivoExportado> Handle(ExportarProducaoQuery consulta, CancellationToken ct = default)
    {
        await validador.ValidarAsync(consulta, ct);
        var dados = await producao.Handle(new ObterProducaoQuery(consulta.Tipo, consulta.Referencia, consulta.ProfissionalId, consulta.CursoId), ct);

        // Filtros efetivamente aplicados pelo recorte do perfil (o filtro recebido pode ser ignorado).
        var (profissionalId, cursoId) = usuario.Perfil switch
        {
            Perfis.ProfissionalNap => ((Guid?)usuario.Id, consulta.CursoId),
            Perfis.Gestao => (consulta.ProfissionalId, null),
            Perfis.CoordenadorCurso => (null, usuario.CursoId),
            _ => (consulta.ProfissionalId, consulta.CursoId),
        };
        var nomeProfissional = profissionalId is { } p ? (await usuarios.ObterResumoAsync(p, ct))?.Nome ?? "Não encontrado" : "Todos";
        var nomeCurso = cursoId is { } c ? (await cursos.ListarAsync(ct)).FirstOrDefault(x => x.Id == c)?.Nome ?? "Não encontrado" : "Todos";
        var exportador = (await usuarios.ObterResumoAsync(usuario.Id, ct))?.Nome ?? "Usuário";

        var tipo = consulta.Tipo switch
        {
            TipoPeriodo.Semana => "Semana",
            TipoPeriodo.Mes => "Mês",
            TipoPeriodo.Semestre => "Semestre",
            _ => "Ano",
        };
        var cabecalho = new CabecalhoExportacao(
            $"{dados.Periodo.Inicio.ToString("dd/MM/yyyy", PtBr)} a {dados.Periodo.Fim.ToString("dd/MM/yyyy", PtBr)}", tipo,
            $"Profissional: {nomeProfissional} · Curso: {nomeCurso}", exportador, relogio.Agora);

        var ehPdf = consulta.Formato == "pdf";
        var conteudo = ehPdf ? pdf.Gerar(dados, cabecalho) : excel.Gerar(dados, cabecalho);
        auditoria.Registrar($"producao.exportar.{consulta.Formato}", "Producao", null);
        await uow.SalvarAsync(ct);
        return new ArquivoExportado(conteudo,
            $"Producao-{consulta.Tipo.ToString().ToLowerInvariant()}-{dados.Periodo.Inicio:yyyy-MM-dd}.{consulta.Formato}",
            ehPdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
