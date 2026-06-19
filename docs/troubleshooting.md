# Troubleshooting

## `NFeXmlValidator` nao compila

Use o namespace:

```csharp
using NFEConsulta.Infrastructure;
```

## XML invalido

`FalhaXml` indica que o problema e do arquivo, nao da SEFAZ.

## Timeout ou falha de rede

Use retry/backoff no worker. A biblioteca ja faz retry curto, mas falhas persistentes devem ser tratadas pela fila.

## Status 108/109

Esses retornos significam indisponibilidade operacional da SEFAZ. Reagende o processamento.

## UF sem endpoint confirmado

Informe `UrlWebServiceOverride` ou `UrlStatusWebServiceOverride`.
