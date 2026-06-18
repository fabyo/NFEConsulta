using System.Security.Cryptography.X509Certificates;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Encapsula o acesso ao repositorio de certificados do Windows.
/// </summary>
public static class CertificadoProvider
{
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
            .Where(c => c.HasPrivateKey)
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
        return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, flags);
#else
        return new X509Certificate2(pfxPath, password, flags);
#endif
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

        X509Certificate2 certificate = X509Certificate2.CreateFromPemFile(certificatePemPath, privateKeyPemPath);

        byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12);

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(pkcs12, password: null);
#else
        return new X509Certificate2(pkcs12);
#endif
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
            .Where(c => c.HasPrivateKey)
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
}
