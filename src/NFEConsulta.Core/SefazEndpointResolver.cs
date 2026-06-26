using NFEConsulta.Models;

namespace NFEConsulta.Infrastructure;

public static class SefazEndpointResolver
{
    private const string SvrsProducao = "https://nfe.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx";
    private const string SvrsHomologacao = "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta/NfeConsulta4.asmx";
    private const string SvrsStatusProducao = "https://nfe.svrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx";
    private const string SvrsStatusHomologacao = "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeStatusServico/NfeStatusServico4.asmx";

    private const string SvanStatusProducao = "https://www.sefazvirtual.fazenda.gov.br/NFeStatusServico4/NFeStatusServico4.asmx";
    private const string SvanStatusHomologacao = "https://hom.sefazvirtual.fazenda.gov.br/NFeStatusServico4/NFeStatusServico4.asmx";

    private static readonly IReadOnlyDictionary<UfNFe, SefazEndpointInfo> ConsultaProtocoloEndpoints =
        new Dictionary<UfNFe, SefazEndpointInfo>
        {
            [UfNFe.AC] = SefazEndpointInfo.Svrs(UfNFe.AC),
            [UfNFe.AL] = SefazEndpointInfo.Svrs(UfNFe.AL),
            [UfNFe.AP] = SefazEndpointInfo.Svrs(UfNFe.AP),
            [UfNFe.CE] = SefazEndpointInfo.Svrs(UfNFe.CE),
            [UfNFe.DF] = SefazEndpointInfo.Svrs(UfNFe.DF),
            [UfNFe.ES] = SefazEndpointInfo.Svrs(UfNFe.ES),
            [UfNFe.PA] = SefazEndpointInfo.Svrs(UfNFe.PA),
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

            [UfNFe.MA] = SefazEndpointInfo.Svan(UfNFe.MA),

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

            [UfNFe.AM] = new(
                UfNFe.AM,
                "SEFAZ-AM",
                "https://nfe.sefaz.am.gov.br/services2/services/NfeConsulta4",
                "https://homnfe.sefaz.am.gov.br/services2/services/NfeConsulta4"),

            [UfNFe.BA] = new(
                UfNFe.BA,
                "SEFAZ-BA",
                "https://nfe.sefaz.ba.gov.br/webservices/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx",
                "https://hnfe.sefaz.ba.gov.br/webservices/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx"),

            [UfNFe.GO] = new(
                UfNFe.GO,
                "SEFAZ-GO",
                "https://nfe.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4",
                "https://homolog.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4"),

            [UfNFe.MT] = new(
                UfNFe.MT,
                "SEFAZ-MT",
                "https://nfe.sefaz.mt.gov.br/nfews/v2/services/NfeConsulta4",
                "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/NfeConsulta4"),

            [UfNFe.MS] = new(
                UfNFe.MS,
                "SEFAZ-MS",
                "https://nfe.fazenda.ms.gov.br/ws/NFeConsultaProtocolo4",
                "https://homologacao.nfe.ms.gov.br/ws/NFeConsultaProtocolo4"),

            [UfNFe.PE] = new(
                UfNFe.PE,
                "SEFAZ-PE",
                "https://nfe.sefaz.pe.gov.br/nfe-service/services/NFeConsultaProtocolo4",
                "https://nfehomolog.sefaz.pe.gov.br/nfe-service/services/NFeConsultaProtocolo4")
        };

    private static readonly IReadOnlyDictionary<UfNFe, SefazEndpointInfo> StatusServicoEndpoints =
        ConsultaProtocoloEndpoints.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Autorizador switch
            {
                "SVRS" => new SefazEndpointInfo(pair.Key, "SVRS", SvrsStatusProducao, SvrsStatusHomologacao),
                "SVAN" => new SefazEndpointInfo(pair.Key, "SVAN", SvanStatusProducao, SvanStatusHomologacao),
                _ => pair.Key switch
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

                    UfNFe.AM => new SefazEndpointInfo(
                        UfNFe.AM,
                        "SEFAZ-AM",
                        "https://nfe.sefaz.am.gov.br/services2/services/NfeStatusServico4",
                        "https://homnfe.sefaz.am.gov.br/services2/services/NfeStatusServico4"),

                    UfNFe.BA => new SefazEndpointInfo(
                        UfNFe.BA,
                        "SEFAZ-BA",
                        "https://nfe.sefaz.ba.gov.br/webservices/NFeStatusServico4/NFeStatusServico4.asmx",
                        "https://hnfe.sefaz.ba.gov.br/webservices/NFeStatusServico4/NFeStatusServico4.asmx"),

                    UfNFe.GO => new SefazEndpointInfo(
                        UfNFe.GO,
                        "SEFAZ-GO",
                        "https://nfe.sefaz.go.gov.br/nfe/services/NFeStatusServico4",
                        "https://homolog.sefaz.go.gov.br/nfe/services/NFeStatusServico4"),

                    UfNFe.MT => new SefazEndpointInfo(
                        UfNFe.MT,
                        "SEFAZ-MT",
                        "https://nfe.sefaz.mt.gov.br/nfews/v2/services/NfeStatusServico4",
                        "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/NfeStatusServico4"),

                    UfNFe.MS => new SefazEndpointInfo(
                        UfNFe.MS,
                        "SEFAZ-MS",
                        "https://nfe.fazenda.ms.gov.br/ws/NFeStatusServico4",
                        "https://homologacao.nfe.ms.gov.br/ws/NFeStatusServico4"),

                    UfNFe.PE => new SefazEndpointInfo(
                        UfNFe.PE,
                        "SEFAZ-PE",
                        "https://nfe.sefaz.pe.gov.br/nfe-service/services/NFeStatusServico4",
                        "https://nfehomolog.sefaz.pe.gov.br/nfe-service/services/NFeStatusServico4"),

                    _ => SefazEndpointInfo.SemEndpointConfirmado(pair.Key)
                }
            });

    private static readonly IReadOnlyDictionary<UfNFe, SefazEndpointInfo> CadastroEndpoints =
        ConsultaProtocoloEndpoints.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Autorizador switch
            {
                "SVRS" => new SefazEndpointInfo(pair.Key, "SVRS",
                    "https://cad.svrs.rs.gov.br/ws/cadconsultacadastro/cadconsultacadastro4.asmx",
                    "https://cad-homologacao.svrs.rs.gov.br/ws/cadconsultacadastro/cadconsultacadastro4.asmx"),
                "SVAN" => new SefazEndpointInfo(pair.Key, "SVRS",
                    "https://cad.svrs.rs.gov.br/ws/cadconsultacadastro/cadconsultacadastro4.asmx",
                    "https://cad-homologacao.svrs.rs.gov.br/ws/cadconsultacadastro/cadconsultacadastro4.asmx"),
                _ => pair.Key switch
                {
                    UfNFe.SP => new SefazEndpointInfo(
                        UfNFe.SP,
                        "SEFAZ-SP",
                        "https://nfe.fazenda.sp.gov.br/ws/cadconsultacadastro4.asmx",
                        "https://homologacao.nfe.fazenda.sp.gov.br/ws/cadconsultacadastro4.asmx",
                        ValidacaoEndpoint.ValidadoLocalmente),

                    UfNFe.MG => new SefazEndpointInfo(
                        UfNFe.MG,
                        "SEFAZ-MG",
                        "https://nfe.fazenda.mg.gov.br/nfe2/services/CadConsultaCadastro4",
                        "https://hnfe.fazenda.mg.gov.br/nfe2/services/CadConsultaCadastro4"),

                    UfNFe.PR => new SefazEndpointInfo(
                        UfNFe.PR,
                        "SEFAZ-PR",
                        "https://nfe.sefa.pr.gov.br/nfe/CadConsultaCadastro4",
                        "https://homologacao.nfe.sefa.pr.gov.br/nfe/CadConsultaCadastro4"),

                    UfNFe.AM => new SefazEndpointInfo(
                        UfNFe.AM,
                        "SEFAZ-AM",
                        "https://nfe.sefaz.am.gov.br/services2/services/CadConsultaCadastro4",
                        "https://homnfe.sefaz.am.gov.br/services2/services/CadConsultaCadastro4"),

                    UfNFe.BA => new SefazEndpointInfo(
                        UfNFe.BA,
                        "SEFAZ-BA",
                        "https://nfe.sefaz.ba.gov.br/webservices/CadConsultaCadastro4/CadConsultaCadastro4.asmx",
                        "https://hnfe.sefaz.ba.gov.br/webservices/CadConsultaCadastro4/CadConsultaCadastro4.asmx"),

                    UfNFe.GO => new SefazEndpointInfo(
                        UfNFe.GO,
                        "SEFAZ-GO",
                        "https://nfe.sefaz.go.gov.br/nfe/services/CadConsultaCadastro4",
                        "https://homolog.sefaz.go.gov.br/nfe/services/CadConsultaCadastro4"),

                    UfNFe.MT => new SefazEndpointInfo(
                        UfNFe.MT,
                        "SEFAZ-MT",
                        "https://nfe.sefaz.mt.gov.br/nfews/v2/services/CadConsultaCadastro4",
                        "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/CadConsultaCadastro4"),

                    UfNFe.MS => new SefazEndpointInfo(
                        UfNFe.MS,
                        "SEFAZ-MS",
                        "https://nfe.sefaz.ms.gov.br/ws/CadConsultaCadastro4",
                        "https://homologacao.nfe.ms.gov.br/ws/CadConsultaCadastro4"),

                    UfNFe.PE => new SefazEndpointInfo(
                        UfNFe.PE,
                        "SEFAZ-PE",
                        "https://nfe.sefaz.pe.gov.br/nfe-service/services/CadConsultaCadastro4",
                        "https://nfehomolog.sefaz.pe.gov.br/nfe-service/services/CadConsultaCadastro4"),

                    _ => SefazEndpointInfo.SemEndpointConfirmado(pair.Key)
                }
            });

    public static IReadOnlyCollection<SefazEndpointInfo> ListarEndpointsConsultaProtocolo() =>
        ConsultaProtocoloEndpoints.Values
            .OrderBy(static endpoint => endpoint.Uf)
            .ToArray();

    public static IReadOnlyCollection<SefazEndpointInfo> ListarEndpointsStatusServico() =>
        StatusServicoEndpoints.Values
            .OrderBy(static endpoint => endpoint.Uf)
            .ToArray();

    public static IReadOnlyCollection<SefazEndpointInfo> ListarEndpointsCadastro() =>
        CadastroEndpoints.Values
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

    public static string ResolveCadastro(UfNFe uf, TipoAmbiente ambiente)
    {
        if (!CadastroEndpoints.TryGetValue(uf, out SefazEndpointInfo? endpoint))
            throw new NotSupportedException($"UF {uf} nao possui endpoint de consulta cadastro mapeado.");

        if (!endpoint.EstaMapeado)
        {
            throw new NotSupportedException(
                $"UF {uf} ainda nao possui endpoint de consulta cadastro confirmado na biblioteca. " +
                "Informe NFeConsultaOptions.UrlWebServiceOverride para usar essa UF.");
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

    public static SefazEndpointInfo Svan(UfNFe uf) => new(
        uf,
        "SVAN",
        "https://www.sefazvirtual.fazenda.gov.br/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx",
        "https://hom.sefazvirtual.fazenda.gov.br/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx");

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
