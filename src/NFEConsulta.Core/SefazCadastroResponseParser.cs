using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

/// <summary>
/// Parser para respostas XML do servico de consulta de cadastro da SEFAZ (consCad).
/// </summary>
public static class SefazCadastroResponseParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public static CadastroConsultaResult Parse(string xmlResposta, string? correlationId = null)
    {
        XDocument doc;
        try
        {
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new System.IO.StringReader(xmlResposta);
            using var reader = System.Xml.XmlReader.Create(stringReader, settings);
            doc = XDocument.Load(reader);
        }
        catch (Exception ex)
        {
            return CadastroConsultaResult.Erro(
                $"Resposta da SEFAZ nao e um XML valido: {ex.Message}",
                correlationId);
        }

        XElement? retConsCad = doc.Descendants(Nfe + "retConsCad").FirstOrDefault();
        if (retConsCad is null)
        {
            return CadastroConsultaResult.Erro(
                "Estrutura da resposta da SEFAZ nao reconhecida (ausencia de retConsCad).",
                correlationId);
        }

        XElement? infCons = retConsCad.Element(Nfe + "infCons");
        if (infCons is null)
        {
            return CadastroConsultaResult.Erro(
                "Estrutura da resposta da SEFAZ nao reconhecida (ausencia de infCons).",
                correlationId);
        }

        string cStat = infCons.Element(Nfe + "cStat")?.Value ?? string.Empty;
        string xMotivo = infCons.Element(Nfe + "xMotivo")?.Value ?? string.Empty;

        // cStat 111 (Cadastro localizado) e 112 (Cadastro localizado com multiplos resultados) sao retornos bem sucedidos
        bool sucesso = cStat is "111" or "112";

        var result = new CadastroConsultaResult
        {
            Sucesso = sucesso,
            CodigoStatus = cStat,
            Motivo = xMotivo,
            CorrelationId = correlationId
        };

        if (!sucesso && cStat != string.Empty)
        {
            result = result with { ErroDetalhado = $"Aviso do WebService SEFAZ: [{cStat}] {xMotivo}" };
        }

        var contribuintesList = new List<CadastroContribuinte>();
        foreach (XElement infCad in infCons.Elements(Nfe + "infCad"))
        {
            contribuintesList.Add(ParseInfCad(infCad));
        }

        return result with { Contribuintes = contribuintesList };
    }

    private static CadastroContribuinte ParseInfCad(XElement infCad)
    {
        string? ie = infCad.Element(Nfe + "IE")?.Value;
        string? cnpj = infCad.Element(Nfe + "CNPJ")?.Value;
        string? cpf = infCad.Element(Nfe + "CPF")?.Value;
        string uf = infCad.Element(Nfe + "UF")?.Value ?? string.Empty;
        string cSit = infCad.Element(Nfe + "cSit")?.Value ?? string.Empty;
        string xNome = infCad.Element(Nfe + "xNome")?.Value ?? string.Empty;
        string? xFant = infCad.Element(Nfe + "xFant")?.Value;
        string? xRegAp = infCad.Element(Nfe + "xRegAp")?.Value;
        string? cnae = infCad.Element(Nfe + "CNAE")?.Value;
        string? ieUnica = infCad.Element(Nfe + "IEUnica")?.Value;
        string? ieAtual = infCad.Element(Nfe + "IEAtual")?.Value;

        DateTime? dIniAtiv = ParseDate(infCad.Element(Nfe + "dIniAtiv")?.Value);
        DateTime? dUltSit = ParseDate(infCad.Element(Nfe + "dUltSit")?.Value);
        DateTime? dBaixa = ParseDate(infCad.Element(Nfe + "dBaixa")?.Value);

        EnderecoContribuinte? endereco = null;
        XElement? ender = infCad.Element(Nfe + "ender");
        if (ender is not null)
        {
            endereco = new EnderecoContribuinte
            {
                Logradouro = ender.Element(Nfe + "xLgr")?.Value,
                Numero = ender.Element(Nfe + "nro")?.Value,
                Complemento = ender.Element(Nfe + "xCpl")?.Value,
                Bairro = ender.Element(Nfe + "xBairro")?.Value,
                CodigoMunicipio = ender.Element(Nfe + "cMun")?.Value,
                NomeMunicipio = ender.Element(Nfe + "xMun")?.Value,
                CEP = ender.Element(Nfe + "CEP")?.Value
            };
        }

        string situacaoLegivel = cSit switch
        {
            "0" => "Nao habilitado",
            "1" => "Habilitado",
            _ => cSit
        };

        return new CadastroContribuinte
        {
            IE = ie,
            CNPJ = cnpj,
            CPF = cpf,
            UF = uf,
            Situacao = situacaoLegivel,
            Nome = xNome,
            NomeFantasia = xFant,
            RegimeApuracao = xRegAp,
            CNAE = cnae,
            DataInicioAtividade = dIniAtiv,
            DataUltimaSituacao = dUltSit,
            DataBaixa = dBaixa,
            IEUnica = ieUnica,
            IEAtual = ieAtual,
            Endereco = endereco
        };
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, out DateTime dt))
            return dt;

        return null;
    }
}
