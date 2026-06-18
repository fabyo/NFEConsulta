using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Extensions;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

SefazApiOptions options = SefazApiOptions.FromConfiguration(builder.Configuration);
using X509Certificate2 certificado = ResolveCertificate(options);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSefazNFeService(
    certificado,
    options.Timeout,
    options.IgnoreServerCertificateErrors);

builder.Services.AddTransient<NFeConsultaClient>(serviceProvider =>
{
    SefazNFeService service = serviceProvider.GetRequiredService<SefazNFeService>();
    return new NFeConsultaClient(service, options.Ambiente, options.UrlWebService);
});

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")));

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
                .ConsultarChaveAsync(request.ChaveAcesso, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(request.Xml))
        {
            if (!string.IsNullOrWhiteSpace(options.XsdDirectory))
                NFeXmlValidator.RequireValidXml(request.Xml, options.XsdDirectory);

            result = await client
                .ConsultarXmlAsync(request.Xml, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            return Results.BadRequest(new ErrorResponse("Informe chaveAcesso ou xml."));
        }

        return Results.Ok(result);
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
});

app.Run();

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

internal sealed record ConsultaNFeApiRequest(string? ChaveAcesso, string? Xml);

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
    public string UrlWebService { get; init; } = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public bool IgnoreServerCertificateErrors { get; init; }

    public static SefazApiOptions FromConfiguration(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Sefaz");
        string ambiente = section["Ambiente"] ?? "homologacao";
        int timeoutSeconds = section.GetValue("TimeoutSeconds", 60);

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
            UrlWebService = section["UrlWebService"]
                ?? "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
            Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 60),
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
