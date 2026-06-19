using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

namespace NFEConsulta.Services;

internal static class SefazSoapTransport
{
    private const string SoapContentType = "application/soap+xml";

    public static async Task<HttpResponseMessage> PostWithRetryAsync(
        HttpClient httpClient,
        ILogger logger,
        string urlWebService,
        string soapEnvelope,
        string soapAction,
        int retryCount,
        string operationName,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        int maxAttempts = Math.Max(1, retryCount + 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using StringContent content = CreateSoapContent(soapEnvelope, soapAction);
                return await httpClient
                    .PostAsync(urlWebService, content, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(
                        ex,
                        "Timeout na comunicacao com a SEFAZ. Operacao: {Operation} | URL: {Url} | CorrelationId: {CorrelationId}",
                        operationName,
                        urlWebService,
                        correlationId);

                    throw new TimeoutException("Timeout: a SEFAZ nao respondeu no tempo esperado.", ex);
                }

                await DelayRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                if (ContainsAuthenticationException(ex))
                    throw new AuthenticationException("Falha TLS/certificado ao comunicar com a SEFAZ.", ex);

                logger.LogWarning(
                    ex,
                    "Falha transitoria de comunicacao com a SEFAZ. Operacao: {Operation} | Tentativa {Attempt}/{MaxAttempts} | CorrelationId: {CorrelationId}",
                    operationName,
                    attempt,
                    maxAttempts,
                    correlationId);

                await DelayRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (ContainsAuthenticationException(ex))
                    throw new AuthenticationException("Falha TLS/certificado ao comunicar com a SEFAZ.", ex);

                logger.LogError(
                    ex,
                    "Erro de comunicacao HTTP com a SEFAZ. Operacao: {Operation} | CorrelationId: {CorrelationId}",
                    operationName,
                    correlationId);

                throw new InvalidOperationException($"Erro de rede: {ex.Message}", ex);
            }
        }

        throw new InvalidOperationException($"Falha inesperada ao executar {operationName} na SEFAZ.");
    }

    public static HttpClient CriarHttpClientComCertificado(
        X509Certificate2 certificado,
        TimeSpan? timeout,
        bool ignoreServerCertificateErrors)
    {
        HttpClientHandler handler = new()
        {
            SslProtocols = SslProtocols.Tls12
        };

        if (ignoreServerCertificateErrors)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        handler.ClientCertificates.Add(certificado);

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
    }

    private static StringContent CreateSoapContent(string soapEnvelope, string soapAction)
    {
        StringContent content = new(soapEnvelope, Encoding.UTF8, SoapContentType);
        content.Headers.ContentType = new MediaTypeHeaderValue(SoapContentType)
        {
            CharSet = Encoding.UTF8.WebName
        };
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", $"\"{soapAction}\""));
        content.Headers.Add("SOAPAction", soapAction);
        return content;
    }

    private static Task DelayRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        int delayMilliseconds = Math.Min(250 * attempt, 1_000);
        return Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
    }

    private static bool ContainsAuthenticationException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException)
                return true;
        }

        return false;
    }
}
