# Contribuindo

Contribuicoes sao bem-vindas quando mantem o projeto pequeno, previsivel e seguro para uso fiscal.

## Antes de abrir um PR

1. Rode `dotnet format NFEConsulta.sln --verify-no-changes`.
2. Rode `dotnet build NFEConsulta.sln`.
3. Rode `dotnet run --project tests/NFEConsulta.Tests/NFEConsulta.Tests.csproj`.
4. Se a mudanca tocar em contrato publico, atualize a documentacao correspondente.
5. Nao inclua certificados, XML reais, senhas ou dados internos.

## Como reportar problema

Use o template de bug em `.github/ISSUE_TEMPLATE/bug_report.md` e inclua:

- versao do pacote;
- versao do .NET;
- sistema operacional;
- UF e ambiente;
- passos para reproduzir;
- logs ou codigo de erro.

## Como propor melhoria

Use o template de feature em `.github/ISSUE_TEMPLATE/feature_request.md` e explique:

- qual dor a mudanca resolve;
- qual comportamento voce espera;
- porque isso e util em cenarios reais de producao.

## O que priorizar

- Correcoes de bugs.
- Melhoria de documentacao.
- Novos testes.
- Ajustes de confiabilidade e observabilidade.

## O que evitar

- Mudancas grandes sem necessidade real.
- Dependencias pesadas sem justificativa.
- Misturar regra fiscal com infraestrutura de transporte.
- Introduzir comportamento implicito quando um option ou override resolve.

## Estilo

- Mantenha o codigo em ASCII quando possivel.
- Prefira contratos tipados.
- Prefira options e overrides claros.
- Preserve a separacao entre `Infrastructure`, `Models`, `Services` e `Extensions`.
