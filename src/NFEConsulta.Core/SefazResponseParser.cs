using System.Xml.Linq;
using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

public static class SefazResponseParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public static ConsultaNFeResult Parse(string xmlResposta)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlResposta);
        }
        catch (Exception ex)
        {
            return ConsultaNFeResult.Erro($"Resposta da SEFAZ nao e um XML valido: {ex.Message}");
        }

        // retConsSitNFe traz o status atual da consulta. Ele deve ter prioridade
        // sobre infProt, pois uma NF-e cancelada ainda carrega o protocolo original.
        XElement? retConsSit = doc.Descendants(Nfe + "retConsSitNFe").FirstOrDefault();
        if (retConsSit is not null)
            return ParseRetConsSit(retConsSit);

        XElement? infProt = doc.Descendants(Nfe + "infProt").FirstOrDefault();
        if (infProt is not null)
            return ParseInfProt(infProt);

        return ConsultaNFeResult.Erro("Estrutura da resposta da SEFAZ nao reconhecida.");
    }

    private static ConsultaNFeResult ParseInfProt(XElement infProt)
    {
        string cStat = infProt.Element(Nfe + "cStat")?.Value ?? string.Empty;
        string xMotivo = infProt.Element(Nfe + "xMotivo")?.Value ?? string.Empty;
        string? nProt = infProt.Element(Nfe + "nProt")?.Value;
        DateTimeOffset? dhRecbto = null;

        if (DateTimeOffset.TryParse(infProt.Element(Nfe + "dhRecbto")?.Value, out DateTimeOffset dt))
            dhRecbto = dt;

        return new ConsultaNFeResult
        {
            Sucesso = true,
            CodigoStatus = cStat,
            Motivo = xMotivo,
            Status = ResolverStatus(cStat),
            NumeroProtocolo = nProt,
            DataAutorizacao = dhRecbto
        };
    }

    private static ConsultaNFeResult ParseRetConsSit(XElement retConsSit)
    {
        string cStat = retConsSit.Element(Nfe + "cStat")?.Value ?? string.Empty;
        string xMotivo = retConsSit.Element(Nfe + "xMotivo")?.Value ?? string.Empty;
        StatusNFe status = ResolverStatus(cStat);

        if (status == StatusNFe.Cancelada)
            return ParseCancelamento(retConsSit, cStat, xMotivo);

        if (status is StatusNFe.Autorizada or StatusNFe.Denegada)
        {
            XElement? infProt = retConsSit.Descendants(Nfe + "infProt").FirstOrDefault();
            if (infProt is not null)
                return ParseInfProt(infProt);
        }

        return new ConsultaNFeResult
        {
            Sucesso = status != StatusNFe.Desconhecido,
            CodigoStatus = cStat,
            Motivo = xMotivo,
            Status = status,
            ErroDetalhado = status == StatusNFe.Desconhecido
                ? $"Aviso do WebService SEFAZ: [{cStat}] {xMotivo}"
                : null
        };
    }

    private static ConsultaNFeResult ParseCancelamento(XElement retConsSit, string cStat, string xMotivo)
    {
        XElement? retEvento = retConsSit.Descendants(Nfe + "retEvento").FirstOrDefault();
        XElement? infEvento = retEvento?.Element(Nfe + "infEvento");
        XElement? infCanc = retConsSit.Descendants(Nfe + "infCanc").FirstOrDefault();

        string? protocoloCancelamento =
            infEvento?.Element(Nfe + "nProt")?.Value ??
            infCanc?.Element(Nfe + "nProt")?.Value;

        DateTimeOffset? dataCancelamento = null;
        string? dataTexto =
            infEvento?.Element(Nfe + "dhRegEvento")?.Value ??
            infCanc?.Element(Nfe + "dhRecbto")?.Value ??
            retConsSit.Element(Nfe + "dhRecbto")?.Value;

        if (DateTimeOffset.TryParse(dataTexto, out DateTimeOffset parsedDate))
            dataCancelamento = parsedDate;

        string motivo =
            infEvento?.Element(Nfe + "xEvento")?.Value ??
            xMotivo;

        return new ConsultaNFeResult
        {
            Sucesso = true,
            CodigoStatus = cStat,
            Motivo = motivo,
            Status = StatusNFe.Cancelada,
            NumeroProtocolo = protocoloCancelamento,
            DataAutorizacao = dataCancelamento
        };
    }

    private static StatusNFe ResolverStatus(string cStat) => cStat switch
    {
        "100" => StatusNFe.Autorizada,
        "101" or "102" or "135" => StatusNFe.Cancelada,
        "110" or "301" or "302" => StatusNFe.Denegada,
        "217" => StatusNFe.NaoEncontrada,
        _ => StatusNFe.Desconhecido
    };
}
