# Uso da Biblioteca

## Status oficial da SEFAZ

A biblioteca consulta o webservice `NFeStatusServico4` para saber se a SEFAZ esta operacional na UF e ambiente configurados.

```csharp
using NFeStatusClient statusClient = NFeStatusClient.CriarComCertificado(
    certificado,
    new NFeConsultaOptions
    {
        Ambiente = TipoAmbiente.Homologacao,
        Uf = UfNFe.SP
    });

SefazStatusResult status = await statusClient.ConsultarStatusAsync();
```

## Chave, XML e UF

```csharp
NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    CorrelationId = "job-20260619-001",
    Timeout = TimeSpan.FromSeconds(30),
    RetryCount = 2
};
```

Sem `Uf`, a consulta infere o estado pelos dois primeiros digitos da chave.
Para forcar outro endpoint ou para operacoes sem chave, informe a UF:

```csharp
NFeConsultaOptions options = new()
{
    Ambiente = TipoAmbiente.Producao,
    Uf = UfNFe.MG
};
```

Se necessario, use override explicito de URL.

## Resultado da consulta

`ConsultaNFeResult` separa retorno fiscal de erro tecnico.

Helpers uteis:

- `DeveRetentarComBackoff`
- `RequerCorrecaoDeEntrada`
- `RequerAlertaOperacional`

`SefazStatusResult` oferece:

- `ServicoEmOperacao`
- `ServicoEmProcessamento`
- `ServicoParalisado`
- `DeveReagendarProcessamento`
- `RequerAlertaOperacional`

## Workers e filas

Politica recomendada:

- `FalhaXml` e `RequisicaoInvalida`: finalizar a mensagem e persistir o erro.
- `FalhaRede`, `Timeout` e falhas tecnicas: retentar com backoff.
- `FalhaCertificado`: alertar operacao.
- `108` e `109`: reagendar.

## Certificado digital

Use `CertificadoProvider` para carregar PEM, PFX ou thumbprint.

Recomendacao:

- Windows producao: thumbprint.
- Linux/container: PEM ou PFX.
- Desenvolvimento: qualquer formato suportado.

## CLI

```bash
nfeconsulta status --uf SP --ambiente homologacao --cert-pem certs/cert.pem --key-pem certs/key.pem
```

```bash
nfeconsulta --xml-file xml/nota.xml --xsd-dir schemas/v4 --cert-pem certs/cert.pem --key-pem certs/key.pem --ambiente producao --uf MG
```
