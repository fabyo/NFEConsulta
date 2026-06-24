using System;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace NFEConsulta.Infrastructure;

public static class CertificadoExtensions
{
    private static readonly Regex CnpjRegex = new(@"([0-9]{14})", RegexOptions.Compiled);

    /// <summary>
    /// Extrai informacoes relevantes do certificado digital, como Nome e CNPJ.
    /// </summary>
    public static CertificadoInfo ExtrairInfo(this X509Certificate2 certificado)
    {
        if (certificado == null)
            throw new ArgumentNullException(nameof(certificado));

        string subject = certificado.Subject;
        
        string nome = ExtractSubjectField(subject, "CN=");
        string? cnpj = null;

        var match = CnpjRegex.Match(subject);
        if (match.Success)
        {
            cnpj = match.Groups[1].Value;
        }

        return new CertificadoInfo(
            Nome: nome,
            Cnpj: cnpj,
            DataEmissao: certificado.NotBefore,
            DataExpiracao: certificado.NotAfter,
            Emissor: ExtractSubjectField(certificado.Issuer, "CN="),
            Thumbprint: certificado.Thumbprint
        );
    }

    private static string ExtractSubjectField(string subject, string fieldPrefix)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return string.Empty;

        var fields = subject.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var field in fields)
        {
            if (field.StartsWith(fieldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = field.Substring(fieldPrefix.Length);
                
                // Se o valor contiver o CNPJ no formato NOME:CNPJ, vamos retornar apenas o nome
                var colonIndex = value.LastIndexOf(':');
                if (colonIndex > 0 && colonIndex + 15 == value.Length)
                {
                    var possibleCnpj = value.Substring(colonIndex + 1);
                    if (CnpjRegex.IsMatch(possibleCnpj))
                    {
                        return value.Substring(0, colonIndex);
                    }
                }
                
                return value;
            }
        }
        return subject;
    }
}
