using System.Xml.Linq;
using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Constroi envelopes SOAP para os Web Services da SEFAZ usando XDocument.
/// </summary>
public static class SoapEnvelopeBuilder
{
    private static readonly XNamespace Soap12 = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace ConsultaWsdl = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4";
    private static readonly XNamespace StatusWsdl = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4";
    private static readonly XNamespace CadastroWsdl = "http://www.portalfiscal.inf.br/nfe/wsdl/CadConsultaCadastro4";
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public static string BuildConsultaProtocolo(string chaveAcesso, TipoAmbiente ambiente)
    {
        string chaveValidada = ChaveAcessoNFe.RequireValid(chaveAcesso);

        XDocument envelope = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap12 + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "soap12", Soap12),
                new XElement(Soap12 + "Body",
                    new XElement(ConsultaWsdl + "nfeDadosMsg",
                        new XElement(Nfe + "consSitNFe",
                            new XAttribute("versao", "4.00"),
                            new XElement(Nfe + "tpAmb", (int)ambiente),
                            new XElement(Nfe + "xServ", "CONSULTAR"),
                            new XElement(Nfe + "chNFe", chaveValidada)
                        )
                    )
                )
            )
        );

        return envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildStatusServico(UfNFe uf, TipoAmbiente ambiente)
    {
        XDocument envelope = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap12 + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "soap12", Soap12),
                new XElement(Soap12 + "Body",
                    new XElement(StatusWsdl + "nfeDadosMsg",
                        new XElement(Nfe + "consStatServ",
                            new XAttribute("versao", "4.00"),
                            new XElement(Nfe + "tpAmb", (int)ambiente),
                            new XElement(Nfe + "cUF", (int)uf),
                            new XElement(Nfe + "xServ", "STATUS")
                        )
                    )
                )
            )
        );

        return envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting);
    }

    public static string BuildCadastro(string documento, UfNFe uf, TipoAmbiente ambiente)
    {
        if (string.IsNullOrWhiteSpace(documento))
            throw new ArgumentException("Documento nao pode ser vazio.", nameof(documento));

        // Limpa formatacao do documento
        string docLimpo = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(documento, char.IsDigit)));
        if (string.IsNullOrWhiteSpace(docLimpo))
            docLimpo = documento.Trim(); // Fallback para IE que pode conter letras em alguns cenarios raros (embora IE seja numerica em geral)

        XElement docElement = docLimpo.Length switch
        {
            14 => new XElement(Nfe + "CNPJ", docLimpo),
            11 => new XElement(Nfe + "CPF", docLimpo),
            _ => new XElement(Nfe + "IE", docLimpo)
        };

        XDocument envelope = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap12 + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute(XNamespace.Xmlns + "soap12", Soap12),
                new XElement(Soap12 + "Body",
                    new XElement(CadastroWsdl + "nfeDadosMsg",
                        new XElement(Nfe + "consCad",
                            new XAttribute("versao", "2.00"),
                            new XElement(Nfe + "infCons",
                                new XElement(Nfe + "xServ", "CONS-CAD"),
                                new XElement(Nfe + "UF", uf.ToString()),
                                docElement
                            )
                        )
                    )
                )
            )
        );

        return envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting);
    }
}
