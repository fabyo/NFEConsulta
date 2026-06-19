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
    private readonly NFeConsultaOptions _options;
    private readonly bool _disposeService;

    public NFeConsultaClient(
        SefazNFeService service,
        NFeConsultaOptions options,
        bool disposeService = false)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeService = disposeService;
    }

    public Task<ConsultaNFeResult> ConsultarChaveAsync(
        string chaveAcesso,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        string? resolvedCorrelationId = correlationId ?? _options.CorrelationId;
        ChaveAcessoValidationResult validation = ChaveAcessoNFe.Validate(chaveAcesso);
        if (!validation.IsValid)
        {
            return Task.FromResult(ConsultaNFeResult.Erro(
                validation.ErrorMessage ?? "Chave de acesso invalida.",
                TipoResultadoConsulta.RequisicaoInvalida,
                correlationId: resolvedCorrelationId));
        }

        string chaveValidada = validation.ChaveAcesso!;
        string urlWebService;
        try
        {
            urlWebService = ResolveUrl(chaveValidada);
        }
        catch (NotSupportedException ex)
        {
            return Task.FromResult(ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.RequisicaoInvalida,
                correlationId: resolvedCorrelationId));
        }

        ConsultaNFeRequest request = new(
            chaveValidada,
            _options.Ambiente,
            urlWebService)
        {
            RetryCount = _options.RetryCount,
            CorrelationId = resolvedCorrelationId
        };

        return _service.ConsultarAsync(request, cancellationToken);
    }

    public Task<ConsultaNFeResult> ConsultarXmlAsync(
        string xml,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        string? resolvedCorrelationId = correlationId ?? _options.CorrelationId;
        try
        {
            if (!string.IsNullOrWhiteSpace(xsdDirectory))
                NFeXmlValidator.RequireValidXml(xml, xsdDirectory);

            string chave = ChaveAcessoNFe.ExtractFromXml(xml);
            return ConsultarChaveAsync(chave, cancellationToken, resolvedCorrelationId);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.FalhaXml,
                correlationId: resolvedCorrelationId));
        }
    }

    public async Task<ConsultaNFeResult> ConsultarXmlAsync(
        Stream xmlStream,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        string? resolvedCorrelationId = correlationId ?? _options.CorrelationId;
        if (xmlStream is null)
            throw new ArgumentNullException(nameof(xmlStream));

        using MemoryStream buffer = new();
        await xmlStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!string.IsNullOrWhiteSpace(xsdDirectory))
            {
                buffer.Position = 0;
                NFeXmlValidationResult validation = await NFeXmlValidator
                    .ValidateXmlAsync(buffer, xsdDirectory, cancellationToken)
                    .ConfigureAwait(false);

                if (!validation.IsValid)
                    return ConsultaNFeResult.Erro(
                        "XML nao passou na validacao XSD: " + string.Join(" | ", validation.Errors),
                        TipoResultadoConsulta.FalhaXml,
                        correlationId: resolvedCorrelationId);
            }

            buffer.Position = 0;
            string chave = await ChaveAcessoNFe
                .ExtractFromXmlAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            return await ConsultarChaveAsync(chave, cancellationToken, resolvedCorrelationId).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.FalhaXml,
                correlationId: resolvedCorrelationId);
        }
    }

    public async Task<ConsultaNFeResult> ConsultarXmlFileAsync(
        string xmlFilePath,
        string? xsdDirectory = null,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        string? resolvedCorrelationId = correlationId ?? _options.CorrelationId;
        try
        {
            if (!string.IsNullOrWhiteSpace(xsdDirectory))
                await NFeXmlValidator
                    .RequireValidXmlFileAsync(xmlFilePath, xsdDirectory, cancellationToken)
                    .ConfigureAwait(false);

            string chave = await ChaveAcessoNFe
                .ExtractFromXmlFileAsync(xmlFilePath, cancellationToken)
                .ConfigureAwait(false);

            return await ConsultarChaveAsync(chave, cancellationToken, resolvedCorrelationId).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return ConsultaNFeResult.Erro(
                ex.Message,
                TipoResultadoConsulta.FalhaXml,
                correlationId: resolvedCorrelationId);
        }
    }

    public static NFeConsultaClient CriarComCertificado(
        X509Certificate2 certificado,
        NFeConsultaOptions options,
        ILogger<SefazNFeService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        SefazNFeService service = SefazNFeService.CriarComCertificado(
            certificado,
            options.Timeout,
            logger,
            ignoreServerCertificateErrors);

        return new NFeConsultaClient(service, options, disposeService: true);
    }

    public static NFeConsultaClient CriarComCertificado(
        X509Certificate2 certificado,
        TipoAmbiente ambiente,
        string urlWebService,
        TimeSpan? timeout = null,
        ILogger<SefazNFeService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        NFeConsultaOptions options = new()
        {
            Ambiente = ambiente,
            UrlWebServiceOverride = urlWebService,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        SefazNFeService service = SefazNFeService.CriarComCertificado(
            certificado,
            options.Timeout,
            logger,
            ignoreServerCertificateErrors);

        return new NFeConsultaClient(service, options, disposeService: true);
    }

    private string ResolveUrl(string chaveAcesso)
    {
        if (!string.IsNullOrWhiteSpace(_options.UrlWebServiceOverride))
            return _options.UrlWebServiceOverride;

        UfNFe uf = _options.Uf ?? SefazEndpointResolver.InferUfFromChave(chaveAcesso);
        return SefazEndpointResolver.ResolveConsultaProtocolo(uf, _options.Ambiente);
    }

    public void Dispose()
    {
        if (_disposeService)
            _service.Dispose();
    }
}
