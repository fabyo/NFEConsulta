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
    "UF": "",
    "UrlWebService": "",
    "UrlStatusWebService": "",
    "CorrelationId": "",
    "TimeoutSeconds": 60,
    "RetryCount": 2,
    "RateLimitPermitLimit": 30,
    "RateLimitWindowSeconds": 60,
    "MaxRequestBodyBytes": 5242880,
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

Os schemas ficam em `schemas/v4`. O pacote NuGet inclui somente os seis arquivos
necessarios para validar `NFe` e `nfeProc` na versao 4.00 e bloqueia imports XSD
fora desse diretorio.

## Build e pacote

```bash
dotnet build NFEConsulta.sln
dotnet test NFEConsulta.sln --collect:"XPlat Code Coverage"
dotnet pack src/NFEConsulta.Core/NFEConsulta.Core.csproj -c Release -o artifacts/packages
```

## Seguranca

- Nao versione senha de certificado.
- Nao use `IgnoreServerCertificateErrors` em producao.
- Prefira logs com cuidado para `ErroDetalhado`.
- A API aplica rate limit por `RemoteIpAddress` apenas nos endpoints SEFAZ; `/health`
  permanece livre. Se houver proxy reverso, configure `ForwardedHeaders` no sistema
  hospedeiro somente com proxies conhecidos antes de usar o IP encaminhado.
- Autenticacao e autorizacao pertencem ao sistema hospedeiro que incorpora esta API.
- O corpo HTTP e os streams XML sao limitados a 5 MiB por padrao.
