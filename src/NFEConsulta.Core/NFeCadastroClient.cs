using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;

namespace NFEConsulta.Services;

/// <summary>
/// Fachada de alto nivel para realizar consulta de cadastro de contribuintes (CadConsultaCadastro4) na SEFAZ.
/// </summary>
public sealed class NFeCadastroClient : IDisposable
{
    private readonly SefazCadastroService _service;
    private readonly NFeConsultaOptions _options;
    private readonly bool _disposeService;

    public NFeCadastroClient(
        SefazCadastroService service,
        NFeConsultaOptions options,
        bool disposeService = false)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeService = disposeService;
    }

    /// <summary>
    /// Consulta a situacao cadastral de um contribuinte a partir de seu CNPJ, CPF ou IE na UF configurada nas opcoes.
    /// </summary>
    public Task<CadastroConsultaResult> ConsultarCadastroAsync(
        string documento,
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
            return Task.FromResult(CadastroConsultaResult.Erro(
                ex.Message,
                resolvedCorrelationId));
        }

        CadastroConsultaRequest request = new(documento, uf, _options.Ambiente, urlWebService)
        {
            RetryCount = _options.RetryCount,
            CorrelationId = resolvedCorrelationId
        };

        return _service.ConsultarCadastroAsync(request, cancellationToken);
    }

    /// <summary>
    /// Instancia um novo NFeCadastroClient utilizando o certificado digital informado.
    /// </summary>
    public static NFeCadastroClient CriarComCertificado(
        X509Certificate2 certificado,
        NFeConsultaOptions options,
        ILogger<SefazCadastroService>? logger = null,
        bool ignoreServerCertificateErrors = false)
    {
        SefazCadastroService service = SefazCadastroService.CriarComCertificado(
            certificado,
            options.Timeout,
            logger,
            ignoreServerCertificateErrors);

        return new NFeCadastroClient(service, options, disposeService: true);
    }

    private string ResolveUrl(UfNFe uf)
    {
        if (!string.IsNullOrWhiteSpace(_options.UrlWebServiceOverride))
            return _options.UrlWebServiceOverride;

        return SefazEndpointResolver.ResolveCadastro(uf, _options.Ambiente);
    }

    public void Dispose()
    {
        if (_disposeService)
            _service.Dispose();
    }
}
