using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

/// <summary>
/// Servico de consulta de cadastro de contribuinte (CadConsultaCadastro4) na SEFAZ.
/// </summary>
public sealed class SefazCadastroService : IDisposable
{
    private const string SoapAction = "http://www.portalfiscal.inf.br/nfe/wsdl/CadConsultaCadastro4/consultaCadastro";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SefazCadastroService> _logger;
    private bool _disposed;

    public SefazCadastroService(HttpClient httpClient, ILogger<SefazCadastroService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CadastroConsultaResult> ConsultarCadastroAsync(
        CadastroConsultaRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Iniciando consulta de cadastro SEFAZ. UF: {Uf} | Documento/IE: {Documento} | Ambiente: {Ambiente} | CorrelationId: {CorrelationId}",
            request.Uf,
            MascaraDocumento(request.Documento),
            request.Ambiente,
            request.CorrelationId);

        string soapEnvelope = SoapEnvelopeBuilder.BuildCadastro(request.Documento, request.Uf, request.Ambiente);

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
                "CadConsultaCadastro4",
                request.CorrelationId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            return CadastroConsultaResult.Erro(
                ex.Message,
                correlationId: request.CorrelationId);
        }
        catch (AuthenticationException ex)
        {
            return CadastroConsultaResult.Erro(
                ex.Message,
                correlationId: request.CorrelationId);
        }
        catch (InvalidOperationException ex)
        {
            return CadastroConsultaResult.Erro(
                ex.Message,
                correlationId: request.CorrelationId);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.InternalServerError)
            {
                return CadastroConsultaResult.Erro(
                    $"Erro HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    correlationId: request.CorrelationId);
            }

            string xmlResposta = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            CadastroConsultaResult result = SefazCadastroResponseParser.Parse(
                xmlResposta,
                request.CorrelationId);

            _logger.LogInformation(
                "Consulta de cadastro concluida. UF: {Uf} | Sucesso: {Sucesso} | Codigo: {Codigo} | Motivo: {Motivo} | CorrelationId: {CorrelationId}",
                request.Uf,
                result.Sucesso,
                result.CodigoStatus,
                result.Motivo,
                result.CorrelationId);

            return result;
        }
    }

    public static SefazCadastroService CriarComCertificado(
        X509Certificate2 certificado,
        TimeSpan? timeout = null,
        ILogger<SefazCadastroService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        HttpClient httpClient = SefazSoapTransport.CriarHttpClientComCertificado(
            certificado,
            timeout,
            ignoreServerCertificateErrors);

        ILogger<SefazCadastroService> resolvedLogger = logger ?? Microsoft.Extensions.Logging.Abstractions
            .NullLogger<SefazCadastroService>.Instance;

        return new SefazCadastroService(httpClient, resolvedLogger);
    }

    private static string MascaraDocumento(string doc) =>
        doc.Length > 4 ? doc[..4] + "..." + doc[^2..] : doc;

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _disposed = true;
    }
}
