using System;
using System.Collections.Generic;

namespace NFEConsulta.Models;

/// <summary>
/// Representa uma solicitacao de consulta de cadastro de contribuinte na SEFAZ.
/// </summary>
public record CadastroConsultaRequest(
    string Documento, // Pode ser CNPJ (14 digitos), CPF (11 digitos) ou IE
    UfNFe Uf,
    TipoAmbiente Ambiente,
    string UrlWebService
)
{
    public int RetryCount { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Resultado retornado pela consulta de cadastro de contribuinte na SEFAZ.
/// </summary>
public record CadastroConsultaResult
{
    public bool Sucesso { get; init; }
    public string CodigoStatus { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? ErroDetalhado { get; init; }
    public List<CadastroContribuinte> Contribuintes { get; init; } = new();

    public bool EhFalhaTecnica => CodigoStatus == string.Empty || ErroDetalhado != null;

    public static CadastroConsultaResult Erro(
        string mensagem,
        string? correlationId = null) => new()
        {
            Sucesso = false,
            ErroDetalhado = mensagem,
            CorrelationId = correlationId
        };
}

/// <summary>
/// Detalhes cadastrais de um contribuinte retornado pela SEFAZ.
/// </summary>
public record CadastroContribuinte
{
    public string? IE { get; init; }
    public string? CNPJ { get; init; }
    public string? CPF { get; init; }
    public string UF { get; init; } = string.Empty;
    public string Situacao { get; init; } = string.Empty; // cSit: 0 = Nao habilitado, 1 = Habilitado
    public string Nome { get; init; } = string.Empty; // xNome
    public string? NomeFantasia { get; init; } // xFant
    public string? RegimeApuracao { get; init; } // xRegAp
    public string? CNAE { get; init; }
    public DateTime? DataInicioAtividade { get; init; } // dIniAtiv
    public DateTime? DataUltimaSituacao { get; init; } // dUltSit
    public DateTime? DataBaixa { get; init; } // dBaixa
    public string? IEUnica { get; init; }
    public string? IEAtual { get; init; }
    public EnderecoContribuinte? Endereco { get; init; }
}

/// <summary>
/// Dados de endereco do contribuinte retornado pela SEFAZ.
/// </summary>
public record EnderecoContribuinte
{
    public string? Logradouro { get; init; } // xLgr
    public string? Numero { get; init; } // nro
    public string? Complemento { get; init; } // xCpl
    public string? Bairro { get; init; } // xBairro
    public string? CodigoMunicipio { get; init; } // cMun
    public string? NomeMunicipio { get; init; } // xMun
    public string? CEP { get; init; }
}
