using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

public sealed class SefazStatusService : IDisposable
{
    private const string SoapAction = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4/nfeStatusServicoNF";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SefazStatusService> _logger;
    private bool _disposed;

    public SefazStatusService(HttpClient httpClient, ILogger<SefazStatusService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SefazStatusResult> ConsultarStatusAsync(
        SefazStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string soapEnvelope = SoapEnvelopeBuilder.BuildStatusServico(request.Uf, request.Ambiente);

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
                "NFeStatusServico4",
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            return SefazStatusResult.Erro(
                ex.Message,
                request.Uf,
                request.Ambiente,
                request.UrlWebService,
                stopwatch.Elapsed,
                TipoResultadoConsulta.Timeout,
                correlationId: request.CorrelationId);
        }
        catch (AuthenticationException ex)
        {
            stopwatch.Stop();
            return SefazStatusResult.Erro(
                ex.Message,
                request.Uf,
                request.Ambiente,
                request.UrlWebService,
                stopwatch.Elapsed,
                TipoResultadoConsulta.FalhaCertificado,
                correlationId: request.CorrelationId);
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            return SefazStatusResult.Erro(
                ex.Message,
                request.Uf,
                request.Ambiente,
                request.UrlWebService,
                stopwatch.Elapsed,
                TipoResultadoConsulta.FalhaRede,
                correlationId: request.CorrelationId);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.InternalServerError)
            {
                stopwatch.Stop();
                return SefazStatusResult.Erro(
                    $"Erro HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    request.Uf,
                    request.Ambiente,
                    request.UrlWebService,
                    stopwatch.Elapsed,
                    TipoResultadoConsulta.FalhaRede,
                    respostaSefazRecebida: true,
                    correlationId: request.CorrelationId);
            }

            string xmlResposta = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            SefazStatusResult result = SefazStatusResponseParser.Parse(
                xmlResposta,
                request.Uf,
                request.Ambiente,
                request.UrlWebService,
                stopwatch.Elapsed,
                request.CorrelationId);

            _logger.LogInformation(
                "Status SEFAZ consultado. UF: {Uf} | Ambiente: {Ambiente} | Online: {Online} | Codigo: {Codigo} | CorrelationId: {CorrelationId}",
                result.Uf,
                result.Ambiente,
                result.Online,
                result.CodigoStatus,
                result.CorrelationId);

            return result;
        }
    }

    public static SefazStatusService CriarComCertificado(
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        ILogger<SefazStatusService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        HttpClient httpClient = SefazSoapTransport.CriarHttpClientComCertificado(
            certificado,
            timeout,
            ignoreServerCertificateErrors);

        ILogger<SefazStatusService> resolvedLogger = logger ?? Microsoft.Extensions.Logging.Abstractions
            .NullLogger<SefazStatusService>.Instance;

        return new SefazStatusService(httpClient, resolvedLogger);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }
}
