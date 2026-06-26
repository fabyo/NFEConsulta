using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

List<(string Name, Func<Task> Run)> tests =
[
    ("Chave valida normaliza e valida DV", TestChaveValida),
    ("Chave invalida retorna erro", TestChaveInvalida),
    ("Extrai chave do XML por chNFe", TestExtraiChavePorChNFe),
    ("Extrai chave do XML por infNFe Id", TestExtraiChavePorInfNFeId),
    ("Parser reconhece autorizada", TestParserAutorizada),
    ("Parser reconhece cancelada", TestParserCancelada),
    ("Parser reconhece nao encontrada", TestParserNaoEncontrada),
    ("Parser classifica XML invalido", TestParserXmlInvalido),
    ("Resultado orienta politica de retry", TestPoliticaResultadoConsulta),
    ("Status orienta reagendamento fiscal", TestPoliticaStatusSefaz),
    ("Endpoint resolve SP default", TestEndpointSp),
    ("Endpoint status resolve SP default", TestEndpointStatusSp),
    ("Endpoint resolve UF por chave", TestEndpointPorChave),
    ("Tabela de endpoints cobre todas as UFs", TestTabelaEndpointsCobreTodasUfs),
    ("Tabela de status cobre todas as UFs", TestTabelaStatusCobreTodasUfs),
    ("Endpoint SP marcado como validado localmente", TestEndpointSpValidadoLocalmente),
    ("UF invalida lanca excecao", TestUfInvalidaExcecao),
    ("Client retorna erro para UF invalida", TestClientUfInvalida),
    ("Override permite UF nao mapeada", TestOverridePermiteUfNaoMapeada),
    ("Parser de status reconhece SEFAZ online", TestParserStatusOnline),
    ("Status client retorna erro para UF invalida", TestStatusClientUfInvalida),
    ("Status service faz retry em falha transitoria", TestStatusRetryTransitorio),
    ("CorrelationId propaga na consulta", TestCorrelationIdConsulta),
    ("CorrelationId propaga no status", TestCorrelationIdStatus),
    ("Client retorna falha XML sem excecao", TestClientXmlInvalido),
    ("Servico faz retry em falha transitoria", TestRetryTransitorio),
    ("Integracao real opcional por env", TestIntegracaoRealOpcional),
    ("Parser de Cadastro reconhece resposta com IE localizada", TestParserCadastro),
    ("Cadastro client realiza consulta com sucesso", TestCadastroClient)
];

int failures = 0;

foreach ((string name, Func<Task> run) in tests)
{
    try
    {
        await run().ConfigureAwait(false);
        Console.WriteLine($"PASS {name}");
    }
    catch (SkipTestException ex)
    {
        Console.WriteLine($"SKIP {name}: {ex.Message}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

return failures == 0 ? 0 : 1;

static Task TestChaveValida()
{
    ChaveAcessoValidationResult result = ChaveAcessoNFe.Validate("3526 0600 0000 0000 0100 5500 1000 0000 0110 0000 0006");

    Assert.True(result.IsValid, result.ErrorMessage ?? "Chave deveria ser valida.");
    Assert.Equal(TestData.Chave, result.ChaveAcesso);
    return Task.CompletedTask;
}

static Task TestChaveInvalida()
{
    ChaveAcessoValidationResult result = ChaveAcessoNFe.Validate(TestData.Chave[..43] + "9");

    Assert.False(result.IsValid, "Chave com DV errado deveria falhar.");
    Assert.Contains("Digito verificador", result.ErrorMessage);
    return Task.CompletedTask;
}

static Task TestExtraiChavePorChNFe()
{
    string chave = ChaveAcessoNFe.ExtractFromXml(TestData.XmlComChNFe);

    Assert.Equal(TestData.Chave, chave);
    return Task.CompletedTask;
}

static Task TestExtraiChavePorInfNFeId()
{
    string chave = ChaveAcessoNFe.ExtractFromXml(TestData.XmlComInfNFeId);

    Assert.Equal(TestData.Chave, chave);
    return Task.CompletedTask;
}

static Task TestParserAutorizada()
{
    ConsultaNFeResult result = SefazResponseParser.Parse(TestData.RespostaAutorizada);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Consulta deveria ser sucesso.");
    Assert.Equal(StatusNFe.Autorizada, result.Status);
    Assert.Equal(TipoResultadoConsulta.ConsultaOk, result.TipoResultado);
    Assert.True(result.RespostaSefazRecebida, "Resposta SEFAZ deveria estar marcada.");
    Assert.Equal("123456789012345", result.NumeroProtocolo);
    return Task.CompletedTask;
}

static Task TestParserCancelada()
{
    ConsultaNFeResult result = SefazResponseParser.Parse(TestData.RespostaCancelada);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Consulta deveria ser sucesso.");
    Assert.Equal(StatusNFe.Cancelada, result.Status);
    Assert.Equal(TipoResultadoConsulta.ConsultaOk, result.TipoResultado);
    Assert.Equal("Cancelamento registrado", result.Motivo);
    return Task.CompletedTask;
}

static Task TestParserNaoEncontrada()
{
    ConsultaNFeResult result = SefazResponseParser.Parse(TestData.RespostaNaoEncontrada);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Resposta fiscal conhecida deveria ser sucesso operacional.");
    Assert.Equal(StatusNFe.NaoEncontrada, result.Status);
    Assert.Equal(TipoResultadoConsulta.RespostaSefazNegativa, result.TipoResultado);
    return Task.CompletedTask;
}

static Task TestParserXmlInvalido()
{
    ConsultaNFeResult result = SefazResponseParser.Parse("<retConsSitNFe>");

    Assert.False(result.Sucesso, "XML invalido deveria falhar.");
    Assert.Equal(TipoResultadoConsulta.FalhaXml, result.TipoResultado);
    Assert.True(result.RespostaSefazRecebida, "A falha veio da resposta recebida.");
    return Task.CompletedTask;
}

static Task TestPoliticaResultadoConsulta()
{
    ConsultaNFeResult timeout = ConsultaNFeResult.Erro("timeout", TipoResultadoConsulta.Timeout);
    ConsultaNFeResult xml = ConsultaNFeResult.Erro("xml", TipoResultadoConsulta.FalhaXml);
    ConsultaNFeResult certificado = ConsultaNFeResult.Erro("certificado", TipoResultadoConsulta.FalhaCertificado);

    Assert.True(timeout.DeveRetentarComBackoff, "Timeout deveria orientar retry com backoff.");
    Assert.False(xml.DeveRetentarComBackoff, "FalhaXml nao deveria orientar retry automatico.");
    Assert.True(xml.RequerCorrecaoDeEntrada, "FalhaXml deveria exigir correcao de entrada.");
    Assert.True(certificado.RequerAlertaOperacional, "FalhaCertificado deveria exigir alerta operacional.");

    return Task.CompletedTask;
}

static Task TestPoliticaStatusSefaz()
{
    SefazStatusResult processamento = SefazStatusResponseParser.Parse(
        TestData.RespostaStatusProcessamento,
        UfNFe.SP,
        TipoAmbiente.Homologacao,
        "https://sefaz.test",
        TimeSpan.FromMilliseconds(100));

    Assert.False(processamento.Online, "Status 108 nao deveria indicar online.");
    Assert.True(processamento.ServicoEmProcessamento, "Status 108 deveria indicar servico em processamento.");
    Assert.True(processamento.DeveReagendarProcessamento, "Status 108 deveria orientar reagendamento.");

    return Task.CompletedTask;
}

static Task TestEndpointSp()
{
    string url = SefazEndpointResolver.ResolveConsultaProtocolo(UfNFe.SP, TipoAmbiente.Homologacao);

    Assert.Equal("https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx", url);
    return Task.CompletedTask;
}

static Task TestEndpointStatusSp()
{
    string url = SefazEndpointResolver.ResolveStatusServico(UfNFe.SP, TipoAmbiente.Homologacao);

    Assert.Equal("https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx", url);
    return Task.CompletedTask;
}

static Task TestEndpointPorChave()
{
    UfNFe uf = SefazEndpointResolver.InferUfFromChave(TestData.Chave);

    Assert.Equal(UfNFe.SP, uf);
    return Task.CompletedTask;
}

static Task TestTabelaEndpointsCobreTodasUfs()
{
    UfNFe[] ufs = Enum.GetValues<UfNFe>();
    IReadOnlyCollection<SefazEndpointInfo> endpoints = SefazEndpointResolver.ListarEndpointsConsultaProtocolo();

    Assert.Equal(ufs.Length, endpoints.Count);
    foreach (UfNFe uf in ufs)
        Assert.True(endpoints.Any(endpoint => endpoint.Uf == uf), $"UF {uf} nao esta na tabela de endpoints.");

    return Task.CompletedTask;
}

static Task TestTabelaStatusCobreTodasUfs()
{
    UfNFe[] ufs = Enum.GetValues<UfNFe>();
    IReadOnlyCollection<SefazEndpointInfo> endpoints = SefazEndpointResolver.ListarEndpointsStatusServico();

    Assert.Equal(ufs.Length, endpoints.Count);
    foreach (UfNFe uf in ufs)
        Assert.True(endpoints.Any(endpoint => endpoint.Uf == uf), $"UF {uf} nao esta na tabela de status.");

    return Task.CompletedTask;
}

static Task TestEndpointSpValidadoLocalmente()
{
    SefazEndpointInfo consulta = SefazEndpointResolver
        .ListarEndpointsConsultaProtocolo()
        .Single(endpoint => endpoint.Uf == UfNFe.SP);
    SefazEndpointInfo status = SefazEndpointResolver
        .ListarEndpointsStatusServico()
        .Single(endpoint => endpoint.Uf == UfNFe.SP);

    Assert.Equal(ValidacaoEndpoint.ValidadoLocalmente, consulta.Validacao);
    Assert.Equal(ValidacaoEndpoint.ValidadoLocalmente, status.Validacao);
    return Task.CompletedTask;
}

static Task TestUfInvalidaExcecao()
{
    Assert.Throws<NotSupportedException>(() =>
        SefazEndpointResolver.ResolveConsultaProtocolo((UfNFe)99, TipoAmbiente.Homologacao));

    return Task.CompletedTask;
}

static async Task TestClientUfInvalida()
{
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
    SefazNFeService service = new(httpClient, NullLogger<SefazNFeService>.Instance);
    using NFeConsultaClient client = new(service, new NFeConsultaOptions { Uf = (UfNFe)99 });

    ConsultaNFeResult result = await client.ConsultarChaveAsync(TestData.Chave).ConfigureAwait(false);

    Assert.False(result.Sucesso, "UF invalida deveria retornar erro.");
    Assert.Equal(TipoResultadoConsulta.RequisicaoInvalida, result.TipoResultado);
    Assert.Contains("nao possui endpoint", result.ErroDetalhado);
}

static async Task TestOverridePermiteUfNaoMapeada()
{
    int calls = 0;
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
    {
        calls++;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TestData.RespostaAutorizada, Encoding.UTF8, "application/xml")
        };
    }));

    SefazNFeService service = new(httpClient, NullLogger<SefazNFeService>.Instance);
    using NFeConsultaClient client = new(
        service,
        new NFeConsultaOptions
        {
            Uf = (UfNFe)99,
            UrlWebServiceOverride = "https://sefaz.test"
        });

    ConsultaNFeResult result = await client.ConsultarChaveAsync(TestData.Chave).ConfigureAwait(false);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Override deveria permitir consulta.");
    Assert.Equal(1, calls);
}

static Task TestParserStatusOnline()
{
    SefazStatusResult result = SefazStatusResponseParser.Parse(
        TestData.RespostaStatusOnline,
        UfNFe.SP,
        TipoAmbiente.Homologacao,
        "https://sefaz.test",
        TimeSpan.FromMilliseconds(120));

    Assert.True(result.Online, "Status 107 deveria indicar SEFAZ online.");
    Assert.Equal("107", result.CodigoStatus);
    Assert.Equal(TipoResultadoConsulta.ConsultaOk, result.TipoResultado);
    Assert.True(result.RespostaSefazRecebida, "Resposta SEFAZ deveria estar marcada.");
    return Task.CompletedTask;
}

static async Task TestStatusClientUfInvalida()
{
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
    SefazStatusService service = new(httpClient, NullLogger<SefazStatusService>.Instance);
    using NFeStatusClient client = new(service, new NFeConsultaOptions { Uf = (UfNFe)99 });

    SefazStatusResult result = await client.ConsultarStatusAsync().ConfigureAwait(false);

    Assert.False(result.Online, "UF invalida deveria retornar offline.");
    Assert.Equal(TipoResultadoConsulta.RequisicaoInvalida, result.TipoResultado);
    Assert.Contains("nao possui endpoint", result.ErroDetalhado);
}

static async Task TestStatusRetryTransitorio()
{
    int calls = 0;
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
    {
        calls++;

        if (calls == 1)
            throw new HttpRequestException("Falha simulada.");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TestData.RespostaStatusOnline, Encoding.UTF8, "application/xml")
        };
    }));

    SefazStatusService service = new(httpClient, NullLogger<SefazStatusService>.Instance);
    SefazStatusResult result = await service
        .ConsultarStatusAsync(new SefazStatusRequest(UfNFe.SP, TipoAmbiente.Homologacao, "https://sefaz.test")
        {
            RetryCount = 1
        })
        .ConfigureAwait(false);

    Assert.True(result.Online, result.ErroDetalhado ?? "Status deveria ficar online apos retry.");
    Assert.Equal(2, calls);
}

static async Task TestCorrelationIdConsulta()
{
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(TestData.RespostaAutorizada, Encoding.UTF8, "application/xml")
    }));

    SefazNFeService service = new(httpClient, NullLogger<SefazNFeService>.Instance);
    using NFeConsultaClient client = new(
        service,
        new NFeConsultaOptions
        {
            UrlWebServiceOverride = "https://sefaz.test",
            CorrelationId = "job-123"
        });

    ConsultaNFeResult result = await client.ConsultarChaveAsync(TestData.Chave).ConfigureAwait(false);

    Assert.Equal("job-123", result.CorrelationId);
}

static async Task TestCorrelationIdStatus()
{
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(TestData.RespostaStatusOnline, Encoding.UTF8, "application/xml")
    }));

    SefazStatusService service = new(httpClient, NullLogger<SefazStatusService>.Instance);
    using NFeStatusClient client = new(
        service,
        new NFeConsultaOptions
        {
            UrlStatusWebServiceOverride = "https://sefaz.test",
            CorrelationId = "status-456"
        });

    SefazStatusResult result = await client.ConsultarStatusAsync().ConfigureAwait(false);

    Assert.Equal("status-456", result.CorrelationId);
}

static async Task TestClientXmlInvalido()
{
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
    SefazNFeService service = new(httpClient, NullLogger<SefazNFeService>.Instance);
    using NFeConsultaClient client = new(service, new NFeConsultaOptions());

    ConsultaNFeResult result = await client.ConsultarXmlAsync("<NFe>").ConfigureAwait(false);

    Assert.False(result.Sucesso, "XML invalido deveria retornar falha.");
    Assert.Equal(TipoResultadoConsulta.FalhaXml, result.TipoResultado);
}

static async Task TestRetryTransitorio()
{
    int calls = 0;
    using HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
    {
        calls++;

        if (calls == 1)
            throw new HttpRequestException("Falha simulada.");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TestData.RespostaAutorizada, Encoding.UTF8, "application/xml")
        };
    }));

    SefazNFeService service = new(httpClient, NullLogger<SefazNFeService>.Instance);
    ConsultaNFeResult result = await service
        .ConsultarAsync(new ConsultaNFeRequest(TestData.Chave, TipoAmbiente.Homologacao, "https://sefaz.test")
        {
            RetryCount = 1
        })
        .ConfigureAwait(false);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Consulta deveria ter sucesso apos retry.");
    Assert.Equal(2, calls);
}

static async Task TestIntegracaoRealOpcional()
{
    if (!string.Equals(Environment.GetEnvironmentVariable("NFE_RUN_INTEGRATION"), "1", StringComparison.Ordinal))
        throw new SkipTestException("defina NFE_RUN_INTEGRATION=1 para rodar consulta real.");

    string certPemPath = Environment.GetEnvironmentVariable("NFE_CERT_PEM") ?? Path.Combine("certs", "cert.pem");
    string keyPemPath = Environment.GetEnvironmentVariable("NFE_KEY_PEM") ?? Path.Combine("certs", "key.pem");
    string xmlPath = Environment.GetEnvironmentVariable("NFE_XML_PATH") ?? "xml";

    if (!File.Exists(certPemPath) || !File.Exists(keyPemPath))
        throw new SkipTestException("certificado PEM local nao encontrado.");

    string[] xmlFiles = ResolveXmlFiles(xmlPath);
    if (xmlFiles.Length == 0)
        throw new SkipTestException("nenhum XML local encontrado para integracao.");

    using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(certPemPath, keyPemPath);
    NFeConsultaOptions options = new()
    {
        Ambiente = ParseAmbiente(Environment.GetEnvironmentVariable("NFE_AMBIENTE") ?? "homologacao"),
        Uf = ParseUfOrDefault(Environment.GetEnvironmentVariable("NFE_UF")),
        Timeout = TimeSpan.FromSeconds(ParseIntOrDefault(Environment.GetEnvironmentVariable("NFE_TIMEOUT_SECONDS"), 60)),
        RetryCount = ParseIntOrDefault(Environment.GetEnvironmentVariable("NFE_RETRY_COUNT"), 2)
    };

    using NFeStatusClient statusClient = NFeStatusClient.CriarComCertificado(certificado, options);
    SefazStatusResult status = await statusClient.ConsultarStatusAsync().ConfigureAwait(false);

    Assert.False(status.EhFalhaTecnica, status.ErroDetalhado ?? "Status real nao deveria retornar falha tecnica.");
    Assert.True(status.RespostaSefazRecebida, "Status real deveria receber resposta da SEFAZ.");

    using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(certificado, options);
    foreach (string file in xmlFiles)
    {
        ConsultaNFeResult result = await client.ConsultarXmlFileAsync(file).ConfigureAwait(false);

        Assert.False(result.TipoResultado == TipoResultadoConsulta.Desconhecido, "Resultado real nao deveria ficar desconhecido.");
        Assert.False(result.EhFalhaTecnica, result.ErroDetalhado ?? "Consulta real nao deveria retornar falha tecnica.");
        Assert.True(result.RespostaSefazRecebida, "Consulta real deveria receber resposta da SEFAZ.");
    }
}

static string[] ResolveXmlFiles(string xmlPath)
{
    if (File.Exists(xmlPath))
        return [xmlPath];

    return Directory.Exists(xmlPath)
        ? Directory.GetFiles(xmlPath, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray()
        : [];
}

static TipoAmbiente ParseAmbiente(string value) => value.Trim().ToLowerInvariant() switch
{
    "1" or "producao" => TipoAmbiente.Producao,
    "2" or "homologacao" => TipoAmbiente.Homologacao,
    _ => throw new ArgumentException("NFE_AMBIENTE invalido.")
};

static UfNFe? ParseUfOrDefault(string? value) =>
    string.IsNullOrWhiteSpace(value) ? UfNFe.SP : SefazEndpointResolver.ParseUf(value);

static int ParseIntOrDefault(string? value, int fallback) =>
    int.TryParse(value, out int parsed) && parsed >= 0 ? parsed : fallback;

static Task TestParserCadastro()
{
    CadastroConsultaResult result = SefazCadastroResponseParser.Parse(TestData.RespostaCadastroLocalizado);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Cadastro deveria ser sucesso.");
    Assert.Equal("111", result.CodigoStatus);
    Assert.Equal(1, result.Contribuintes.Count);

    var cad = result.Contribuintes[0];
    Assert.Equal("110042490114", cad.IE);
    Assert.Equal("12345678901234", cad.CNPJ);
    Assert.Equal("SP", cad.UF);
    Assert.Equal("Habilitado", cad.Situacao);
    Assert.Equal("EMPRESA TESTE LTDA", cad.Nome);
    Assert.Equal("TESTE ME", cad.NomeFantasia);
    Assert.Equal("AVENIDA PAULISTA", cad.Endereco?.Logradouro);
    Assert.Equal("1000", cad.Endereco?.Numero);
    return Task.CompletedTask;
}

static async Task TestCadastroClient()
{
    using SefazCadastroService service = new(
        new HttpClient(new StubHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TestData.RespostaCadastroLocalizado, Encoding.UTF8, "application/soap+xml")
            };
        })),
        NullLogger<SefazCadastroService>.Instance);

    NFeConsultaOptions options = new()
    {
        Ambiente = TipoAmbiente.Homologacao,
        Uf = UfNFe.SP
    };

    using NFeCadastroClient client = new(service, options);
    CadastroConsultaResult result = await client.ConsultarCadastroAsync("12345678901234").ConfigureAwait(false);

    Assert.True(result.Sucesso, result.ErroDetalhado ?? "Deveria conseguir consultar.");
    Assert.Equal("111", result.CodigoStatus);
    Assert.Equal(1, result.Contribuintes.Count);
}

internal static class TestData
{
    public const string RespostaCadastroLocalizado = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>
            <nfeResultMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/CadConsultaCadastro4">
              <retConsCad xmlns="http://www.portalfiscal.inf.br/nfe" versao="2.00">
                <infCons>
                  <verAplic>SP-MD-26-06</verAplic>
                  <cStat>111</cStat>
                  <xMotivo>Consulta cadastro com uma IE localizada</xMotivo>
                  <UF>SP</UF>
                  <CNPJ>12345678901234</CNPJ>
                  <dhCons>2026-06-26T04:00:00-03:00</dhCons>
                  <cUF>35</cUF>
                  <infCad>
                    <IE>110042490114</IE>
                    <CNPJ>12345678901234</CNPJ>
                    <UF>SP</UF>
                    <cSit>1</cSit>
                    <xNome>EMPRESA TESTE LTDA</xNome>
                    <xFant>TESTE ME</xFant>
                    <xRegAp>1</xRegAp>
                    <CNAE>4711302</CNAE>
                    <dIniAtiv>2000-01-01</dIniAtiv>
                    <ender>
                      <xLgr>AVENIDA PAULISTA</xLgr>
                      <nro>1000</nro>
                      <xCpl>SALA 15</xCpl>
                      <xBairro>BELA VISTA</xBairro>
                      <cMun>3550308</cMun>
                      <xMun>SAO PAULO</xMun>
                      <CEP>01310100</CEP>
                    </ender>
                  </infCad>
                </infCons>
              </retConsCad>
            </nfeResultMsg>
          </soap:Body>
        </soap:Envelope>
        """;

    public const string Chave = "35260600000000000100550010000000011000000006";

    public const string XmlComChNFe = """
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
          <protNFe>
            <infProt>
              <chNFe>35260600000000000100550010000000011000000006</chNFe>
            </infProt>
          </protNFe>
        </nfeProc>
        """;

    public const string XmlComInfNFeId = """
        <NFe xmlns="http://www.portalfiscal.inf.br/nfe">
          <infNFe Id="NFe35260600000000000100550010000000011000000006" versao="4.00" />
        </NFe>
        """;

    public const string RespostaAutorizada = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>
            <nfeResultMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4">
              <retConsSitNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
                <tpAmb>2</tpAmb>
                <verAplic>TEST</verAplic>
                <cStat>100</cStat>
                <xMotivo>Autorizado o uso da NF-e</xMotivo>
                <protNFe>
                  <infProt>
                    <tpAmb>2</tpAmb>
                    <chNFe>35260600000000000100550010000000011000000006</chNFe>
                    <dhRecbto>2026-06-18T10:00:00-03:00</dhRecbto>
                    <nProt>123456789012345</nProt>
                    <cStat>100</cStat>
                    <xMotivo>Autorizado o uso da NF-e</xMotivo>
                  </infProt>
                </protNFe>
              </retConsSitNFe>
            </nfeResultMsg>
          </soap:Body>
        </soap:Envelope>
        """;

    public const string RespostaCancelada = """
        <retConsSitNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
          <cStat>101</cStat>
          <xMotivo>Cancelamento de NF-e homologado</xMotivo>
          <procEventoNFe>
            <retEvento>
              <infEvento>
                <cStat>135</cStat>
                <xEvento>Cancelamento registrado</xEvento>
                <nProt>135000000000000</nProt>
                <dhRegEvento>2026-06-18T11:00:00-03:00</dhRegEvento>
              </infEvento>
            </retEvento>
          </procEventoNFe>
        </retConsSitNFe>
        """;

    public const string RespostaNaoEncontrada = """
        <retConsSitNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
          <cStat>217</cStat>
          <xMotivo>NF-e nao consta na base de dados da SEFAZ</xMotivo>
        </retConsSitNFe>
        """;

    public const string RespostaStatusOnline = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>
            <nfeResultMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4">
              <retConsStatServ xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
                <tpAmb>2</tpAmb>
                <verAplic>TEST</verAplic>
                <cStat>107</cStat>
                <xMotivo>Servico em Operacao</xMotivo>
                <cUF>35</cUF>
                <dhRecbto>2026-06-18T10:00:00-03:00</dhRecbto>
              </retConsStatServ>
            </nfeResultMsg>
          </soap:Body>
        </soap:Envelope>
        """;

    public const string RespostaStatusProcessamento = """
        <retConsStatServ xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
          <tpAmb>2</tpAmb>
          <verAplic>TEST</verAplic>
          <cStat>108</cStat>
          <xMotivo>Servico Paralisado Momentaneamente (curto prazo)</xMotivo>
          <cUF>35</cUF>
        </retConsStatServ>
        """;
}

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(send(request));
}

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Esperado '{expected}', recebido '{actual}'.");
    }

    public static void Contains(string expectedSubstring, string? actual)
    {
        if (actual is null || !actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new InvalidOperationException($"Esperado texto contendo '{expectedSubstring}', recebido '{actual}'.");
    }

    public static void Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Esperada excecao {typeof(TException).Name}, recebida {ex.GetType().Name}.");
        }

        throw new InvalidOperationException($"Esperada excecao {typeof(TException).Name}.");
    }
}

internal sealed class SkipTestException(string message) : Exception(message);
