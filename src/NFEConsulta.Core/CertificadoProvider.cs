using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Encapsula o acesso ao repositorio de certificados do Windows.
/// </summary>
public static class CertificadoProvider
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    public static X509Certificate2 ObterPorThumbprint(string thumbprint) =>
        ObterPorStore(
            X509FindType.FindByThumbprint,
            NormalizeCertificateIdentifier(thumbprint),
            $"Certificado com thumbprint '{thumbprint}' nao encontrado em CurrentUser\\My.");

    public static X509Certificate2 ObterPorNumeroSerie(string numeroSerie) =>
        ObterPorStore(
            X509FindType.FindBySerialNumber,
            NormalizeCertificateIdentifier(numeroSerie),
            $"Certificado com serie '{numeroSerie}' nao encontrado em CurrentUser\\My.");

    public static X509Certificate2 ObterPorSubject(string subjectContains)
    {
        if (string.IsNullOrWhiteSpace(subjectContains))
            throw new ArgumentException("Subject do certificado nao informado.", nameof(subjectContains));

        using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2? cert = store.Certificates
            .Find(X509FindType.FindBySubjectName, subjectContains, validOnly: false)
            .OfType<X509Certificate2>()
            .Where(IsUsableForClientAuthentication)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();

        return cert is not null
            ? new X509Certificate2(cert)
            : throw new InvalidOperationException(
                $"Nenhum certificado com chave privada encontrado com subject contendo '{subjectContains}'.");
    }

    public static X509Certificate2 ObterPorPfx(string pfxPath, string? password)
    {
        if (string.IsNullOrWhiteSpace(pfxPath))
            throw new ArgumentException("Caminho do PFX nao informado.", nameof(pfxPath));

        if (!File.Exists(pfxPath))
            throw new FileNotFoundException("Arquivo PFX nao encontrado.", pfxPath);

        X509KeyStorageFlags flags = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet;

#if NET9_0_OR_GREATER
        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, flags);
#else
        X509Certificate2 certificate = new(pfxPath, password, flags);
#endif

        return EnsureUsable(certificate);
    }

    public static X509Certificate2 ObterPorPem(string certificatePemPath, string privateKeyPemPath)
    {
        if (string.IsNullOrWhiteSpace(certificatePemPath))
            throw new ArgumentException("Caminho do certificado PEM nao informado.", nameof(certificatePemPath));

        if (string.IsNullOrWhiteSpace(privateKeyPemPath))
            throw new ArgumentException("Caminho da chave privada PEM nao informado.", nameof(privateKeyPemPath));

        if (!File.Exists(certificatePemPath))
            throw new FileNotFoundException("Arquivo de certificado PEM nao encontrado.", certificatePemPath);

        if (!File.Exists(privateKeyPemPath))
            throw new FileNotFoundException("Arquivo de chave privada PEM nao encontrado.", privateKeyPemPath);

        using X509Certificate2 certificate = X509Certificate2.CreateFromPemFile(
            certificatePemPath,
            privateKeyPemPath);

        byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12);

#if NET9_0_OR_GREATER
        X509Certificate2 loadedCertificate = X509CertificateLoader.LoadPkcs12(pkcs12, password: null);
#else
        X509Certificate2 loadedCertificate = new(pkcs12);
#endif

        return EnsureUsable(loadedCertificate);
    }

    private static X509Certificate2 ObterPorStore(
        X509FindType findType,
        string findValue,
        string notFoundMessage)
    {
        if (string.IsNullOrWhiteSpace(findValue))
            throw new ArgumentException("Identificador do certificado nao informado.", nameof(findValue));

        using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection collection = store.Certificates.Find(
            findType,
            findValue,
            validOnly: false);

        X509Certificate2? cert = collection
            .OfType<X509Certificate2>()
            .Where(IsUsableForClientAuthentication)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();

        return cert is not null
            ? new X509Certificate2(cert)
            : throw new InvalidOperationException(notFoundMessage);
    }

    private static string NormalizeCertificateIdentifier(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

    private static X509Certificate2 EnsureUsable(X509Certificate2 certificate)
    {
        if (IsUsableForClientAuthentication(certificate))
            return certificate;

        certificate.Dispose();
        throw new InvalidOperationException(
            "O certificado deve possuir chave privada, estar dentro da validade e permitir autenticacao de cliente TLS.");
    }

    private static bool IsUsableForClientAuthentication(X509Certificate2 certificate)
    {
        DateTime utcNow = DateTime.UtcNow;
        if (!certificate.HasPrivateKey
            || certificate.NotBefore.ToUniversalTime() > utcNow
            || certificate.NotAfter.ToUniversalTime() <= utcNow)
        {
            return false;
        }

        X509EnhancedKeyUsageExtension? enhancedKeyUsage = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        return enhancedKeyUsage is null
            || enhancedKeyUsage.EnhancedKeyUsages
                .OfType<Oid>()
                .Any(oid => oid.Value == ClientAuthenticationOid);
    }
}
