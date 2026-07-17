# Historico de alteracoes

## v1.0.1

### Principais melhorias:
- Testes migrados para xUnit: 34 aprovados e 1 integração real opcional ignorada.
- Rate limit por IP: 30 requisições por minuto nos endpoints SEFAZ.
- Sem autenticação/autorização, conforme solicitado.
- Limite de 5 MB para XML/requisições e 2 MB para respostas da SEFAZ.
- XSD protegido contra carregamento remoto ou fora da pasta autorizada.
- Certificados vencidos, sem chave privada ou inadequados para TLS são rejeitados.
- Retry para HTTP 408, 429, 500, 502, 503 e 504, respeitando Retry-After.
- Inferência automática da UF pela chave da NF-e corrigida.
- Suporte validado em .NET 8 e .NET 10.
- Pacote reduzido de 282 para somente 6 schemas XSD necessários.
- Dependências centralizadas e GitHub Actions fixadas por SHA.
- Documentação e CI atualizados.

### Validação final:
- Build: zero erros e zero avisos.
- xUnit: 34 aprovados, 1 ignorado, zero falhas.
- Cobertura: 58,47% de linhas e 39,20% de branches.
- Nenhum pacote vulnerável.
- Nenhuma dependência direta desatualizada.
- Formatação e git diff --check aprovados.
- Pacotes NFEConsulta 1.0.1 e NFEConsulta.Cli 1.0.1 gerados.

## Unreleased


## 0.4.0
- Adicionado suporte nativo para consulta de cadastro de contribuintes da SEFAZ via Web Service `CadConsultaCadastro4` (consulta por CNPJ, CPF ou IE).
- Introduzido `NFeCadastroClient`, `SefazCadastroService` e `SefazCadastroResponseParser`.
- Mapeamento completo dos endpoints de consulta de cadastro para todas as UFs (via SVRS e servidores próprios).

## 0.3.0
- Adicionada extracao de metadados de certificados digitais (Nome, CNPJ, Emissor, Datas e Thumbprint) via metodo de extensao `ExtrairInfo()`.
- Novo comando `certificado` na CLI para impressao de metadados de certificados de forma amigavel ou estruturada em JSON (usando `--json`).

## 0.2.5
- Correcao de testes unitarios para refletir o mapeamento completo das UFs.
- Publicacao da versao para sincronizar master e release.

## 0.2.4
- Mapeamento completo de URLs da SEFAZ para todas as UFs pendentes (AM, BA, CE, GO, MA, MT, MS, PA e PE), eliminando a necessidade de override manual de URL.
- Suporte a consultas nas infraestruturas da SVRS e SVAN atualizado para todos os estados correspondentes.

## 0.2.3
- Organizada a documentacao publica com hub, guias e contribuicao.
- Atualizada a versao publica para refletir as melhorias de documentacao.

## 0.2.2
- Corrigida a documentacao de namespaces e exemplos de `NFeXmlValidator`.
- Reforcada a documentacao publica da biblioteca.

## 0.2.1
- Adicionados helpers de decisao para workers e tratamento operacional mais claro.
- `CorrelationId` propagado em consulta e status.

## 0.2.0
- Introduzido status oficial da SEFAZ via `NFeStatusServico4`.
- `NFeStatusClient`, endpoints por UF e contratos tipados.
- Integracao real com certificado/XML e CI inicial.
