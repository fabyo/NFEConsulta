using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

/// <summary>
/// Servico de consulta de NF-e na SEFAZ.
/// </summary>
public sealed class SefazNFeService : IDisposable
{
    private const string SoapAction = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4/nfeConsultaNF";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SefazNFeService> _logger;
    private bool _disposed;

    public SefazNFeService(HttpClient httpClient, ILogger<SefazNFeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ConsultaNFeResult> ConsultarAsync(
        ConsultaNFeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando consulta NF-e. Chave: {Chave} | Ambiente: {Ambiente} | CorrelationId: {CorrelationId}",
            MascaraChave(request.ChaveAcesso),
            request.Ambiente,
            request.CorrelationId);

        string soapEnvelope;
        try
        {
            soapEnvelope = SoapEnvelopeBuilder.BuildConsultaProtocolo(request.ChaveAcesso, request.Ambiente);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Chave de acesso invalida: {Chave}", request.ChaveAcesso);
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.RequisicaoInvalida,
                correlationId: request.CorrelationId);
        }

        HttpResponseMessage response;
        try
        {
            response = await SefazSoapTransport.PostWithRetryAsync(
                _httpClient,
                _logger,
                request.UrlWebService,
                soapEnvelope,
                SoapAction,
                request.RetryCount,
                "NFeConsultaProtocolo4",
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.Timeout,
                correlationId: request.CorrelationId);
        }
        catch (InvalidOperationException ex)
        {
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.FalhaRede,
                correlationId: request.CorrelationId);
        }
        catch (AuthenticationException ex)
        {
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.FalhaCertificado,
                correlationId: request.CorrelationId);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    _logger.LogWarning("SEFAZ retornou HTTP 500. Tentando parsear SOAP Fault.");
                }
                else
                {
                    _logger.LogError("HTTP {StatusCode} da SEFAZ", response.StatusCode);
                    return ConsultaNFeResult.Erro(
                        $"Erro HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                        TipoResultadoConsulta.FalhaRede,
                        respostaSefazRecebida: true,
                        correlationId: request.CorrelationId);
                }
            }

            string xmlResposta = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Resposta da SEFAZ recebida ({Bytes} bytes)", xmlResposta.Length);

            ConsultaNFeResult resultado = SefazResponseParser.Parse(xmlResposta, request.CorrelationId);

            _logger.LogInformation(
                "Consulta concluida. Status: {Status} | Codigo: {Codigo} | Motivo: {Motivo} | CorrelationId: {CorrelationId}",
                resultado.Status,
                resultado.CodigoStatus,
                resultado.Motivo,
                resultado.CorrelationId);

            return resultado;
        }
    }

    public static SefazNFeService CriarComCertificado(
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        ILogger<SefazNFeService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        HttpClient httpClient = SefazSoapTransport.CriarHttpClientComCertificado(
            certificado,
            timeout,
            ignoreServerCertificateErrors);

        ILogger<SefazNFeService> resolvedLogger = logger ?? Microsoft.Extensions.Logging.Abstractions
            .NullLogger<SefazNFeService>.Instance;

        return new SefazNFeService(httpClient, resolvedLogger);
    }

    private static string MascaraChave(string chave) =>
        chave.Length >= 14 ? chave[..14] + "**************************" + chave[^2..] : chave;

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }
}
