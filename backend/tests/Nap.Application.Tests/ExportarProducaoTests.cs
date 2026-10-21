using Nap.Application.Comum;
using Nap.Application.Features.Cursos;
using Nap.Application.Features.Producao;
using Nap.Application.Features.Producao.Queries;
using Nap.Application.Tests.Fakes;
using Nap.Domain.Comum;
using Nap.Domain.Producao;

namespace Nap.Application.Tests;

public class ExportarProducaoTests
{
    private sealed class GeradorFalso : IGeradorPdfProducao, IGeradorExcelProducao
    {
        public CabecalhoExportacao? Cabecalho { get; private set; }
        public ProducaoDto? Dados { get; private set; }

        byte[] IGeradorPdfProducao.Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho) => Registrar(dados, cabecalho, 1);
        byte[] IGeradorExcelProducao.Gerar(ProducaoDto dados, CabecalhoExportacao cabecalho) => Registrar(dados, cabecalho, 2);

        private byte[] Registrar(ProducaoDto dados, CabecalhoExportacao cabecalho, byte marca)
        {
            Dados = dados;
            Cabecalho = cabecalho;
            return [marca];
        }
    }

    private readonly ProducaoQueriesFalso _consultas = new();
    private readonly AuditoriaFalsa _auditoria = new();
    private readonly GeradorFalso _gerador = new();
    private readonly CursoQueriesFalso _cursos = new();
    private readonly Guid _prof = Guid.NewGuid();
    private readonly Guid _outro = Guid.NewGuid();

    private ExportarProducaoHandler Handler(UsuarioAtualFalso usuario)
    {
        var usuarios = new UsuarioQueriesFalso(_prof, _outro);
        usuarios.Usuarios.AddRange([(_prof, Perfis.ProfissionalNap, null, true), (_outro, Perfis.ProfissionalNap, null, true),
            (usuario.Id, usuario.Perfil, usuario.CursoId, true)]);
        usuarios.Nomes[_prof] = "Ana";
        usuarios.Nomes[_outro] = "Bruno";
        usuarios.Nomes.TryAdd(usuario.Id, "Quem Exporta");
        var relogio = new RelogioFixo();
        var obter = new ObterProducaoHandler(new ObterProducaoValidator(), _consultas, usuarios, usuario, relogio);
        return new ExportarProducaoHandler(new ExportarProducaoValidator(), obter, _gerador, _gerador, usuarios, _cursos, usuario, relogio,
            _auditoria, new UnidadeDeTrabalhoFalsa());
    }

    private Task<ArquivoExportado> Exportar(UsuarioAtualFalso usuario, string formato = "pdf", Guid? profissionalId = null, Guid? cursoId = null) =>
        Handler(usuario).Handle(new ExportarProducaoQuery(formato, TipoPeriodo.Mes, new DateOnly(2026, 9, 15), profissionalId, cursoId),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Exporta_com_o_mesmo_recorte_da_consulta()
    {
        var eu = new UsuarioAtualFalso(Perfis.ProfissionalNap) { Id = _prof };
        var ocorrencia = new OcorrenciaProfissional(_prof, CategoriaProducao.AtendimentosRealizados, new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero),
            new(2026, 9, 22, 11, 0, 0, TimeSpan.Zero), true);
        var deOutro = ocorrencia with { ProfissionalId = _outro };
        _consultas.Fatos = _consultas.Fatos with { Ocorrencias = [ocorrencia, deOutro, deOutro] };

        var arquivo = await Exportar(eu, "xlsx", profissionalId: _outro);

        Assert.Equal([_prof], _consultas.IdsRecebidos);
        Assert.Equal(1, _gerador.Dados!.Categorias.Single(c => c.Chave == CategoriaProducao.AtendimentosRealizados).Total.Ocorrencias);
        Assert.Equal("Profissional: Ana · Curso: Todos", _gerador.Cabecalho!.Filtros);
        Assert.Equal("Ana", _gerador.Cabecalho.ExportadoPor);
        Assert.Equal("01/09/2026 a 30/09/2026", _gerador.Cabecalho.Periodo);
        Assert.Equal("Mês", _gerador.Cabecalho.Tipo);
        Assert.Equal([2], arquivo.Conteudo);
        Assert.Equal("Producao-mes-2026-09-01.xlsx", arquivo.NomeArquivo);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", arquivo.ContentType);
    }

    [Fact]
    public async Task Filtros_levam_os_nomes_e_nunca_ids()
    {
        var curso = Guid.NewGuid();
        _cursos.Cursos.Add(new CursoDto(curso, "Direito", true, "v"));

        await Exportar(new UsuarioAtualFalso(Perfis.CoordenadorNap), profissionalId: _outro, cursoId: curso);

        Assert.Equal("Profissional: Bruno · Curso: Direito", _gerador.Cabecalho!.Filtros);
    }

    [Theory]
    [InlineData("")]
    [InlineData("docx")]
    [InlineData("PDF")]
    public async Task Formato_invalido_retorna_400(string formato)
    {
        var erro = await Assert.ThrowsAsync<ValidationException>(() => Exportar(new UsuarioAtualFalso(Perfis.CoordenadorNap), formato));

        Assert.Contains("formato", erro.Erros.Keys);
        Assert.Empty(_auditoria.Registros);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("xlsx")]
    public async Task Exportacao_e_auditada(string formato)
    {
        await Exportar(new UsuarioAtualFalso(Perfis.Gestao), formato);

        Assert.Equal([($"producao.exportar.{formato}", "Producao", (Guid?)null)], _auditoria.Registros);
    }
}
