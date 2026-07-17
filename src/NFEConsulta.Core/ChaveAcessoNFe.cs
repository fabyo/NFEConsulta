using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NFEConsulta.Infrastructure;

public sealed record ChaveAcessoValidationResult(
    bool IsValid,
    string? ChaveAcesso,
    string? ErrorMessage);

/// <summary>
/// Normaliza, valida e extrai chave de acesso de NF-e.
/// </summary>
public static partial class ChaveAcessoNFe
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public static ChaveAcessoValidationResult Validate(string? value)
    {
        string chave = Normalize(value);

        if (chave.Length != 44)
            return Invalid("A chave de acesso deve conter exatamente 44 digitos numericos.");

        if (!chave.All(char.IsDigit))
            return Invalid("A chave de acesso deve conter apenas digitos numericos.");

        if (!DigitoVerificadorValido(chave))
            return Invalid("Digito verificador da chave de acesso invalido.");

        return new ChaveAcessoValidationResult(true, chave, null);
    }

    public static string RequireValid(string? value)
    {
        ChaveAcessoValidationResult result = Validate(value);
        return result.IsValid
            ? result.ChaveAcesso!
            : throw new ArgumentException(result.ErrorMessage, nameof(value));
    }

    public static string ExtractFromXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("XML nao informado.", nameof(xml));

        XmlInputLimits.EnsureTextWithinLimit(xml);

        XDocument doc;
        try
        {
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new System.IO.StringReader(xml);
            using var reader = System.Xml.XmlReader.Create(stringReader, settings);
            doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"XML invalido: {ex.Message}", nameof(xml));
        }

        string? chNFe = doc.Descendants(Nfe + "chNFe")
            .Select(e => e.Value)
            .FirstOrDefault(v => Validate(v).IsValid);

        if (chNFe is not null)
            return RequireValid(chNFe);

        string? idInfNFe = doc.Descendants(Nfe + "infNFe")
            .Select(e => e.Attribute("Id")?.Value)
            .FirstOrDefault(v => TryExtractFromInfNFeId(v, out _));

        if (idInfNFe is not null && TryExtractFromInfNFeId(idInfNFe, out string chaveFromId))
            return chaveFromId;

        string? match = ChaveRegex().Matches(xml)
            .Select(m => m.Value)
            .FirstOrDefault(v => Validate(v).IsValid);

        if (match is not null)
            return RequireValid(match);

        throw new ArgumentException("Nao foi encontrada uma chave de acesso valida no XML.", nameof(xml));
    }

    public static async Task<string> ExtractFromXmlAsync(
        Stream xmlStream,
        CancellationToken cancellationToken = default)
    {
        if (xmlStream is null)
            throw new ArgumentNullException(nameof(xmlStream));

        string xml = await XmlInputLimits
            .ReadTextAsync(xmlStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ExtractFromXml(xml);
    }

    public static async Task<string> ExtractFromXmlFileAsync(
        string xmlFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(xmlFilePath))
            throw new ArgumentException("Caminho do XML nao informado.", nameof(xmlFilePath));

        await using FileStream stream = File.OpenRead(xmlFilePath);
        return await ExtractFromXmlAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsDigit));

    private static bool TryExtractFromInfNFeId(string? id, out string chave)
    {
        chave = string.Empty;

        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase))
            return false;

        string candidate = id[3..];
        ChaveAcessoValidationResult result = Validate(candidate);
        if (!result.IsValid)
            return false;

        chave = result.ChaveAcesso!;
        return true;
    }

    private static bool DigitoVerificadorValido(string chave)
    {
        int soma = 0;
        int peso = 2;

        for (int i = 42; i >= 0; i--)
        {
            soma += (chave[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }

        int resto = soma % 11;
        int dv = resto is 0 or 1 ? 0 : 11 - resto;

        return dv == chave[43] - '0';
    }

    private static ChaveAcessoValidationResult Invalid(string message) =>
        new(false, null, message);

    [GeneratedRegex(@"(?<!\d)\d{44}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex ChaveRegex();
}
