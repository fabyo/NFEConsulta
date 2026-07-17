using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;

namespace NFEConsulta.Services;

internal static class SefazSoapTransport
{
    private const string SoapContentType = "application/soap+xml";
    private const long MaxResponseContentBytes = 2 * 1024 * 1024;

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
                using HttpRequestMessage request = new(HttpMethod.Post, urlWebService)
                {
                    Content = content
                };
                HttpResponseMessage response = await httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (attempt < maxAttempts && IsTransientStatusCode(response.StatusCode))
                {
                    TimeSpan? retryAfter = ResolveRetryAfter(response.Headers.RetryAfter);
                    logger.LogWarning(
                        "Resposta HTTP transitoria da SEFAZ. Operacao: {Operation} | Status: {StatusCode} | Tentativa {Attempt}/{MaxAttempts} | CorrelationId: {CorrelationId}",
                        operationName,
                        (int)response.StatusCode,
                        attempt,
                        maxAttempts,
                        correlationId);

                    response.Dispose();
                    await DelayRetryAsync(attempt, retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    await BufferResponseContentAsync(response, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
                {
                    response.Dispose();
                    throw new InvalidOperationException(
                        $"A resposta da SEFAZ excedeu o limite de {MaxResponseContentBytes} bytes.",
                        ex);
                }

                return response;
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

                await DelayRetryAsync(attempt, retryAfter: null, cancellationToken).ConfigureAwait(false);
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

                await DelayRetryAsync(attempt, retryAfter: null, cancellationToken).ConfigureAwait(false);
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

    private static async Task BufferResponseContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxResponseContentBytes)
        {
            throw new InvalidDataException(
                $"A resposta declarou mais de {MaxResponseContentBytes} bytes.");
        }

        await using Stream source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using MemoryStream buffer = await XmlInputLimits
            .CopyToMemoryAsync(source, MaxResponseContentBytes, cancellationToken)
            .ConfigureAwait(false);

        ByteArrayContent bufferedContent = new(buffer.ToArray());
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
            bufferedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

        response.Content.Dispose();
        response.Content = bufferedContent;
    }

    private static Task DelayRetryAsync(
        int attempt,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        TimeSpan delay = retryAfter ?? TimeSpan.FromMilliseconds(
            Math.Min(250 * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 251), 5_000));

        return Task.Delay(delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay, cancellationToken);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.InternalServerError or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static TimeSpan? ResolveRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
            return delta;

        if (retryAfter?.Date is { } date)
            return date - DateTimeOffset.UtcNow > TimeSpan.Zero
                ? date - DateTimeOffset.UtcNow
                : TimeSpan.Zero;

        return null;
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
