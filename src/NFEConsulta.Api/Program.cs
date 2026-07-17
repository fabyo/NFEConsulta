using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using NFEConsulta.Extensions;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

SefazApiOptions options = SefazApiOptions.FromConfiguration(builder.Configuration);
using X509Certificate2 certificado = ResolveCertificate(options);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = options.MaxRequestBodyBytes;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddPolicy("sefaz-per-ip", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.RateLimitPermitLimit,
                Window = options.RateLimitWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response
            .WriteAsJsonAsync(
                new ErrorResponse("Limite de consultas excedido para este IP."),
                cancellationToken)
            .ConfigureAwait(false);
    };
});
builder.Services.AddSefazNFeService(
    certificado,
    options.Timeout,
    options.IgnoreServerCertificateErrors);
builder.Services.AddSefazStatusService(
    certificado,
    options.Timeout,
    options.IgnoreServerCertificateErrors);

builder.Services.AddTransient<NFeConsultaClient>(serviceProvider =>
{
    SefazNFeService service = serviceProvider.GetRequiredService<SefazNFeService>();
    return new NFeConsultaClient(service, options.ToConsultaOptions());
});
builder.Services.AddTransient<NFeStatusClient>(serviceProvider =>
{
    SefazStatusService service = serviceProvider.GetRequiredService<SefazStatusService>();
    return new NFeStatusClient(service, options.ToConsultaOptions());
});

WebApplication app = builder.Build();

app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

app.MapGet("/sefaz/status", async (
    string? correlationId,
    NFeStatusClient client,
    CancellationToken cancellationToken) =>
{
    SefazStatusResult result = await client
        .ConsultarStatusAsync(cancellationToken, correlationId)
        .ConfigureAwait(false);

    return ToHttpStatusResult(result);
}).RequireRateLimiting("sefaz-per-ip");

app.MapPost("/consultar", async (
    ConsultaNFeApiRequest request,
    NFeConsultaClient client,
    CancellationToken cancellationToken) =>
{
    try
    {
        ConsultaNFeResult result;

        if (!string.IsNullOrWhiteSpace(request.ChaveAcesso))
        {
            result = await client
                .ConsultarChaveAsync(request.ChaveAcesso, cancellationToken, request.CorrelationId)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(request.Xml))
        {
            result = await client
                .ConsultarXmlAsync(
                    request.Xml,
                    options.XsdDirectory,
                    cancellationToken,
                    request.CorrelationId)
                .ConfigureAwait(false);
        }
        else
        {
            return Results.BadRequest(new ErrorResponse("Informe chaveAcesso ou xml."));
        }

        return ToHttpConsultaResult(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
    catch (TimeoutException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status504GatewayTimeout);
    }
}).RequireRateLimiting("sefaz-per-ip");

app.Run();

static IResult ToHttpConsultaResult(ConsultaNFeResult result) => result.TipoResultado switch
{
    TipoResultadoConsulta.RequisicaoInvalida or TipoResultadoConsulta.FalhaXml => Results.BadRequest(result),
    TipoResultadoConsulta.Timeout => Results.Json(result, statusCode: StatusCodes.Status504GatewayTimeout),
    TipoResultadoConsulta.FalhaRede or TipoResultadoConsulta.FalhaCertificado or TipoResultadoConsulta.FalhaTecnica =>
        Results.Json(result, statusCode: StatusCodes.Status502BadGateway),
    _ => Results.Ok(result)
};

static IResult ToHttpStatusResult(SefazStatusResult result) => result.TipoResultado switch
{
    TipoResultadoConsulta.RequisicaoInvalida or TipoResultadoConsulta.FalhaXml => Results.BadRequest(result),
    TipoResultadoConsulta.Timeout => Results.Json(result, statusCode: StatusCodes.Status504GatewayTimeout),
    TipoResultadoConsulta.FalhaRede or TipoResultadoConsulta.FalhaCertificado or TipoResultadoConsulta.FalhaTecnica =>
        Results.Json(result, statusCode: StatusCodes.Status502BadGateway),
    _ => Results.Ok(result)
};

static X509Certificate2 ResolveCertificate(SefazApiOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.CertThumbprint))
        return CertificadoProvider.ObterPorThumbprint(options.CertThumbprint);

    if (!string.IsNullOrWhiteSpace(options.CertSerial))
        return CertificadoProvider.ObterPorNumeroSerie(options.CertSerial);

    if (!string.IsNullOrWhiteSpace(options.CertSubject))
        return CertificadoProvider.ObterPorSubject(options.CertSubject);

    if (!string.IsNullOrWhiteSpace(options.CertPfxPath))
        return CertificadoProvider.ObterPorPfx(
            options.CertPfxPath,
            Environment.GetEnvironmentVariable("NFE_CERT_PASSWORD"));

    if (!string.IsNullOrWhiteSpace(options.CertPemPath) || !string.IsNullOrWhiteSpace(options.KeyPemPath))
    {
        if (string.IsNullOrWhiteSpace(options.CertPemPath) || string.IsNullOrWhiteSpace(options.KeyPemPath))
            throw new InvalidOperationException("Configure Sefaz:CertPemPath e Sefaz:KeyPemPath juntos.");

        return CertificadoProvider.ObterPorPem(options.CertPemPath, options.KeyPemPath);
    }

    throw new InvalidOperationException(
        "Configure Sefaz:CertThumbprint, Sefaz:CertSerial, Sefaz:CertSubject, Sefaz:CertPfxPath ou Sefaz:CertPemPath com Sefaz:KeyPemPath.");
}

internal sealed record ConsultaNFeApiRequest(string? ChaveAcesso, string? Xml, string? CorrelationId);

internal sealed record ErrorResponse(string Erro);

internal sealed record HealthResponse(string Status);

internal sealed record SefazApiOptions
{
    public string? CertThumbprint { get; init; }
    public string? CertSerial { get; init; }
    public string? CertSubject { get; init; }
    public string? CertPfxPath { get; init; }
    public string? CertPemPath { get; init; }
    public string? KeyPemPath { get; init; }
    public string? XsdDirectory { get; init; }
    public TipoAmbiente Ambiente { get; init; } = TipoAmbiente.Homologacao;
    public UfNFe? Uf { get; init; }
    public string? UrlWebService { get; init; }
    public string? UrlStatusWebService { get; init; }
    public string? CorrelationId { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public int RetryCount { get; init; } = 2;
    public int RateLimitPermitLimit { get; init; } = 30;
    public TimeSpan RateLimitWindow { get; init; } = TimeSpan.FromMinutes(1);
    public long MaxRequestBodyBytes { get; init; } = XmlInputLimits.DefaultMaxXmlBytes;
    public bool IgnoreServerCertificateErrors { get; init; }

    public NFeConsultaOptions ToConsultaOptions() => new()
    {
        Ambiente = Ambiente,
        Uf = Uf,
        UrlWebServiceOverride = UrlWebService,
        UrlStatusWebServiceOverride = UrlStatusWebService,
        CorrelationId = CorrelationId,
        Timeout = Timeout,
        RetryCount = RetryCount
    };

    public static SefazApiOptions FromConfiguration(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Sefaz");
        string ambiente = section["Ambiente"] ?? "homologacao";
        string? uf = section["UF"];
        int timeoutSeconds = section.GetValue("TimeoutSeconds", 60);
        int retryCount = section.GetValue("RetryCount", 2);
        int rateLimitPermitLimit = section.GetValue("RateLimitPermitLimit", 30);
        int rateLimitWindowSeconds = section.GetValue("RateLimitWindowSeconds", 60);
        long maxRequestBodyBytes = section.GetValue("MaxRequestBodyBytes", XmlInputLimits.DefaultMaxXmlBytes);

        return new SefazApiOptions
        {
            CertThumbprint = section["CertThumbprint"],
            CertSerial = section["CertSerial"],
            CertSubject = section["CertSubject"],
            CertPfxPath = section["CertPfxPath"],
            CertPemPath = section["CertPemPath"],
            KeyPemPath = section["KeyPemPath"],
            XsdDirectory = section["XsdDirectory"],
            Ambiente = ParseAmbiente(ambiente),
            Uf = string.IsNullOrWhiteSpace(uf) ? null : SefazEndpointResolver.ParseUf(uf),
            UrlWebService = section["UrlWebService"],
            UrlStatusWebService = section["UrlStatusWebService"],
            CorrelationId = section["CorrelationId"],
            Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 60),
            RetryCount = retryCount >= 0 ? retryCount : 2,
            RateLimitPermitLimit = rateLimitPermitLimit > 0 ? rateLimitPermitLimit : 30,
            RateLimitWindow = TimeSpan.FromSeconds(rateLimitWindowSeconds > 0 ? rateLimitWindowSeconds : 60),
            MaxRequestBodyBytes = maxRequestBodyBytes > 0
                ? maxRequestBodyBytes
                : XmlInputLimits.DefaultMaxXmlBytes,
            IgnoreServerCertificateErrors = section.GetValue("IgnoreServerCertificateErrors", false)
        };
    }

    private static TipoAmbiente ParseAmbiente(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "1" or "producao" => TipoAmbiente.Producao,
            "2" or "homologacao" => TipoAmbiente.Homologacao,
            _ => throw new InvalidOperationException("Sefaz:Ambiente invalido. Use homologacao ou producao.")
        };
}
