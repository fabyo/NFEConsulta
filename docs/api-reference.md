# API Reference

## Namespaces

```csharp
using NFEConsulta.Infrastructure;
using NFEConsulta.Models;
using NFEConsulta.Services;
using NFEConsulta.Extensions;
```

## Principais Tipos

- `NFeConsultaOptions`
- `ConsultaNFeResult`
- `SefazStatusResult`
- `NFeConsultaClient`
- `NFeStatusClient`
- `ChaveAcessoNFe`
- `NFeXmlValidator`

## Dependency Injection

- `AddSefazNFeService`
- `AddSefazStatusService`

## Policies

- `ConsultaNFeResult.DeveRetentarComBackoff`
- `ConsultaNFeResult.RequerCorrecaoDeEntrada`
- `ConsultaNFeResult.RequerAlertaOperacional`
- `SefazStatusResult.ServicoEmOperacao`
- `SefazStatusResult.ServicoEmProcessamento`
- `SefazStatusResult.ServicoParalisado`
- `SefazStatusResult.DeveReagendarProcessamento`

## Version

Pacote atual: `0.2.3`.
