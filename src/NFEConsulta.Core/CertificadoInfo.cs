using System;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Informacoes extraidas de um certificado digital.
/// </summary>
public record CertificadoInfo(
    string Nome,
    string? Cnpj,
    DateTime DataEmissao,
    DateTime DataExpiracao,
    string Emissor,
    string Thumbprint
);
