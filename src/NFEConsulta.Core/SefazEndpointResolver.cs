using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

public static class SefazEndpointResolver
{
    private const string SvrsProducao = "https://nfe.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx";
    private const string SvrsHomologacao = "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx";
    private const string SvrsStatusProducao = "https://nfe.svrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx";
    private const string SvrsStatusHomologacao = "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx";

    private static readonly IReadOnlyDictionary<UfNFe, SefazEndpointInfo> ConsultaProtocoloEndpoints =
        new Dictionary<UfNFe, SefazEndpointInfo>
        {
            [UfNFe.AC] = SefazEndpointInfo.Svrs(UfNFe.AC),
            [UfNFe.AL] = SefazEndpointInfo.Svrs(UfNFe.AL),
            [UfNFe.AP] = SefazEndpointInfo.Svrs(UfNFe.AP),
            [UfNFe.DF] = SefazEndpointInfo.Svrs(UfNFe.DF),
            [UfNFe.ES] = SefazEndpointInfo.Svrs(UfNFe.ES),
            [UfNFe.PB] = SefazEndpointInfo.Svrs(UfNFe.PB),
            [UfNFe.PI] = SefazEndpointInfo.Svrs(UfNFe.PI),
            [UfNFe.RJ] = SefazEndpointInfo.Svrs(UfNFe.RJ),
            [UfNFe.RN] = SefazEndpointInfo.Svrs(UfNFe.RN),
            [UfNFe.RO] = SefazEndpointInfo.Svrs(UfNFe.RO),
            [UfNFe.RR] = SefazEndpointInfo.Svrs(UfNFe.RR),
            [UfNFe.RS] = SefazEndpointInfo.Svrs(UfNFe.RS),
            [UfNFe.SC] = SefazEndpointInfo.Svrs(UfNFe.SC),
            [UfNFe.SE] = SefazEndpointInfo.Svrs(UfNFe.SE),
            [UfNFe.TO] = SefazEndpointInfo.Svrs(UfNFe.TO),

            [UfNFe.SP] = new(
                UfNFe.SP,
                "SEFAZ-SP",
                "https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
                "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
                ValidacaoEndpoint.ValidadoLocalmente),

            [UfNFe.MG] = new(
                UfNFe.MG,
                "SEFAZ-MG",
                "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeConsultaProtocolo4",
                "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeConsultaProtocolo4"),

            [UfNFe.PR] = new(
                UfNFe.PR,
                "SEFAZ-PR",
                "https://nfe.fazenda.pr.gov.br/nfe/NFeConsultaProtocolo4",
                "https://homologacao.nfe.fazenda.pr.gov.br/nfe/NFeConsultaProtocolo4"),

            [UfNFe.AM] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.AM),
            [UfNFe.BA] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.BA),
            [UfNFe.CE] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.CE),
            [UfNFe.GO] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.GO),
            [UfNFe.MA] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.MA),
            [UfNFe.MT] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.MT),
            [UfNFe.MS] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.MS),
            [UfNFe.PA] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.PA),
            [UfNFe.PE] = SefazEndpointInfo.SemEndpointConfirmado(UfNFe.PE)
        };

    private static readonly IReadOnlyDictionary<UfNFe, SefazEndpointInfo> StatusServicoEndpoints =
        ConsultaProtocoloEndpoints.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Autorizador == "SVRS"
                ? new SefazEndpointInfo(pair.Key, "SVRS", SvrsStatusProducao, SvrsStatusHomologacao)
                : pair.Key switch
                {
                    UfNFe.SP => new SefazEndpointInfo(
                        UfNFe.SP,
                        "SEFAZ-SP",
                        "https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
                        "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
                        ValidacaoEndpoint.ValidadoLocalmente),

                    UfNFe.MG => new SefazEndpointInfo(
                        UfNFe.MG,
                        "SEFAZ-MG",
                        "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeStatusServico4",
                        "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeStatusServico4"),

                    UfNFe.PR => new SefazEndpointInfo(
                        UfNFe.PR,
                        "SEFAZ-PR",
                        "https://nfe.fazenda.pr.gov.br/nfe/NFeStatusServico4",
                        "https://homologacao.nfe.fazenda.pr.gov.br/nfe/NFeStatusServico4"),

                    _ => SefazEndpointInfo.SemEndpointConfirmado(pair.Key)
                });

    public static IReadOnlyCollection<SefazEndpointInfo> ListarEndpointsConsultaProtocolo() =>
        ConsultaProtocoloEndpoints.Values
            .OrderBy(static endpoint => endpoint.Uf)
            .ToArray();

    public static IReadOnlyCollection<SefazEndpointInfo> ListarEndpointsStatusServico() =>
        StatusServicoEndpoints.Values
            .OrderBy(static endpoint => endpoint.Uf)
            .ToArray();

    public static string ResolveConsultaProtocolo(UfNFe uf, TipoAmbiente ambiente)
    {
        if (!ConsultaProtocoloEndpoints.TryGetValue(uf, out SefazEndpointInfo? endpoint))
            throw new NotSupportedException($"UF {uf} nao possui endpoint de consulta NF-e mapeado.");

        if (!endpoint.EstaMapeado)
        {
            throw new NotSupportedException(
                $"UF {uf} ainda nao possui endpoint de consulta NF-e confirmado na biblioteca. " +
                "Informe NFeConsultaOptions.UrlWebServiceOverride para usar essa UF.");
        }

        return ambiente == TipoAmbiente.Producao
            ? endpoint.UrlProducao!
            : endpoint.UrlHomologacao!;
    }

    public static string ResolveStatusServico(UfNFe uf, TipoAmbiente ambiente)
    {
        if (!StatusServicoEndpoints.TryGetValue(uf, out SefazEndpointInfo? endpoint))
            throw new NotSupportedException($"UF {uf} nao possui endpoint de status NF-e mapeado.");

        if (!endpoint.EstaMapeado)
        {
            throw new NotSupportedException(
                $"UF {uf} ainda nao possui endpoint de status NF-e confirmado na biblioteca. " +
                "Informe NFeConsultaOptions.UrlStatusWebServiceOverride para usar essa UF.");
        }

        return ambiente == TipoAmbiente.Producao
            ? endpoint.UrlProducao!
            : endpoint.UrlHomologacao!;
    }

    public static UfNFe InferUfFromChave(string chaveAcesso)
    {
        string chave = ChaveAcessoNFe.RequireValid(chaveAcesso);
        int codigoUf = int.Parse(chave[..2], System.Globalization.CultureInfo.InvariantCulture);

        return Enum.IsDefined(typeof(UfNFe), codigoUf)
            ? (UfNFe)codigoUf
            : throw new ArgumentException($"Codigo de UF invalido na chave de acesso: {codigoUf}.", nameof(chaveAcesso));
    }

    public static UfNFe ParseUf(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UF nao informada.", nameof(value));

        if (Enum.TryParse(value.Trim(), ignoreCase: true, out UfNFe uf))
            return uf;

        if (int.TryParse(value, out int codigoUf) && Enum.IsDefined(typeof(UfNFe), codigoUf))
            return (UfNFe)codigoUf;

        throw new ArgumentException("UF invalida. Use a sigla, por exemplo SP, ou o codigo IBGE da UF.");
    }
}

public sealed record SefazEndpointInfo(
    UfNFe Uf,
    string Autorizador,
    string? UrlProducao,
    string? UrlHomologacao,
    ValidacaoEndpoint Validacao = ValidacaoEndpoint.NaoValidadoNesteAmbiente)
{
    public bool EstaMapeado => !string.IsNullOrWhiteSpace(UrlProducao)
        && !string.IsNullOrWhiteSpace(UrlHomologacao);

    public static SefazEndpointInfo Svrs(UfNFe uf) => new(
        uf,
        "SVRS",
        "https://nfe.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx",
        "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx");

    public static SefazEndpointInfo SemEndpointConfirmado(UfNFe uf) => new(
            uf,
            "Nao confirmado",
            null,
            null,
            ValidacaoEndpoint.SemEndpointConfirmado);
}

public enum ValidacaoEndpoint
{
    ValidadoLocalmente,
    NaoValidadoNesteAmbiente,
    SemEndpointConfirmado
}
