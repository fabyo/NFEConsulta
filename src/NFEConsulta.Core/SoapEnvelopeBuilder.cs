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
}
