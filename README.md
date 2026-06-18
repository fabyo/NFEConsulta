# NFEConsulta

`NFEConsulta` e uma biblioteca .NET para trabalhar com NF-e:

- Validar chave de acesso de 44 digitos.
- Extrair a chave de acesso de um XML de NF-e.
- Validar XML contra os schemas XSD oficiais incluídos no pacote.
- Consultar a SEFAZ pela chave para obter o status atual da NF-e.
- Usar como biblioteca C#, CLI ou API REST interna.

Ponto importante: o XML pode estar estruturalmente valido e ainda assim a NF-e pode ter sido cancelada depois. Por isso o fluxo correto e:

```text
XML -> valida XSD -> extrai chave -> consulta SEFAZ -> retorna status atual
```

## Pacotes

Biblioteca:

```bash
dotnet add package NFEConsulta --version 0.1.6
```

CLI como dotnet tool:

```bash
dotnet tool install --global NFEConsulta.Cli --version 0.1.6
```

Depois:

```bash
nfeconsulta --help
```

## Projetos

```text
src/NFEConsulta.Core
  Biblioteca principal. Vai para o NuGet como NFEConsulta.

src/NFEConsulta.Cli
  CLI. Vai para o NuGet como dotnet tool NFEConsulta.Cli.

src/NFEConsulta.Api
  API REST interna. Nao vai para o NuGet; deve ser publicada/deployada como servico.
```

## Schemas XSD Incluidos

O pacote `NFEConsulta` inclui os schemas em:

```text
schemas/v4
```

Eles sao empacotados no NuGet como `contentFiles`, entao quem instala a biblioteca recebe os XSDs junto.

Os schemas foram incorporados a partir deste projeto:

https://github.com/fabyo/sefaz-scraper

Use esse link se quiser conhecer, auditar ou atualizar a origem dos XSDs.

## Conceitos

### Validar XML com XSD

Valida se o XML respeita o layout da NF-e.

Isso responde:

```text
Este XML tem estrutura valida?
```

Nao responde:

```text
Esta NF-e ainda esta autorizada hoje?
```

### Consultar SEFAZ

Consulta a SEFAZ pela chave de acesso.

Isso responde:

```text
Qual e o status atual dessa NF-e?
```

Exemplos de retorno:

- `Autorizada`
- `Cancelada`
- `Denegada`
- `NaoEncontrada`
- `Desconhecido`

## Uso Como Biblioteca C#

### Instalar

```bash
dotnet add package NFEConsulta --version 0.1.6
```

### Consultar Pela Chave

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");

using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(
    certificado,
    TipoAmbiente.Producao,
    "https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx");

ConsultaNFeResult resultado = await client.ConsultarChaveAsync(
    "35260632409620000175550010000047711996933150");

Console.WriteLine(resultado.Status);
Console.WriteLine(resultado.CodigoStatus);
Console.WriteLine(resultado.Motivo);
Console.WriteLine(resultado.NumeroProtocolo);
Console.WriteLine(resultado.DataAutorizacao);
```

### Consultar Pelo XML

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");

using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(
    certificado,
    TipoAmbiente.Producao,
    "https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx");

ConsultaNFeResult resultado = await client.ConsultarXmlFileAsync(
    "xml/nota.xml",
    xsdDirectory: "schemas/v4");

Console.WriteLine(resultado.Status);
```

Nesse exemplo:

- O XML e validado contra XSD.
- A chave e extraida do XML.
- A chave e validada.
- A SEFAZ e consultada.
- O status atual e retornado.

### Validar Apenas A Chave

```csharp
using NFEConsulta.Infrastructure;

ChaveAcessoValidationResult result = ChaveAcessoNFe.Validate(
    "35260632409620000175550010000047711996933150");

if (!result.IsValid)
    Console.WriteLine(result.ErrorMessage);
```

### Extrair Chave Do XML

```csharp
using NFEConsulta.Infrastructure;

string chave = await ChaveAcessoNFe.ExtractFromXmlFileAsync("xml/nota.xml");
```

### Validar Apenas O XML Contra XSD

```csharp
using NFEConsulta.Infrastructure;

NFeXmlValidationResult validation = await NFeXmlValidator.ValidateXmlFileAsync(
    "xml/nota.xml",
    "schemas/v4");

if (!validation.IsValid)
{
    foreach (string error in validation.Errors)
        Console.WriteLine(error);
}
```

## Certificado Digital

A SEFAZ exige certificado digital para consulta. A biblioteca aceita `X509Certificate2`, entao voce pode carregar o certificado de varias formas.

### Opcao 1: PEM

Boa opcao para Linux, containers e ambientes onde voce tem `cert.pem` e `key.pem`.

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");
```

### Opcao 2: PFX

Boa opcao quando voce recebeu um arquivo `.pfx`.

Nao coloque a senha no codigo. Use variavel de ambiente:

```bash
set NFE_CERT_PASSWORD=minha-senha
```

No codigo:

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPfx(
    "certificado.pfx",
    Environment.GetEnvironmentVariable("NFE_CERT_PASSWORD"));
```

### Opcao 3: Windows Store Por Thumbprint

Recomendado para producao em Windows.

Instale o certificado no store:

```text
CurrentUser\My
```

Depois use o thumbprint:

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;

using X509Certificate2 certificado = CertificadoProvider.ObterPorThumbprint(
    "ABCDEF123456...");
```

### Opcao 4: Windows Store Por Subject

Util para desenvolvimento, mas menos preciso que thumbprint:

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;

using X509Certificate2 certificado = CertificadoProvider.ObterPorSubject(
    "NOME DA EMPRESA");
```

### Recomendacao

```text
Windows producao     -> Thumbprint
Linux/container      -> PEM ou PFX
Desenvolvimento      -> PEM, PFX ou Subject
```

## Ambientes E URLs

Producao SP:

```text
https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx
```

Homologacao SP:

```text
https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx
```

Exemplo:

```csharp
TipoAmbiente.Producao
```

ou:

```csharp
TipoAmbiente.Homologacao
```

## Resultado Da Consulta

`ConsultaNFeResult` retorna:

```csharp
public bool Sucesso { get; init; }
public string CodigoStatus { get; init; }
public string Motivo { get; init; }
public StatusNFe Status { get; init; }
public string? NumeroProtocolo { get; init; }
public DateTimeOffset? DataAutorizacao { get; init; }
public string? ErroDetalhado { get; init; }
```

Exemplo de NF-e cancelada:

```text
Status: Cancelada
Codigo: 101
Motivo: Cancelamento registrado
Protocolo: 135262397537122
```

## Uso Do CLI

Instalar:

```bash
dotnet tool install --global NFEConsulta.Cli --version 0.1.6
```

Consultar pela chave:

```bash
nfeconsulta --chave 35260632409620000175550010000047711996933150 --cert-thumbprint <thumbprint>
```

Consultar por XML com validacao XSD:

```bash
nfeconsulta --xml-file xml/nota.xml --xsd-dir schemas/v4 --cert-pem certs/cert.pem --key-pem certs/key.pem --ambiente producao --url https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx
```

Ler XML via stdin:

```bash
type xml/nota.xml | nfeconsulta --xml-stdin --xsd-dir schemas/v4 --cert-pem certs/cert.pem --key-pem certs/key.pem
```

Parametros de certificado no CLI:

```text
--cert-thumbprint <valor>
--cert-serial <valor>
--cert-subject <texto>
--cert-pfx <path>
--cert-pem <path> --key-pem <path>
```

Para PFX, use:

```bash
set NFE_CERT_PASSWORD=minha-senha
```

## Uso Da API REST Interna

A API e indicada quando outro backend, por exemplo Rust, precisa consultar a SEFAZ por HTTP.

Fluxo:

```text
Rust backend -> HTTP interno -> NFEConsulta.Api -> SEFAZ
```

Executar localmente:

```bash
dotnet run --project src/NFEConsulta.Api --framework net10.0
```

Health check:

```http
GET /health
```

Consultar por chave:

```http
POST /consultar
Content-Type: application/json

{
  "chaveAcesso": "35260632409620000175550010000047711996933150"
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
    "UrlWebService": "https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
    "TimeoutSeconds": 60,
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

## Build

```bash
dotnet build NFEConsulta.sln
```

## Gerar Pacote NuGet

Biblioteca:

```bash
dotnet pack src/NFEConsulta.Core/NFEConsulta.Core.csproj -c Release -o artifacts/packages
```

CLI:

```bash
dotnet pack src/NFEConsulta.Cli/NFEConsulta.Cli.csproj -c Release -o artifacts/packages
```

Saida esperada:

```text
artifacts/packages/NFEConsulta.0.1.6.nupkg
artifacts/packages/NFEConsulta.Cli.0.1.6.nupkg
```

## Observacoes De Seguranca

- Nao versionar senha de certificado.
- Preferir thumbprint em Windows producao.
- Preferir secret/variavel de ambiente para senha de PFX.
- Nao usar `IgnoreServerCertificateErrors` em producao.
- Validar XML por XSD antes de consultar a SEFAZ quando o XML for fornecido pelo usuario.
