using Nap.Domain.Comum;

namespace Nap.Domain.Encaminhamentos;

public class Encaminhamento : AgregadoComEventos
{
    private readonly List<EvolucaoEncaminhamento> _evolucoes = [];

    public Guid AlunoId { get; private set; }
    public Guid CursoId { get; private set; }
    public Guid SolicitanteId { get; private set; }
    public Especialidade Especialidade { get; private set; }
    public DestinoEncaminhamento Destino { get; private set; }
    public DateOnly DataEncaminhamento { get; private set; }
    public string MotivoRestrito { get; private set; } = null!;
    public string ResumoCompartilhavel { get; private set; } = null!;
    public string? Observacoes { get; private set; }
    public DateOnly? DataAcolhimento { get; private set; }
    public DateOnly? DataPrimeiroAtendimento { get; private set; }
    public DateOnly? DataEncerramento { get; private set; }
    public EstadoEncaminhamento Estado { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    public IReadOnlyCollection<EvolucaoEncaminhamento> Evolucoes => _evolucoes;

    private Encaminhamento() { } // ORM

    public static Encaminhamento Criar(Guid alunoId, Guid cursoId, Guid solicitanteId, Especialidade especialidade,
        DestinoEncaminhamento destino, DateOnly data, string motivoRestrito, string resumoCompartilhavel,
        string? observacoes, DateOnly hoje, DateTimeOffset agora)
    {
        if ((especialidade == Especialidade.CoordenacaoCurso) != (destino == DestinoEncaminhamento.CoordenacaoCurso))
            throw new DomainException("A especialidade Coordenação de curso exige o destino Coordenação de curso, e vice-versa.");
        var e = new Encaminhamento
        {
            AlunoId = alunoId,
            CursoId = cursoId,
            SolicitanteId = solicitanteId,
            Especialidade = especialidade,
            Destino = destino,
            DataEncaminhamento = ValidarData(data, null, hoje, "do encaminhamento"),
            MotivoRestrito = Texto.Validar(motivoRestrito, 3, 4000, "O motivo restrito"),
            ResumoCompartilhavel = Texto.Validar(resumoCompartilhavel, 3, 2000, "O resumo compartilhável"),
            Observacoes = ValidarObservacoes(observacoes, "As observações"),
            Estado = EstadoEncaminhamento.Encaminhado,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
        e.Registrar(new EncaminhamentoCriado(agora, e.Id, alunoId, cursoId, solicitanteId, especialidade, destino));
        return e;
    }

    public void RegistrarRetorno(AcaoRetorno acao, DateOnly? data, string? texto, Guid autorId, DateOnly hoje,
        DateTimeOffset agora)
    {
        if (acao == AcaoRetorno.Cancelamento)
            throw new DomainException("O cancelamento deve ser feito pela operação de cancelar.");
        ExigirAberto();

        switch (acao)
        {
            case AcaoRetorno.Acolhimento:
                ExigirEstado(EstadoEncaminhamento.Encaminhado, "O acolhimento só pode ser registrado em um encaminhamento recém-criado.");
                var acolhimento = ValidarData(data, DataEncaminhamento, hoje, "do acolhimento");
                var textoAcolhimento = ValidarObservacoes(texto, "O texto");
                DataAcolhimento = acolhimento;
                Aplicar(acao, EstadoEncaminhamento.Acolhido, acolhimento, textoAcolhimento, autorId, agora);
                break;
            case AcaoRetorno.InicioAtendimento:
                ExigirEstado(EstadoEncaminhamento.Acolhido, "O início do atendimento exige o acolhimento prévio.");
                var inicio = ValidarData(data, DataAcolhimento, hoje, "do início do atendimento");
                var textoInicio = ValidarObservacoes(texto, "O texto");
                DataPrimeiroAtendimento = inicio;
                Aplicar(acao, EstadoEncaminhamento.EmAtendimento, inicio, textoInicio, autorId, agora);
                break;
            case AcaoRetorno.Evolucao:
                Aplicar(acao, Estado, null, Texto.Validar(texto, 1, 4000, "O texto da evolução"), autorId, agora);
                break;
            case AcaoRetorno.Alta:
                ExigirEstado(EstadoEncaminhamento.EmAtendimento, "A alta exige que o atendimento já tenha sido iniciado.");
                var alta = ValidarData(data, DataPrimeiroAtendimento, hoje, "da alta");
                var textoAlta = ValidarObservacoes(texto, "O texto");
                DataEncerramento = alta;
                Aplicar(acao, EstadoEncaminhamento.Alta, alta, textoAlta, autorId, agora);
                break;
            case AcaoRetorno.Desistencia:
                var minima = new[] { DataEncaminhamento, DataAcolhimento ?? default, DataPrimeiroAtendimento ?? default }.Max();
                var desistencia = ValidarData(data, minima, hoje, "da desistência");
                var justificativa = Texto.Validar(texto, 3, 1000, "A justificativa");
                DataEncerramento = desistencia;
                Aplicar(acao, EstadoEncaminhamento.Desistencia, desistencia, justificativa, autorId, agora);
                break;
        }
    }

    public void Cancelar(string justificativa, Guid autorId, DateOnly hoje, DateTimeOffset agora)
    {
        ExigirAberto();
        var texto = Texto.Validar(justificativa, 3, 1000, "A justificativa");
        DataEncerramento = hoje;
        Aplicar(AcaoRetorno.Cancelamento, EstadoEncaminhamento.Cancelado, hoje, texto, autorId, agora);
    }

    private void Aplicar(AcaoRetorno acao, EstadoEncaminhamento novo, DateOnly? data, string? texto, Guid autorId,
        DateTimeOffset agora)
    {
        _evolucoes.Add(EvolucaoEncaminhamento.Criar(agora, autorId, acao, Estado, novo, data, texto));
        Estado = novo;
        AtualizadoEm = agora;
        Registrar(new RetornoEncaminhamentoRegistrado(agora, Id, AlunoId, SolicitanteId, autorId, novo, acao));
    }

    private void ExigirAberto()
    {
        if (Estado is EstadoEncaminhamento.Alta or EstadoEncaminhamento.Desistencia or EstadoEncaminhamento.Cancelado)
            throw new DomainException("O encaminhamento já foi encerrado e não pode mais ser alterado.");
    }

    private void ExigirEstado(EstadoEncaminhamento esperado, string mensagem)
    {
        if (Estado != esperado) throw new DomainException(mensagem);
    }

    private static DateOnly ValidarData(DateOnly? data, DateOnly? minima, DateOnly hoje, string nome)
    {
        if (data is null) throw new DomainException($"A data {nome} é obrigatória.");
        if (data > hoje) throw new DomainException($"A data {nome} não pode estar no futuro.");
        if (minima is { } m && data < m)
            throw new DomainException($"A data {nome} não pode ser anterior a {m:dd/MM/yyyy}.");
        return data.Value;
    }

    private static string? ValidarObservacoes(string? texto, string nome)
    {
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        return Texto.Validar(texto, 1, 4000, nome);
    }
}
