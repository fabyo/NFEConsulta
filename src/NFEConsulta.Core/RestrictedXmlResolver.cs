using System.Xml;

namespace NFEConsulta.Infrastructure;

internal sealed class RestrictedXmlResolver : XmlUrlResolver
{
    private readonly string _rootDirectory;
    private readonly StringComparison _pathComparison;

    public RestrictedXmlResolver(string rootDirectory)
    {
        string fullRoot = Path.GetFullPath(rootDirectory);
        _rootDirectory = Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
        EnsureAllowed(base.ResolveUri(baseUri, relativeUri));

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn) =>
        base.GetEntity(EnsureAllowed(absoluteUri), role, ofObjectToReturn);

    private Uri EnsureAllowed(Uri uri)
    {
        if (!uri.IsFile)
            throw new XmlException("Schemas XSD externos ou remotos nao sao permitidos.");

        string fullPath = Path.GetFullPath(uri.LocalPath);
        if (!fullPath.StartsWith(_rootDirectory, _pathComparison))
            throw new XmlException("O schema XSD tentou acessar um arquivo fora do diretorio permitido.");

        return new Uri(fullPath);
    }
}
