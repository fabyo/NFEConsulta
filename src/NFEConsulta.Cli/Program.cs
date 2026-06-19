using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

CliOptions options = CliOptions.Parse(args);

if (options.ShowHelp)
{
    CliOptions.PrintHelp();
    return 0;
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(o => o.SingleLine = true);
    builder.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Warning);
});

using CancellationTokenSource cts = new(options.Timeout);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    using X509Certificate2 certificado = ResolveCertificate(options);
    NFeConsultaOptions consultaOptions = options.ToConsultaOptions();

    if (options.Command == CliCommand.Status)
    {
        using NFeStatusClient statusClient = NFeStatusClient.CriarComCertificado(
            certificado,
            consultaOptions,
            loggerFactory.CreateLogger<SefazStatusService>(),
            options.IgnoreServerCertificateErrors);

        SefazStatusResult status = await statusClient
            .ConsultarStatusAsync(cts.Token, options.CorrelationId)
            .ConfigureAwait(false);

        PrintStatus(status);
        return status.Online ? 0 : 2;
    }

    using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(
        certificado,
        consultaOptions,
        loggerFactory.CreateLogger<SefazNFeService>(),
        options.IgnoreServerCertificateErrors);

    string chave = await ResolveChaveAsync(options, cts.Token).ConfigureAwait(false);
    ConsultaNFeResult resultado = await client
        .ConsultarChaveAsync(chave, cts.Token, options.CorrelationId)
        .ConfigureAwait(false);

    PrintResult(resultado, chave);
    return resultado.Sucesso ? 0 : 2;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operacao cancelada por timeout ou pelo usuario.");
    return 124;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FileNotFoundException)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return 1;
}

static X509Certificate2 ResolveCertificate(CliOptions options)
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
            throw new ArgumentException("Informe --cert-pem e --key-pem juntos.");

        return CertificadoProvider.ObterPorPem(options.CertPemPath, options.KeyPemPath);
    }

    throw new ArgumentException(
        "Informe --cert-thumbprint, --cert-serial, --cert-subject, --cert-pfx ou --cert-pem com --key-pem.");
}

static async Task<string> ResolveChaveAsync(CliOptions options, CancellationToken cancellationToken)
{
    if (!string.IsNullOrWhiteSpace(options.ChaveAcesso))
        return ChaveAcessoNFe.RequireValid(options.ChaveAcesso);

    if (!string.IsNullOrWhiteSpace(options.XmlFilePath))
    {
        if (!string.IsNullOrWhiteSpace(options.XsdDirectory))
            await NFeXmlValidator
                .RequireValidXmlFileAsync(options.XmlFilePath, options.XsdDirectory, cancellationToken)
                .ConfigureAwait(false);

        return await ChaveAcessoNFe
            .ExtractFromXmlFileAsync(options.XmlFilePath, cancellationToken)
            .ConfigureAwait(false);
    }

    if (options.ReadXmlFromStdIn)
    {
        using MemoryStream buffer = new();
        await Console.OpenStandardInput().CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(options.XsdDirectory))
        {
            buffer.Position = 0;
            NFeXmlValidationResult validation = await NFeXmlValidator
                .ValidateXmlAsync(buffer, options.XsdDirectory, cancellationToken)
                .ConfigureAwait(false);

            if (!validation.IsValid)
                throw new ArgumentException(
                    "XML nao passou na validacao XSD: " + string.Join(" | ", validation.Errors));
        }

        buffer.Position = 0;
        return await ChaveAcessoNFe
            .ExtractFromXmlAsync(buffer, cancellationToken)
            .ConfigureAwait(false);
    }

    throw new ArgumentException("Informe --chave, --xml-file ou --xml-stdin.");
}

static void PrintResult(ConsultaNFeResult result, string chave)
{
    Console.WriteLine($"Chave: {chave}");
    Console.WriteLine($"Status: {result.Status}");
    Console.WriteLine($"TipoResultado: {result.TipoResultado}");
    Console.WriteLine($"CorrelationId: {result.CorrelationId}");
    Console.WriteLine($"Codigo: {result.CodigoStatus}");
    Console.WriteLine($"Motivo: {result.Motivo}");

    if (result.NumeroProtocolo is not null)
        Console.WriteLine($"Protocolo: {result.NumeroProtocolo}");

    if (result.DataAutorizacao is not null)
        Console.WriteLine($"DataAutorizacao: {result.DataAutorizacao:O}");

    if (!result.Sucesso && !string.IsNullOrWhiteSpace(result.ErroDetalhado))
        Console.WriteLine($"Erro: {result.ErroDetalhado}");
}

static void PrintStatus(SefazStatusResult result)
{
    Console.WriteLine($"UF: {result.Uf}");
    Console.WriteLine($"Ambiente: {result.Ambiente}");
    Console.WriteLine($"Online: {result.Online}");
    Console.WriteLine($"TipoResultado: {result.TipoResultado}");
    Console.WriteLine($"CorrelationId: {result.CorrelationId}");
    Console.WriteLine($"Codigo: {result.CodigoStatus}");
    Console.WriteLine($"Motivo: {result.Motivo}");
    Console.WriteLine($"DuracaoMs: {result.Duracao.TotalMilliseconds:F0}");

    if (!string.IsNullOrWhiteSpace(result.ErroDetalhado))
        Console.WriteLine($"Erro: {result.ErroDetalhado}");
}

internal enum CliCommand
{
    Consultar,
    Status
}

internal sealed record CliOptions
{
    public CliCommand Command { get; init; } = CliCommand.Consultar;
    public string? ChaveAcesso { get; init; }
    public string? XmlFilePath { get; init; }
    public bool ReadXmlFromStdIn { get; init; }
    public string? CertThumbprint { get; init; }
    public string? CertSerial { get; init; }
    public string? CertSubject { get; init; }
    public string? CertPfxPath { get; init; }
    public string? CertPemPath { get; init; }
    public string? KeyPemPath { get; init; }
    public string? XsdDirectory { get; init; }
    public TipoAmbiente Ambiente { get; init; } = TipoAmbiente.Homologacao;
    public UfNFe? Uf { get; init; } = UfNFe.SP;
    public string? UrlWebService { get; init; }
    public string? UrlStatusWebService { get; init; }
    public string? CorrelationId { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public int RetryCount { get; init; } = 2;
    public bool Verbose { get; init; }
    public bool ShowHelp { get; init; }
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

    public static CliOptions Parse(string[] args)
    {
        CliOptions options = new();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            options = arg switch
            {
                "status" => options with { Command = CliCommand.Status },
                "--help" or "-h" => options with { ShowHelp = true },
                "--verbose" or "-v" => options with { Verbose = true },
                "--xml-stdin" => options with { ReadXmlFromStdIn = true },
                "--ignore-server-certificate-errors" => options with { IgnoreServerCertificateErrors = true },
                "--chave" => options with { ChaveAcesso = RequireValue(args, ref i, arg) },
                "--xml-file" => options with { XmlFilePath = RequireValue(args, ref i, arg) },
                "--cert-thumbprint" => options with { CertThumbprint = RequireValue(args, ref i, arg) },
                "--cert-serial" => options with { CertSerial = RequireValue(args, ref i, arg) },
                "--cert-subject" => options with { CertSubject = RequireValue(args, ref i, arg) },
                "--cert-pfx" => options with { CertPfxPath = RequireValue(args, ref i, arg) },
                "--cert-pem" => options with { CertPemPath = RequireValue(args, ref i, arg) },
                "--key-pem" => options with { KeyPemPath = RequireValue(args, ref i, arg) },
                "--xsd-dir" => options with { XsdDirectory = RequireValue(args, ref i, arg) },
                "--ambiente" => options with { Ambiente = ParseAmbiente(RequireValue(args, ref i, arg)) },
                "--uf" => options with { Uf = SefazEndpointResolver.ParseUf(RequireValue(args, ref i, arg)) },
                "--url" => options with { UrlWebService = RequireValue(args, ref i, arg) },
                "--status-url" => options with { UrlStatusWebService = RequireValue(args, ref i, arg) },
                "--correlation-id" => options with { CorrelationId = RequireValue(args, ref i, arg) },
                "--timeout-seconds" => options with { Timeout = TimeSpan.FromSeconds(ParsePositiveInt(RequireValue(args, ref i, arg), arg)) },
                "--retry-count" => options with { RetryCount = ParseNonNegativeInt(RequireValue(args, ref i, arg), arg) },
                _ => throw new ArgumentException($"Argumento desconhecido: {arg}")
            };
        }

        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  nfeconsulta --chave <44 digitos> --cert-thumbprint <thumbprint>");
        Console.WriteLine("  nfeconsulta --xml-file nota.xml --cert-thumbprint <thumbprint>");
        Console.WriteLine("  type nota.xml | nfeconsulta --xml-stdin --cert-thumbprint <thumbprint>");
        Console.WriteLine("  nfeconsulta status --uf SP --cert-thumbprint <thumbprint>");
        Console.WriteLine();
        Console.WriteLine("Parametros:");
        Console.WriteLine("  --chave <valor>             Chave de acesso NF-e com 44 digitos.");
        Console.WriteLine("  --xml-file <path>           Arquivo XML para extrair a chave.");
        Console.WriteLine("  --xml-stdin                 Le XML do stdin para extrair a chave.");
        Console.WriteLine("  --cert-thumbprint <valor>   Thumbprint do certificado em CurrentUser\\My. Recomendado.");
        Console.WriteLine("  --cert-serial <serie>       Serie do certificado em CurrentUser\\My. Fallback.");
        Console.WriteLine("  --cert-subject <texto>      Texto do subject do certificado. Fallback.");
        Console.WriteLine("  --cert-pfx <path>           Arquivo PFX. Senha via NFE_CERT_PASSWORD.");
        Console.WriteLine("  --cert-pem <path>           Arquivo PEM do certificado.");
        Console.WriteLine("  --key-pem <path>            Arquivo PEM da chave privada.");
        Console.WriteLine("  --xsd-dir <path>            Diretorio com XSDs oficiais para validar XML antes da consulta.");
        Console.WriteLine("  --ambiente <homologacao|producao>  Padrao: homologacao.");
        Console.WriteLine("  --uf <sigla>                UF para resolver endpoint. Se omitida, infere pela chave.");
        Console.WriteLine("  --url <url>                 Override avancado da URL do Web Service SEFAZ.");
        Console.WriteLine("  --status-url <url>          Override avancado da URL do Status Servico SEFAZ.");
        Console.WriteLine("  --correlation-id <valor>    Identificador para rastrear consulta em logs e resultado.");
        Console.WriteLine("  --timeout-seconds <n>       Padrao: 60.");
        Console.WriteLine("  --retry-count <n>           Retries curtos para falhas transitorias. Padrao: 2.");
        Console.WriteLine("  --verbose                   Logs detalhados.");
        Console.WriteLine("  --ignore-server-certificate-errors  Ignora erros TLS do servidor. Evite em producao.");
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            throw new ArgumentException($"Informe um valor para {optionName}.");

        index++;
        return args[index];
    }

    private static TipoAmbiente ParseAmbiente(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "1" or "producao" or "producao" => TipoAmbiente.Producao,
            "2" or "homologacao" or "homologacao" => TipoAmbiente.Homologacao,
            _ => throw new ArgumentException("Ambiente invalido. Use homologacao ou producao.")
        };

    private static int ParsePositiveInt(string value, string optionName) =>
        int.TryParse(value, out int result) && result > 0
            ? result
            : throw new ArgumentException($"{optionName} deve ser um numero inteiro positivo.");

    private static int ParseNonNegativeInt(string value, string optionName) =>
        int.TryParse(value, out int result) && result >= 0
            ? result
            : throw new ArgumentException($"{optionName} deve ser um numero inteiro maior ou igual a zero.");
}
