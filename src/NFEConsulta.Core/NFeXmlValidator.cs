using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace NFEConsulta.Infrastructure;

public sealed record NFeXmlValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public static class NFeXmlValidator
{
    public static NFeXmlValidationResult ValidateXml(string xml, string xsdDirectory)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return Invalid("XML nao informado.");

        try
        {
            XmlInputLimits.EnsureTextWithinLimit(xml);
        }
        catch (InvalidDataException ex)
        {
            return Invalid(ex.Message);
        }

        XDocument document;
        try
        {
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new System.IO.StringReader(xml);
            using var reader = System.Xml.XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            return Invalid($"XML invalido: {ex.Message}");
        }

        XmlSchemaSet schemas = LoadSchemas(xsdDirectory, ResolveRootSchemaFile(document.Root));
        List<string> errors = [];

        document.Validate(
            schemas,
            (_, args) =>
            {
                XmlSchemaException exception = args.Exception;
                string location = exception.LineNumber > 0
                    ? $"Linha {exception.LineNumber}, coluna {exception.LinePosition}: "
                    : string.Empty;

                errors.Add($"{location}{args.Severity}: {args.Message}");
            },
            addSchemaInfo: false);

        return errors.Count == 0
            ? new NFeXmlValidationResult(true, Array.Empty<string>())
            : new NFeXmlValidationResult(false, errors);
    }

    public static async Task<NFeXmlValidationResult> ValidateXmlAsync(
        Stream xmlStream,
        string xsdDirectory,
        CancellationToken cancellationToken = default)
    {
        if (xmlStream is null)
            throw new ArgumentNullException(nameof(xmlStream));

        MemoryStream buffer;
        try
        {
            buffer = await XmlInputLimits
                .CopyToMemoryAsync(xmlStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            return Invalid(ex.Message);
        }

        using (buffer)
        {

            string rootSchemaFile;
            try
            {
                rootSchemaFile = ResolveRootSchemaFile(buffer);
                buffer.Position = 0;
            }
            catch (XmlException ex)
            {
                return Invalid($"XML invalido: {ex.Message}");
            }

            XmlSchemaSet schemas = LoadSchemas(xsdDirectory, rootSchemaFile);
            List<string> errors = [];

            try
            {
                XmlReaderSettings settings = new()
                {
                    Async = true,
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    ValidationType = ValidationType.Schema,
                    Schemas = schemas
                };

                settings.ValidationEventHandler += (_, args) =>
                {
                    XmlSchemaException exception = args.Exception;
                    string location = exception.LineNumber > 0
                        ? $"Linha {exception.LineNumber}, coluna {exception.LinePosition}: "
                        : string.Empty;

                    errors.Add($"{location}{args.Severity}: {args.Message}");
                };

                using XmlReader reader = XmlReader.Create(buffer, settings);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (XmlException ex)
            {
                return Invalid($"XML invalido: {ex.Message}");
            }

            return errors.Count == 0
                ? new NFeXmlValidationResult(true, Array.Empty<string>())
                : new NFeXmlValidationResult(false, errors);
        }
    }

    public static async Task<NFeXmlValidationResult> ValidateXmlFileAsync(
        string xmlFilePath,
        string xsdDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(xmlFilePath))
            throw new ArgumentException("Caminho do XML nao informado.", nameof(xmlFilePath));

        await using FileStream stream = File.OpenRead(xmlFilePath);
        return await ValidateXmlAsync(stream, xsdDirectory, cancellationToken).ConfigureAwait(false);
    }

    public static void RequireValidXml(string xml, string xsdDirectory)
    {
        NFeXmlValidationResult result = ValidateXml(xml, xsdDirectory);

        if (!result.IsValid)
            throw new ArgumentException(BuildErrorMessage(result));
    }

    public static async Task RequireValidXmlFileAsync(
        string xmlFilePath,
        string xsdDirectory,
        CancellationToken cancellationToken = default)
    {
        NFeXmlValidationResult result = await ValidateXmlFileAsync(
            xmlFilePath,
            xsdDirectory,
            cancellationToken).ConfigureAwait(false);

        if (!result.IsValid)
            throw new ArgumentException(BuildErrorMessage(result));
    }

    private static XmlSchemaSet LoadSchemas(string xsdDirectory, string rootSchemaFileName)
    {
        if (string.IsNullOrWhiteSpace(xsdDirectory))
            throw new ArgumentException("Diretorio de XSD nao informado.", nameof(xsdDirectory));

        if (!Directory.Exists(xsdDirectory))
            throw new DirectoryNotFoundException($"Diretorio de XSD nao encontrado: {xsdDirectory}");

        string[] xsdFiles = Directory.GetFiles(xsdDirectory, rootSchemaFileName, SearchOption.AllDirectories);

        if (xsdFiles.Length == 0)
            throw new InvalidOperationException($"XSD raiz '{rootSchemaFileName}' nao encontrado em: {xsdDirectory}");

        RestrictedXmlResolver resolver = new(xsdDirectory);
        XmlSchemaSet schemas = new() { XmlResolver = resolver };

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = resolver
        };

        string xsdFile = xsdFiles.OrderBy(static path => path, StringComparer.Ordinal).First();
        using XmlReader reader = XmlReader.Create(xsdFile, settings);
        schemas.Add(null, reader);

        schemas.Compile();
        return schemas;
    }

    private static string ResolveRootSchemaFile(Stream xmlStream)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using XmlReader reader = XmlReader.Create(xmlStream, settings);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
                return ResolveRootSchemaFile(reader.LocalName, reader.GetAttribute("versao"));
        }

        throw new XmlException("XML sem elemento raiz.");
    }

    private static string ResolveRootSchemaFile(XElement? root)
    {
        if (root is null)
            throw new XmlException("XML sem elemento raiz.");

        return ResolveRootSchemaFile(root.Name.LocalName, root.Attribute("versao")?.Value);
    }

    private static string ResolveRootSchemaFile(string rootLocalName, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new XmlException("Elemento raiz sem atributo versao.");

        if (version.Count(static character => character == '.') != 1
            || version.Any(static character => !char.IsAsciiDigit(character) && character != '.'))
        {
            throw new XmlException("A versao do XML possui formato invalido.");
        }

        string schemaPrefix = rootLocalName switch
        {
            "nfeProc" => "procNFe",
            "NFe" => "nfe",
            _ => rootLocalName
        };

        return $"{schemaPrefix}_v{version}.xsd";
    }

    private static NFeXmlValidationResult Invalid(string message) =>
        new(false, new[] { message });

    private static string BuildErrorMessage(NFeXmlValidationResult result) =>
        "XML nao passou na validacao XSD: " + string.Join(" | ", result.Errors);
}
