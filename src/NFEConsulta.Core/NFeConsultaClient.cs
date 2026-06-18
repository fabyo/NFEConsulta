using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

/// <summary>
/// Fachada de alto nivel para incorporar consulta de NF-e em outros projetos C#.
/// Aceita chave diretamente ou XML, mas sempre consulta a SEFAZ pela chave validada.
/// </summary>
public sealed class NFeConsultaClient : IDisposable
{
    private readonly SefazNFeService _service;
    private readonly TipoAmbiente _ambiente;
    private readonly string _urlWebService;
    private readonly bool _disposeService;

    public NFeConsultaClient(
        SefazNFeService service,
        TipoAmbiente ambiente,
        string urlWebService,
        bool disposeService = false)
    {
        _service = service;
        _ambiente = ambiente;
        _urlWebService = string.IsNullOrWhiteSpace(urlWebService)
            ? throw new ArgumentException("URL do Web Service nao informada.", nameof(urlWebService))
            : urlWebService;
        _disposeService = disposeService;
    }

    public Task<ConsultaNFeResult> ConsultarChaveAsync(
        string chaveAcesso,
        CancellationToken cancellationToken = default)
    {
        string chaveValidada = ChaveAcessoNFe.RequireValid(chaveAcesso);
        ConsultaNFeRequest request = new(chaveValidada, _ambiente, _urlWebService);
        return _service.ConsultarAsync(request, cancellationToken);
    }

    public Task<ConsultaNFeResult> ConsultarXmlAsync(
        string xml,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(xsdDirectory))
            NFeXmlValidator.RequireValidXml(xml, xsdDirectory);

        string chave = ChaveAcessoNFe.ExtractFromXml(xml);
        return ConsultarChaveAsync(chave, cancellationToken);
    }

    public async Task<ConsultaNFeResult> ConsultarXmlAsync(
        Stream xmlStream,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (xmlStream is null)
            throw new ArgumentNullException(nameof(xmlStream));

        using MemoryStream buffer = new();
        await xmlStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(xsdDirectory))
        {
            buffer.Position = 0;
            NFeXmlValidationResult validation = await NFeXmlValidator
                .ValidateXmlAsync(buffer, xsdDirectory, cancellationToken)
                .ConfigureAwait(false);

            if (!validation.IsValid)
                throw new ArgumentException(
                    "XML nao passou na validacao XSD: " + string.Join(" | ", validation.Errors));
        }

        buffer.Position = 0;
        string chave = await ChaveAcessoNFe
            .ExtractFromXmlAsync(buffer, cancellationToken)
            .ConfigureAwait(false);

        return await ConsultarChaveAsync(chave, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConsultaNFeResult> ConsultarXmlFileAsync(
        string xmlFilePath,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(xsdDirectory))
            await NFeXmlValidator
                .RequireValidXmlFileAsync(xmlFilePath, xsdDirectory, cancellationToken)
                .ConfigureAwait(false);

        string chave = await ChaveAcessoNFe
            .ExtractFromXmlFileAsync(xmlFilePath, cancellationToken)
            .ConfigureAwait(false);

        return await ConsultarChaveAsync(chave, cancellationToken).ConfigureAwait(false);
    }

    public static NFeConsultaClient CriarComCertificado(
        X509Certificate2 certificado,
        TipoAmbiente ambiente,
        string urlWebService,
        TimeSpan? timeout = null,
        ILogger<SefazNFeService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        SefazNFeService service = SefazNFeService.CriarComCertificado(
            certificado,
            timeout,
            logger,
            ignoreServerCertificateErrors);

        return new NFeConsultaClient(service, ambiente, urlWebService, disposeService: true);
    }

    public void Dispose()
    {
        if (_disposeService)
            _service.Dispose();
    }
}
