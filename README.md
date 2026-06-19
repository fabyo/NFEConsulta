![Logo](https://raw.githubusercontent.com/fabyo/NFEConsulta/master/logo-200.png)

[![NuGet](https://img.shields.io/nuget/v/NFEConsulta.svg)](https://www.nuget.org/packages/NFEConsulta)
[![Downloads](https://img.shields.io/nuget/dt/NFEConsulta.svg)](https://www.nuget.org/packages/NFEConsulta)
[![GitHub stars](https://img.shields.io/github/stars/fabyo/NFEConsulta)](https://github.com/fabyo/NFEConsulta)
[![License](https://img.shields.io/github/license/fabyo/NFEConsulta)](https://github.com/fabyo/NFEConsulta)

`NFEConsulta` e uma biblioteca .NET para consultar NF-e com uma API pequena, tipada e pronta para uso em servicos.

- Valida chave de acesso de 44 digitos.
- Extrai chave de XML de NF-e.
- Valida XML contra schemas XSD oficiais incluidos no pacote.
- Consulta a SEFAZ pela chave para obter o status atual da NF-e.
- Consulta a disponibilidade oficial da SEFAZ via `NFeStatusServico4`.
- Resolve endpoint por UF + ambiente, com SP como default e override opcional.
- Propaga `CorrelationId` opcional em resultados e logs para rastrear jobs/lotes.
- Explicita falhas tecnicas, falhas de XML e respostas fiscais negativas.
- Disponibiliza biblioteca C#, CLI e API REST interna.

## Diferencial: Status Oficial Da SEFAZ

A biblioteca consegue consultar o status oficial da SEFAZ antes de processar XMLs ou lotes. Isso usa o webservice `NFeStatusServico4`, que retorna `cStat` e `xMotivo`, por exemplo `107 - Servico em Operacao`.

Isso e diferente de ping, teste TCP ou handshake TLS. Ping/TLS so dizem que existe conectividade; `NFeStatusServico4` diz se o servico fiscal esta operacional para a UF e ambiente configurados.

Exemplo:

```csharp
using NFeStatusClient statusClient = NFeStatusClient.CriarComCertificado(
    certificado,
    new NFeConsultaOptions
    {
        Ambiente = TipoAmbiente.Homologacao,
        Uf = UfNFe.SP
    });

SefazStatusResult status = await statusClient.ConsultarStatusAsync();

if (!status.Online)
    Console.WriteLine($"{status.CodigoStatus} - {status.Motivo}");
```

CLI:

```bash
nfeconsulta status --uf SP --ambiente homologacao --cert-pem certs/cert.pem --key-pem certs/key.pem
```

Fluxo recomendado:

```text
XML -> valida XSD -> extrai chave -> valida chave -> consulta SEFAZ -> retorna status atual
```

## Pacotes

Biblioteca:

```bash
dotnet add package NFEConsulta
```

CLI como dotnet tool:

```bash
dotnet tool install --global NFEConsulta.Cli
```

## Projetos

```text
src/NFEConsulta.Core
  Biblioteca principal. Vai para o NuGet como NFEConsulta.

src/NFEConsulta.Cli
  CLI. Vai para o NuGet como dotnet tool NFEConsulta.Cli.

src/NFEConsulta.Api
  API REST interna. Nao vai para o NuGet; deve ser publicada como servico.
```

## Uso Como Biblioteca

### Consulta Por Chave

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");

NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    Uf = UfNFe.SP,
    CorrelationId = "job-20260619-001",
    Timeout = TimeSpan.FromSeconds(30),
    RetryCount = 2
};

using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(
    certificado,
    options);

ConsultaNFeResult resultado = await client.ConsultarChaveAsync(
    "99999999999999999999999999999999999999999999");

Console.WriteLine(resultado.Status);
Console.WriteLine(resultado.TipoResultado);
Console.WriteLine(resultado.CorrelationId);
Console.WriteLine(resultado.CodigoStatus);
Console.WriteLine(resultado.Motivo);
Console.WriteLine(resultado.NumeroProtocolo);
```

### Consulta Por XML

```csharp
ConsultaNFeResult resultado = await client.ConsultarXmlFileAsync(
    "xml/nota.xml",
    xsdDirectory: "schemas/v4");
```

Nesse caminho, a biblioteca valida o XML quando `xsdDirectory` e informado, extrai a chave, valida o digito verificador e consulta a SEFAZ.

### Validar Apenas A Chave

```csharp
ChaveAcessoValidationResult result = ChaveAcessoNFe.Validate(
    "99999999999999999999999999999999999999999999");

if (!result.IsValid)
    Console.WriteLine(result.ErrorMessage);
```

### Validar Apenas O XML

```csharp
NFeXmlValidationResult validation = await NFeXmlValidator.ValidateXmlFileAsync(
    "xml/nota.xml",
    "schemas/v4");

foreach (string error in validation.Errors)
    Console.WriteLine(error);
```

## UF, Ambiente E Endpoint

O uso comum nao precisa informar URL. Configure `Ambiente` e `Uf`:

```csharp
NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Homologacao,
    Uf = UfNFe.SP
};
```

SP e o default nos projetos CLI e API. Na biblioteca, se `Uf` ficar nula, a UF pode ser inferida pelos dois primeiros digitos da chave de acesso.

Para outro estado:

```csharp
NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    Uf = UfNFe.MG
};
```

Para casos especiais, use override explicito:

```csharp
NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Homologacao,
    Uf = UfNFe.SP,
    CorrelationId = "lote-123",
    UrlStatusWebServiceOverride = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
    UrlWebServiceOverride = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx"
};
```

O catalogo de endpoints e conservador: a biblioteca so resolve automaticamente UFs com endpoint conhecido no codigo. UFs sem endpoint confirmado exigem override explicito para evitar chamada fiscal no autorizador errado.

### UFs Com Endpoint Automatico

Endpoints mapeados no resolver atual para `NFeConsultaProtocolo4` e `NFeStatusServico4`:

```text
AC, AL, AP, DF, ES, PB, PI, RJ, RN, RO, RR, RS, SC, SE, TO -> SVRS
SP -> SEFAZ-SP
MG -> SEFAZ-MG
PR -> SEFAZ-PR
```

Validacao local realizada nesta revisao:

```text
SP homologacao -> status oficial SEFAZ + consulta por XML real com certificado local
```

UFs presentes na tabela, mas sem endpoint confirmado no codigo:

```text
AM, BA, CE, GO, MA, MT, MS, PA, PE
```

Para essas UFs, informe `UrlWebServiceOverride`. A biblioteca falha de forma explicita em vez de usar fallback silencioso.

## Resultado Da Consulta

`ConsultaNFeResult` separa retorno fiscal de erro tecnico:

```csharp
public bool Sucesso { get; init; }
public string CodigoStatus { get; init; }
public string Motivo { get; init; }
public StatusNFe Status { get; init; }
public TipoResultadoConsulta TipoResultado { get; init; }
public bool RespostaSefazRecebida { get; init; }
public string? CorrelationId { get; init; }
public string? NumeroProtocolo { get; init; }
public DateTimeOffset? DataAutorizacao { get; init; }
public string? ErroDetalhado { get; init; }
```

Tipos principais:

- `ConsultaOk`: resposta SEFAZ reconhecida, como autorizada, cancelada ou denegada.
- `RespostaSefazNegativa`: a SEFAZ respondeu, mas a nota nao foi encontrada ou a consulta nao resultou em autorizacao.
- `FalhaRede`, `Timeout`, `FalhaCertificado`, `FalhaXml`, `RequisicaoInvalida`: falha tecnica ou de entrada.

`SefazStatusResult` retorna:

```csharp
public bool Online { get; init; }
public string CodigoStatus { get; init; }
public string Motivo { get; init; }
public UfNFe Uf { get; init; }
public TipoAmbiente Ambiente { get; init; }
public string UrlWebService { get; init; }
public TimeSpan Duracao { get; init; }
public TipoResultadoConsulta TipoResultado { get; init; }
public bool RespostaSefazRecebida { get; init; }
public string? CorrelationId { get; init; }
public string? ErroDetalhado { get; init; }
```

## Certificado Digital

A SEFAZ exige certificado digital. A biblioteca trabalha com `X509Certificate2` e oferece helpers.

PEM:

```csharp
using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");
```

PFX:

```bash
set NFE_CERT_PASSWORD=minha-senha
```

```csharp
using X509Certificate2 certificado = CertificadoProvider.ObterPorPfx(
    "certificado.pfx",
    Environment.GetEnvironmentVariable("NFE_CERT_PASSWORD"));
```

Windows Store:

```csharp
using X509Certificate2 certificado = CertificadoProvider.ObterPorThumbprint(
    "ABCDEF123456...");
```

Recomendacao:

```text
Windows producao     -> Thumbprint
Linux/container      -> PEM ou PFX
Desenvolvimento      -> PEM, PFX ou Subject
```

## CLI

Consultar por chave usando SP homologacao:

```bash
nfeconsulta --chave 99999999999999999999999999999999999999999999 --cert-thumbprint <thumbprint>
```

Consultar status oficial da SEFAZ:

```bash
nfeconsulta status --uf SP --ambiente homologacao --cert-thumbprint <thumbprint>
```

Consultar por XML em outro estado:

```bash
nfeconsulta --xml-file xml/nota.xml --xsd-dir schemas/v4 --cert-pem certs/cert.pem --key-pem certs/key.pem --ambiente producao --uf MG
```

Override de URL, apenas quando necessario:

```bash
nfeconsulta --chave 99999999999999999999999999999999999999999999 --cert-thumbprint <thumbprint> --url https://endpoint-customizado
```

Parametros principais:

```text
--chave <valor>
--xml-file <path>
--xml-stdin
--cert-thumbprint <valor>
--cert-serial <valor>
--cert-subject <texto>
--cert-pfx <path>
--cert-pem <path> --key-pem <path>
--xsd-dir <path>
--ambiente <homologacao|producao>
--uf <sigla>
--url <url>
--status-url <url>
--correlation-id <valor>
--timeout-seconds <n>
--retry-count <n>
--verbose
```

## API REST Interna

Executar localmente:

```bash
dotnet run --project src/NFEConsulta.Api --framework net10.0
```

Health check:

```http
GET /health
```

Status oficial da SEFAZ:

```http
GET /sefaz/status
```

Consultar por chave:

```http
POST /consultar
Content-Type: application/json

{
  "chaveAcesso": "99999999999999999999999999999999999999999999"
}
```

Consultar por XML:

```http
POST /consultar
Content-Type: application/json

{
  "xml": "<nfeProc>...</nfeProc>"
}
```

Configuracao:

```json
{
  "Sefaz": {
    "Ambiente": "producao",
    "UF": "SP",
    "UrlWebService": "",
    "UrlStatusWebService": "",
    "CorrelationId": "",
    "TimeoutSeconds": 60,
    "RetryCount": 2,
    "CertThumbprint": "",
    "CertSerial": "",
    "CertSubject": "",
    "CertPfxPath": "",
    "CertPemPath": "certs/cert.pem",
    "KeyPemPath": "certs/key.pem",
    "XsdDirectory": "schemas/v4",
    "IgnoreServerCertificateErrors": false
  }
}
```

## Schemas XSD

O pacote inclui os schemas em `schemas/v4` e os empacota como `contentFiles`.

Origem dos schemas:

https://github.com/fabyo/sefaz-scraper

## Build E Pacote

Build:

```bash
dotnet build NFEConsulta.sln
```

Testes automatizados sem certificado real:

```bash
dotnet run --project tests/NFEConsulta.Tests/NFEConsulta.Tests.csproj
```

Teste de integracao real opcional, usando arquivos locais ignorados pelo Git:

```powershell
$env:NFE_RUN_INTEGRATION="1"
$env:NFE_CERT_PEM="certs/cert.pem"
$env:NFE_KEY_PEM="certs/key.pem"
$env:NFE_XML_PATH="xml"
$env:NFE_UF="SP"
$env:NFE_AMBIENTE="homologacao"
dotnet run --project tests/NFEConsulta.Tests/NFEConsulta.Tests.csproj
```

`NFE_XML_PATH` pode apontar para um arquivo XML especifico ou para uma pasta com varios XMLs.
`certs/` e `xml/` ficam no `.gitignore`. Nao suba certificado, chave privada, senha ou XML real.

Pacote da biblioteca:

```bash
dotnet pack src/NFEConsulta.Core/NFEConsulta.Core.csproj -c Release -o artifacts/packages
```

A versao publica atual do pacote e `0.2.0`, porque a API passou a expor options tipadas, status oficial SEFAZ, endpoints por UF e resultado com `CorrelationId`.

Pacote do CLI:

```bash
dotnet pack src/NFEConsulta.Cli/NFEConsulta.Cli.csproj -c Release -o artifacts/packages
```

## Seguranca

- Nao versione senha de certificado.
- Prefira thumbprint em Windows producao.
- Use secret ou variavel de ambiente para senha de PFX.
- Nao use `IgnoreServerCertificateErrors` em producao.
- Logue `ErroDetalhado` com cuidado, principalmente em APIs expostas.
- Valide XML por XSD antes de consultar a SEFAZ quando o XML vier do usuario.

## Autor

Fabyo Guimaraes Oliveira

- LinkedIn: [https://www.linkedin.com/in/fabyo-guimaraes/](https://www.linkedin.com/in/fabyo-guimaraes/)
- GitHub: https://github.com/fabyo

## Licenca

MIT.
