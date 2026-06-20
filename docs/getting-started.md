# Guia Inicial

`NFEConsulta` valida XML/chave de NF-e e consulta a SEFAZ com contratos tipados.

## Instalacao

```bash
dotnet add package NFEConsulta
```

## Exemplo Basico

```csharp
using System.Security.Cryptography.X509Certificates;
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;
using NFEConsulta.Extensions;

using X509Certificate2 certificado = CertificadoProvider.ObterPorPem(
    "certs/cert.pem",
    "certs/key.pem");

NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    Uf = UfNFe.SP,
    CorrelationId = "job-001"
};

using NFeConsultaClient client = NFeConsultaClient.CriarComCertificado(certificado, options);

ConsultaNFeResult result = await client.ConsultarChaveAsync("35260600000000000100550010000000011000000006");
```

## Status Oficial

```csharp
using NFeStatusClient statusClient = NFeStatusClient.CriarComCertificado(certificado, options);
SefazStatusResult status = await statusClient.ConsultarStatusAsync();
```

## Certificado

- PEM: recomendado para Linux/container.
- PFX: bom para arquivos prontos.
- Windows Store: bom para producao Windows.
