namespace NFEConsulta.Models;

public record ConsultaNFeRequest(
    string ChaveAcesso,
    TipoAmbiente Ambiente,
    string UrlWebService
)
{
    public int RetryCount { get; init; }
    public string? CorrelationId { get; init; }
}

public record SefazStatusRequest(
    UfNFe Uf,
    TipoAmbiente Ambiente,
    string UrlWebService
)
{
    public int RetryCount { get; init; }
    public string? CorrelationId { get; init; }
}

public record ConsultaNFeResult
{
    public bool Sucesso { get; init; }
    public string CodigoStatus { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public StatusNFe Status { get; init; }
    public TipoResultadoConsulta TipoResultado { get; init; } = TipoResultadoConsulta.Desconhecido;
    public bool RespostaSefazRecebida { get; init; }
    public string? CorrelationId { get; init; }
    public string? NumeroProtocolo { get; init; }
    public DateTimeOffset? DataAutorizacao { get; init; }
    public string? ErroDetalhado { get; init; }

    public bool EhFalhaTecnica => TipoResultado is
        TipoResultadoConsulta.FalhaRede or
        TipoResultadoConsulta.FalhaCertificado or
        TipoResultadoConsulta.FalhaXml or
        TipoResultadoConsulta.Timeout or
        TipoResultadoConsulta.RequisicaoInvalida or
        TipoResultadoConsulta.FalhaTecnica;

    public bool EhRespostaSefazNegativa => TipoResultado == TipoResultadoConsulta.RespostaSefazNegativa;

    public static ConsultaNFeResult Erro(
        string mensagem,
        TipoResultadoConsulta tipoResultado = TipoResultadoConsulta.FalhaTecnica,
        bool respostaSefazRecebida = false,
        string? correlationId = null) => new()
        {
            Sucesso = false,
            ErroDetalhado = mensagem,
            Status = StatusNFe.Desconhecido,
            TipoResultado = tipoResultado,
            RespostaSefazRecebida = respostaSefazRecebida,
            CorrelationId = correlationId
        };
}

public record SefazStatusResult
{
    public bool Online { get; init; }
    public string CodigoStatus { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public UfNFe Uf { get; init; }
    public TipoAmbiente Ambiente { get; init; }
    public string UrlWebService { get; init; } = string.Empty;
    public TimeSpan Duracao { get; init; }
    public TipoResultadoConsulta TipoResultado { get; init; } = TipoResultadoConsulta.Desconhecido;
    public bool RespostaSefazRecebida { get; init; }
    public string? CorrelationId { get; init; }
    public string? ErroDetalhado { get; init; }

    public bool EhFalhaTecnica => TipoResultado is
        TipoResultadoConsulta.FalhaRede or
        TipoResultadoConsulta.FalhaCertificado or
        TipoResultadoConsulta.FalhaXml or
        TipoResultadoConsulta.Timeout or
        TipoResultadoConsulta.RequisicaoInvalida or
        TipoResultadoConsulta.FalhaTecnica;

    public static SefazStatusResult Erro(
        string mensagem,
        UfNFe uf,
        TipoAmbiente ambiente,
        string urlWebService,
        TimeSpan duracao,
        TipoResultadoConsulta tipoResultado = TipoResultadoConsulta.FalhaTecnica,
        bool respostaSefazRecebida = false,
        string? correlationId = null) => new()
        {
            Online = false,
            Uf = uf,
            Ambiente = ambiente,
            UrlWebService = urlWebService,
            Duracao = duracao,
            TipoResultado = tipoResultado,
            RespostaSefazRecebida = respostaSefazRecebida,
            CorrelationId = correlationId,
            ErroDetalhado = mensagem
        };
}

public enum TipoAmbiente
{
    Producao = 1,
    Homologacao = 2
}

public enum StatusNFe
{
    Autorizada,
    Cancelada,
    Denegada,
    NaoEncontrada,
    Desconhecido
}

public enum TipoResultadoConsulta
{
    ConsultaOk,
    RespostaSefazNegativa,
    FalhaRede,
    FalhaCertificado,
    FalhaXml,
    Timeout,
    RequisicaoInvalida,
    FalhaTecnica,
    Desconhecido
}

public enum UfNFe
{
    AC = 12,
    AL = 27,
    AP = 16,
    AM = 13,
    BA = 29,
    CE = 23,
    DF = 53,
    ES = 32,
    GO = 52,
    MA = 21,
    MT = 51,
    MS = 50,
    MG = 31,
    PA = 15,
    PB = 25,
    PR = 41,
    PE = 26,
    PI = 22,
    RJ = 33,
    RN = 24,
    RS = 43,
    RO = 11,
    RR = 14,
    SC = 42,
    SP = 35,
    SE = 28,
    TO = 17
}

public sealed record NFeConsultaOptions
{
    public TipoAmbiente Ambiente { get; init; } = TipoAmbiente.Homologacao;
    public UfNFe? Uf { get; init; } = UfNFe.SP;
    public string? UrlWebServiceOverride { get; init; }
    public string? UrlStatusWebServiceOverride { get; init; }
    public string? CorrelationId { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int RetryCount { get; init; } = 2;
}
