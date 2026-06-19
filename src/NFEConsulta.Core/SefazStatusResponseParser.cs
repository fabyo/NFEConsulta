using System.Xml.Linq;
using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

public static class SefazStatusResponseParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public static SefazStatusResult Parse(
        string xmlResposta,
        UfNFe uf,
        TipoAmbiente ambiente,
        string urlWebService,
        TimeSpan duracao,
        string? correlationId = null)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlResposta);
        }
        catch (Exception ex)
        {
            return SefazStatusResult.Erro(
                $"Resposta da SEFAZ nao e um XML valido: {ex.Message}",
                uf,
                ambiente,
                urlWebService,
                duracao,
                TipoResultadoConsulta.FalhaXml,
                respostaSefazRecebida: true,
                correlationId: correlationId);
        }

        XElement? retConsStatServ = doc.Descendants(Nfe + "retConsStatServ").FirstOrDefault();
        if (retConsStatServ is null)
        {
            return SefazStatusResult.Erro(
                "Estrutura da resposta de status da SEFAZ nao reconhecida.",
                uf,
                ambiente,
                urlWebService,
                duracao,
                TipoResultadoConsulta.FalhaXml,
                respostaSefazRecebida: true,
                correlationId: correlationId);
        }

        string cStat = retConsStatServ.Element(Nfe + "cStat")?.Value ?? string.Empty;
        string xMotivo = retConsStatServ.Element(Nfe + "xMotivo")?.Value ?? string.Empty;
        bool online = cStat == "107";

        return new SefazStatusResult
        {
            Online = online,
            CodigoStatus = cStat,
            Motivo = xMotivo,
            Uf = uf,
            Ambiente = ambiente,
            UrlWebService = urlWebService,
            Duracao = duracao,
            TipoResultado = online
                ? TipoResultadoConsulta.ConsultaOk
                : TipoResultadoConsulta.RespostaSefazNegativa,
            RespostaSefazRecebida = true,
            CorrelationId = correlationId
        };
    }
}
