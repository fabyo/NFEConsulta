![Logo](https://raw.githubusercontent.com/fabyo/NFEConsulta/master/logo-200.png)

[![NuGet](https://img.shields.io/nuget/v/NFEConsulta.svg)](https://www.nuget.org/packages/NFEConsulta)
[![Downloads](https://img.shields.io/nuget/dt/NFEConsulta.svg)](https://www.nuget.org/packages/NFEConsulta)
[![Build](https://github.com/fabyo/NFEConsulta/actions/workflows/ci.yml/badge.svg)](https://github.com/fabyo/NFEConsulta/actions/workflows/ci.yml)
[![Publish](https://github.com/fabyo/NFEConsulta/actions/workflows/publish.yml/badge.svg)](https://github.com/fabyo/NFEConsulta/actions/workflows/publish.yml)
[![CodeQL](https://github.com/fabyo/NFEConsulta/actions/workflows/codeql.yml/badge.svg)](https://github.com/fabyo/NFEConsulta/actions/workflows/codeql.yml)
[![Scorecard](https://github.com/fabyo/NFEConsulta/actions/workflows/scorecard.yml/badge.svg?branch=master)](https://github.com/fabyo/NFEConsulta/actions/workflows/scorecard.yml)
[![Release Drafter](https://github.com/fabyo/NFEConsulta/actions/workflows/release-drafter.yml/badge.svg)](https://github.com/fabyo/NFEConsulta/actions/workflows/release-drafter.yml)
[![GitHub stars](https://img.shields.io/github/stars/fabyo/NFEConsulta)](https://github.com/fabyo/NFEConsulta)
[![License](https://img.shields.io/github/license/fabyo/NFEConsulta)](https://github.com/fabyo/NFEConsulta)

`NFEConsulta` é uma biblioteca .NET para consultar NF-e.

## Diferenciais

- Status oficial da SEFAZ via `NFeStatusServico4`, em vez de ping ou teste de rede.
- Resolucao conservadora de endpoints por UF, com SP como default e override explicito quando necessario.
- `CorrelationId` opcional para rastrear jobs, lotes e logs.
- Validacao de XML contra XSD oficial incluso no pacote.
- Contratos tipados para separar retorno fiscal de falha tecnica.
- Suporte para biblioteca, CLI e API REST interna.

## Documentacao

- [Pagina inicial da documentacao](docs/README.md)
- [Guia inicial](docs/getting-started.md)
- [Uso da biblioteca](docs/uso.md)
- [Referencia da API](docs/api-reference.md)
- [Solucao de problemas](docs/troubleshooting.md)
- [Integracao interna](docs/integracao-interna.md)
- [Contribuindo](CONTRIBUTING.md)
- [Historico de alteracoes](CHANGELOG.md)

## Instalacao

```bash
dotnet add package NFEConsulta
```

## Exemplo rapido

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
    CorrelationId = "job-20260619-001"
};

using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(certificado, options);
ConsultaNFeResult resultado = await client.ConsultarChaveAsync("99999999999999999999999999999999999999999999");
```

## Extraindo dados do Certificado

### Biblioteca C#
```csharp
using NFEConsulta.Infrastructure;

CertificadoInfo info = certificado.ExtrairInfo();
Console.WriteLine($"CNPJ: {info.Cnpj}");
Console.WriteLine($"Vencimento: {info.DataExpiracao}");
```

### CLI
```bash
nfeconsulta certificado --cert-pem cert.pem --key-pem key.pem
# Ou com saida em JSON:
nfeconsulta certificado --cert-pem cert.pem --key-pem key.pem --json
```

## Consultando CNPJ / Cadastro de Contribuinte na SEFAZ

Você pode consultar a situação fiscal e cadastral de um CNPJ, CPF ou Inscrição Estadual (IE) diretamente junto aos webservices estaduais da SEFAZ usando o método `NFeCadastroClient`.

```csharp
using NFEConsulta.Models;
using NFEConsulta.Services;

NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    Uf = UfNFe.SP // Define a UF da consulta
};

using NFeCadastroClient client = NFeCadastroClient.CriarComCertificado(certificado, options);
CadastroConsultaResult resultado = await client.ConsultarCadastroAsync("12345678901234"); // CNPJ, CPF ou IE

if (resultado.Sucesso)
{
    foreach (var cad in resultado.Contribuintes)
    {
        Console.WriteLine($"Nome: {cad.Nome}");
        Console.WriteLine($"IE: {cad.IE}");
        Console.WriteLine($"Situação: {cad.Situacao}"); // Ex: "Habilitado"
        Console.WriteLine($"Endereço: {cad.Endereco?.Logradouro}, {cad.Endereco?.Numero} - {cad.Endereco?.NomeMunicipio}/{cad.UF}");
    }
}
```

## O que esta incluso

- Validacao de chave de acesso de 44 digitos.
- Extracao de chave a partir de XML de NF-e.
- Validacao XML contra XSD oficial.
- Consulta de NF-e por chave.
- Consulta do status oficial da SEFAZ.
- Consulta de cadastro de contribuinte (CNPJ, CPF ou IE) via SEFAZ (`CadConsultaCadastro4`).
- Extração de dados e metadados de certificados digitais (CNPJ, nome, datas).
- Suporte para biblioteca, CLI e API interna.

## Projetos

- `src/NFEConsulta.Core`: biblioteca principal publicada no NuGet como `NFEConsulta`.
- `src/NFEConsulta.Cli`: CLI publicada como `NFEConsulta.Cli`.
- `src/NFEConsulta.Api`: API REST interna.

## Pacote e versao

A versao publica atual e `0.4.2`.

## 🔗 Projetos relacionados

| Projeto | Descrição |
|---|---|
| [NFeSchemaDownloader](https://github.com/fabyo/NFeSchemaDownloader) | Mantém os Schemas XML (XSD) da SEFAZ sempre atualizados automaticamente |
| [NFEDanfe](https://github.com/fabyo/NFEDanfe) | Gera DANFE em PDF a partir de XML NF-e autorizado |

### Ferramentas CLI

- **NFEDanfe.Cli** → Geração de DANFE pela linha de comando.
- **NFeSchemaDownloader.Cli** → Automação de download de Schemas.

### Fluxo recomendado

```text
NFeSchemaDownloader (Mantém XSDs atualizados)
   │
   ▼
NF-e XML
   │
   ▼
NFEConsulta (Valida XML via XSD e consulta SEFAZ)
   │
   ▼
NFEDanfe (Gera o PDF final)
```

## Autor

Fabyo Guimaraes Oliveira

- LinkedIn: [https://www.linkedin.com/in/fabyo-guimaraes/](https://www.linkedin.com/in/fabyo-guimaraes/)
- GitHub: https://github.com/fabyo

## Licenca

MIT.
