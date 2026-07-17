# Historico de alteracoes

## v1.0.1

- Migração de testes para xUnit com 34 testes aprovados e 1 teste de integração opcional ignorado.
- Implementação de Rate Limit configurado por IP (limite padrão de 30 requisições por minuto para endpoints da SEFAZ).
- Remoção e simplificação de mecanismos de autenticação/autorização nos endpoints locais.
- Definição de limites de tamanho para requisições XML (máximo de 5 MB) e para payloads de resposta da SEFAZ (máximo de 2 MB).
- Proteção de carregamento de esquemas XSD restringindo o acesso a caminhos locais autorizados e bloqueando carregamentos externos.
- Implementação de validação rigorosa de certificados digitais, rejeitando certificados expirados, sem chaves privadas associadas ou inadequados para comunicações TLS.
- Mecanismo de retransmissão (Retry) para códigos HTTP transitórios (408, 429, 500, 502, 503, 504) respeitando a diretiva 'Retry-After'.
- Correção na lógica de inferência automática da Unidade Federativa (UF) baseada na chave de acesso da NF-e.
- Homologação e validação de compatibilidade do SDK nos runtimes .NET 8 e .NET 10.
- Otimização do pacote com redução do conjunto de esquemas XSD distribuídos de 282 para apenas os 6 estritamente necessários.
- Centralização de dependências de projetos e fixação de versões de GitHub Actions por hashes SHA estáveis.
- Atualização geral da documentação do projeto e dos fluxos de Integração Contínua (CI).

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
