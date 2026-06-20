# Integracao Interna

## API REST interna

Executar localmente:

```bash
dotnet run --project src/NFEConsulta.Api --framework net10.0
```

Endpoints principais:

- `GET /health`
- `GET /sefaz/status`
- `POST /consultar`

## Configuracao

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

Os schemas ficam em `schemas/v4` e sao empacotados junto com a biblioteca.

## Build e pacote

```bash
dotnet build NFEConsulta.sln
dotnet run --project tests/NFEConsulta.Tests/NFEConsulta.Tests.csproj
dotnet pack src/NFEConsulta.Core/NFEConsulta.Core.csproj -c Release -o artifacts/packages
```

## Seguranca

- Nao versione senha de certificado.
- Nao use `IgnoreServerCertificateErrors` em producao.
- Prefira logs com cuidado para `ErroDetalhado`.
