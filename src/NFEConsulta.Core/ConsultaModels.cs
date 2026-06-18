namespace NFEConsulta.Models;

public record ConsultaNFeRequest(
    string ChaveAcesso,
    TipoAmbiente Ambiente,
    string UrlWebService
);

public record ConsultaNFeResult
{
    public bool Sucesso { get; init; }
    public string CodigoStatus { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public StatusNFe Status { get; init; }
    public string? NumeroProtocolo { get; init; }
    public DateTimeOffset? DataAutorizacao { get; init; }
    public string? ErroDetalhado { get; init; }

    public static ConsultaNFeResult Erro(string mensagem) => new()
    {
        Sucesso = false,
        ErroDetalhado = mensagem,
        Status = StatusNFe.Desconhecido
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
