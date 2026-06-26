# Historico de alteracoes

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
