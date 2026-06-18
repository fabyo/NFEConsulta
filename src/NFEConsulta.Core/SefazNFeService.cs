using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

/// <summary>
/// Servico de consulta de NF-e na SEFAZ.
/// </summary>
public sealed class SefazNFeService : IDisposable
{
    private const string SoapContentType = "application/soap+xml";
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
            "Iniciando consulta NF-e. Chave: {Chave} | Ambiente: {Ambiente}",
            MascaraChave(request.ChaveAcesso),
            request.Ambiente);

        string soapEnvelope;
        try
        {
            soapEnvelope = SoapEnvelopeBuilder.BuildConsultaProtocolo(request.ChaveAcesso, request.Ambiente);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Chave de acesso invalida: {Chave}", request.ChaveAcesso);
            return ConsultaNFeResult.Erro(ex.Message);
        }

        using StringContent content = new(soapEnvelope, Encoding.UTF8, SoapContentType);
        content.Headers.ContentType = new MediaTypeHeaderValue(SoapContentType)
        {
            CharSet = Encoding.UTF8.WebName
        };
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", $"\"{SoapAction}\""));
        content.Headers.Add("SOAPAction", SoapAction);

        using HttpResponseMessage response = await PostAsync(
            request.UrlWebService,
            content,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogWarning("SEFAZ retornou HTTP 500. Tentando parsear SOAP Fault.");
            }
            else
            {
                _logger.LogError("HTTP {StatusCode} da SEFAZ", response.StatusCode);
                return ConsultaNFeResult.Erro($"Erro HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
        }

        string xmlResposta = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Resposta da SEFAZ recebida ({Bytes} bytes)", xmlResposta.Length);

        ConsultaNFeResult resultado = SefazResponseParser.Parse(xmlResposta);

        _logger.LogInformation(
            "Consulta concluida. Status: {Status} | Codigo: {Codigo} | Motivo: {Motivo}",
            resultado.Status,
            resultado.CodigoStatus,
            resultado.Motivo);

        return resultado;
    }

    public static SefazNFeService CriarComCertificado(
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        ILogger<SefazNFeService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        HttpClientHandler handler = new()
        {
            SslProtocols = SslProtocols.Tls12
        };

        if (ignoreServerCertificateErrors)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        handler.ClientCertificates.Add(certificado);

        HttpClient httpClient = new(handler, disposeHandler: true)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        ILogger<SefazNFeService> resolvedLogger = logger ?? Microsoft.Extensions.Logging.Abstractions
            .NullLogger<SefazNFeService>.Instance;

        return new SefazNFeService(httpClient, resolvedLogger);
    }

    private async Task<HttpResponseMessage> PostAsync(
        string urlWebService,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient
                .PostAsync(urlWebService, content, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout na comunicacao com a SEFAZ. URL: {Url}", urlWebService);
            throw new TimeoutException("Timeout: a SEFAZ nao respondeu no tempo esperado.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de comunicacao HTTP com a SEFAZ");
            throw new InvalidOperationException($"Erro de rede: {ex.Message}", ex);
        }
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
