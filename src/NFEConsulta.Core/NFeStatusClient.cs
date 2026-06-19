using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

public sealed class NFeStatusClient : IDisposable
{
    private readonly SefazStatusService _service;
    private readonly NFeConsultaOptions _options;
    private readonly bool _disposeService;

    public NFeStatusClient(
        SefazStatusService service,
        NFeConsultaOptions options,
        bool disposeService = false)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeService = disposeService;
    }

    public Task<SefazStatusResult> ConsultarStatusAsync(
        CancellationToken cancellationToken = default,
        string? correlationId = null)
    {
        UfNFe uf = _options.Uf ?? UfNFe.SP;
        string? resolvedCorrelationId = correlationId ?? _options.CorrelationId;
        string urlWebService;

        try
        {
            urlWebService = ResolveUrl(uf);
        }
        catch (NotSupportedException ex)
        {
            return Task.FromResult(SefazStatusResult.Erro(
                ex.Message,
                uf,
                _options.Ambiente,
                string.Empty,
                TimeSpan.Zero,
                TipoResultadoConsulta.RequisicaoInvalida,
                correlationId: resolvedCorrelationId));
        }

        SefazStatusRequest request = new(uf, _options.Ambiente, urlWebService)
        {
            RetryCount = _options.RetryCount,
            CorrelationId = resolvedCorrelationId
        };

        return _service.ConsultarStatusAsync(request, cancellationToken);
    }

    public static NFeStatusClient CriarComCertificado(
        X509Certificate2 certificado,
        NFeConsultaOptions options,
        ILogger<SefazStatusService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        SefazStatusService service = SefazStatusService.CriarComCertificado(
            certificado,
            options.Timeout,
            logger,
            ignoreServerCertificateErrors);

        return new NFeStatusClient(service, options, disposeService: true);
    }

    private string ResolveUrl(UfNFe uf)
    {
        if (!string.IsNullOrWhiteSpace(_options.UrlStatusWebServiceOverride))
            return _options.UrlStatusWebServiceOverride;

        return SefazEndpointResolver.ResolveStatusServico(uf, _options.Ambiente);
    }

    public void Dispose()
    {
        if (_disposeService)
            _service.Dispose();
    }
}
