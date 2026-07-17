# Referencia da API

## Espacos de nomes

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

## Injecao de dependencia

- `AddSefazNFeService`
- `AddSefazStatusService`
- `AddSefazCadastroService`

## Politicas

- `ConsultaNFeResult.DeveRetentarComBackoff`
- `ConsultaNFeResult.RequerCorrecaoDeEntrada`
- `ConsultaNFeResult.RequerAlertaOperacional`
- `SefazStatusResult.ServicoEmOperacao`
- `SefazStatusResult.ServicoEmProcessamento`
- `SefazStatusResult.ServicoParalisado`
- `SefazStatusResult.DeveReagendarProcessamento`

## Versao

Pacote atual: `0.4.2`.
