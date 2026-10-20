using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Ren genereringsberegning (Fase 3 Steg C): anker-kjeder, sirkularitet, tentativ-arv, valgår.</summary>
public class GenereringsberegningTester
{
    private static Gjentaksregel Regel(string loep, Regeltype type, string parametre, bool valgaarssensitiv = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Loep = loep,
            Tittel = loep,
            Kategori = Kategori.Budsjett,
            Regeltype = type,
            Regelparametre = parametre,
            Valgaarssensitiv = valgaarssensitiv
        };

    [Theory]
    [InlineData(2026, 3, 20, int.MinValue)]
    [InlineData(2026, 3, 20, int.MaxValue)]
    [InlineData(1, 1, 1, -1)]
    [InlineData(9999, 12, 31, 1)]
    public void Relativ_dato_utenfor_datoomraadet_gir_regelfeil_og_beholder_uavhengige_regler(
        int aar, int maaned, int dag, int offset)
    {
        var relativ = Regel("utenfor", Regeltype.RelativTilMilepael,
            $$"""{"anker_loep":"manuelt","offset_dager":{{offset}}}""");
        var gyldig = Regel("gyldig", Regeltype.FastDato, """{"maaned":3,"dag":20}""");

        var resultat = Genereringsberegning.Beregn(2026, [relativ, gyldig],
            new Dictionary<string, DateOnly> { ["manuelt"] = new(aar, maaned, dag) });

        var feil = Assert.Single(resultat, r => r.Regel.Loep == "utenfor");
        Assert.Null(feil.Dato);
        Assert.Contains("utenfor", feil.Feil!);
        Assert.Contains("0001-01-01", feil.Feil!);
        Assert.Contains("9999-12-31", feil.Feil!);
        var beregnet = Assert.Single(resultat, r => r.Regel.Loep == "gyldig");
        Assert.Null(beregnet.Feil);
        Assert.Equal(new DateOnly(2026, 3, 20), beregnet.Dato);
    }

    public static TheoryData<Regeltype, int, int> UgyldigeKalenderaar
    {
        get
        {
            var data = new TheoryData<Regeltype, int, int>();
            foreach (var type in new[] { Regeltype.FastDato, Regeltype.RelativUkedag })
            {
                data.Add(type, 2026, int.MinValue);
                data.Add(type, 2026, int.MaxValue);
                data.Add(type, 1, -1);
                data.Add(type, 9999, 1);
                // Uten bred årsregning ville disse summene ha blitt til 1 eller 9999.
                data.Add(type, int.MinValue, -2147483647);
                data.Add(type, int.MinValue, -2147473649);
                data.Add(type, int.MaxValue, int.MaxValue);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(UgyldigeKalenderaar))]
    public void Aarsforskyvning_utenfor_kalenderaar_gir_regelfeil_uten_heltallsoverloep(
        Regeltype type, int budsjettaar, int forskyvning)
    {
        var parametre = type == Regeltype.FastDato
            ? $$"""{"maaned":1,"dag":1,"aar_forskyvning":{{forskyvning}}}"""
            : $$"""{"maaned":1,"uke":1,"ukedag":"man","aar_forskyvning":{{forskyvning}}}""";
        var regel = Regel("utenfor-aar", type, parametre);

        var beregnet = Assert.Single(Genereringsberegning.Beregn(budsjettaar, [regel]));

        Assert.Null(beregnet.Dato);
        Assert.Contains("kalenderår", beregnet.Feil!);
        Assert.Contains("1–9999", beregnet.Feil!);
    }

    [Theory]
    [InlineData("man", 27)]
    [InlineData("fre", 31)]
    public void Femte_ukedag_i_desember_9999_beholder_siste_forekomst(string ukedag, int dag)
    {
        var regel = Regel("desember", Regeltype.RelativUkedag,
            $$"""{"maaned":12,"uke":5,"ukedag":"{{ukedag}}"}""");

        var beregnet = Assert.Single(Genereringsberegning.Beregn(9999, [regel]));

        Assert.Null(beregnet.Feil);
        Assert.Equal(new DateOnly(9999, 12, dag), beregnet.Dato);
    }

    [Theory]
    [InlineData(1, 1, 1, 3652058, 9999, 12, 31)]
    [InlineData(9999, 12, 31, -3652058, 1, 1, 1)]
    [InlineData(1, 1, 2, -1, 1, 1, 1)]
    [InlineData(9999, 12, 30, 1, 9999, 12, 31)]
    [InlineData(2028, 3, 1, 0, 2028, 3, 1)]
    [InlineData(2028, 3, 1, -1, 2028, 2, 29)]
    public void Representerbare_relative_datoer_beholdes_uten_vilkaarlig_forskyvningsgrense(
        int ankerAar, int ankerMaaned, int ankerDag, int offset, int aar, int maaned, int dag)
    {
        var regel = Regel("relativ", Regeltype.RelativTilMilepael,
            $$"""{"anker_loep":"manuelt","offset_dager":{{offset}}}""");

        var beregnet = Assert.Single(Genereringsberegning.Beregn(2026, [regel],
            new Dictionary<string, DateOnly> { ["manuelt"] = new(ankerAar, ankerMaaned, ankerDag) }));

        Assert.Null(beregnet.Feil);
        Assert.Equal(new DateOnly(aar, maaned, dag), beregnet.Dato);
    }

    [Theory]
    [InlineData(Regeltype.FastDato, false)]
    [InlineData(Regeltype.FastDato, true)]
    [InlineData(Regeltype.RelativUkedag, false)]
    [InlineData(Regeltype.RelativUkedag, true)]
    [InlineData(Regeltype.RelativTilMilepael, false)]
    [InlineData(Regeltype.RelativTilMilepael, true)]
    public void Datooverloep_forplanter_seg_gjennom_hele_ankerkjeden_uavhengig_av_rekkefoelge(
        Regeltype ankerType, bool avhengigeFoerst)
    {
        var anker = Regel("anker", ankerType, ankerType switch
        {
            Regeltype.FastDato => """{"maaned":1,"dag":1,"aar_forskyvning":2147483647}""",
            Regeltype.RelativUkedag => """{"maaned":1,"uke":1,"ukedag":"man","aar_forskyvning":2147483647}""",
            _ => """{"anker_loep":"manuelt","offset_dager":2147483647}"""
        });
        var ledd = Regel("ledd", Regeltype.RelativTilMilepael, """{"anker_loep":"anker","offset_dager":-1}""");
        var siste = Regel("siste", Regeltype.RelativTilMilepael, """{"anker_loep":"ledd","offset_dager":1}""");
        var gyldig = Regel("gyldig", Regeltype.FastDato, """{"maaned":3,"dag":20}""");
        Gjentaksregel[] regler = avhengigeFoerst ? [siste, ledd, anker, gyldig] : [anker, ledd, siste, gyldig];

        var resultat = Genereringsberegning.Beregn(2026, regler,
            new Dictionary<string, DateOnly> { ["manuelt"] = new(2026, 3, 20) });

        var ankerfeil = Assert.Single(resultat, r => r.Regel.Loep == "anker").Feil;
        Assert.NotNull(ankerfeil);
        Assert.All(resultat.Where(r => r.Regel.Loep != "gyldig"), r =>
        {
            Assert.Equal(ankerfeil, r.Feil);
            Assert.Null(r.Dato);
        });
        var beregnet = Assert.Single(resultat, r => r.Regel.Loep == "gyldig");
        Assert.Null(beregnet.Feil);
        Assert.Equal(new DateOnly(2026, 3, 20), beregnet.Dato);
    }

    [Fact]
    public void Manuelt_anker_overstyrer_datooverloep_ogsaa_ved_rekursivt_oppslag()
    {
        var anker = Regel("anker", Regeltype.RelativTilMilepael,
            """{"anker_loep":"opprinnelig","offset_dager":2147483647}""");
        var ledd = Regel("ledd", Regeltype.RelativTilMilepael, """{"anker_loep":"anker","offset_dager":-1}""");
        var siste = Regel("siste", Regeltype.RelativTilMilepael, """{"anker_loep":"ledd","offset_dager":1}""");

        var resultat = Genereringsberegning.Beregn(2026, [siste, ledd, anker],
            new Dictionary<string, DateOnly>
            {
                ["opprinnelig"] = DateOnly.MinValue,
                ["anker"] = new(2026, 3, 20)
            });

        Assert.All(resultat, r => Assert.Null(r.Feil));
        Assert.Equal(new DateOnly(2026, 3, 20), Assert.Single(resultat, r => r.Regel.Loep == "anker").Dato);
        Assert.Equal(new DateOnly(2026, 3, 19), Assert.Single(resultat, r => r.Regel.Loep == "ledd").Dato);
        Assert.Equal(new DateOnly(2026, 3, 20), Assert.Single(resultat, r => r.Regel.Loep == "siste").Dato);
    }

    [Fact]
    public void Foerste_ukedag_etter_oevre_datogrense_gir_kontrollert_regelfeil()
    {
        var regel = Regel("neste-mandag", Regeltype.RelativUkedag,
            """{"maaned":12,"fra_dag":31,"ukedag":"man"}""");

        var beregnet = Assert.Single(Genereringsberegning.Beregn(9999, [regel]));

        Assert.NotNull(beregnet.Feil);
        Assert.Null(beregnet.Dato);
    }

    [Fact]
    public void FastDato_beregnes_i_riktig_kalenderaar_med_forskyvning()
    {
        var regel = Regel("marskonferanse", Regeltype.FastDato, "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        var r = Assert.Single(Genereringsberegning.Beregn(2030, [regel]));

        Assert.False(r.ErFeil);
        Assert.Equal(2029, r.Dato!.Value.Year); // 2030 + (-1)
        Assert.Equal(3, r.Dato.Value.Month);
        Assert.False(r.Tentativ);
    }

    [Fact]
    public void RelativTilMilepael_henger_paa_ankerets_dato()
    {
        var anker = Regel("fremleggelse", Regeltype.FastDato, "{\"maaned\":10,\"dag\":6,\"aar_forskyvning\":-1}");
        var avhengig = Regel("rammenotat", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"fremleggelse\",\"offset_dager\":-7}");

        var resultat = Genereringsberegning.Beregn(2030, [anker, avhengig]);
        var ankerR = resultat.Single(r => r.Regel.Loep == "fremleggelse");
        var avhR = resultat.Single(r => r.Regel.Loep == "rammenotat");

        Assert.Equal(ankerR.Dato!.Value.AddDays(-7), avhR.Dato!.Value);
        Assert.False(avhR.Tentativ);
    }

    [Fact]
    public void Sirkulaer_ankerkjede_feiler_tydelig()
    {
        var a = Regel("a", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"b\",\"offset_dager\":1}");
        var b = Regel("b", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"a\",\"offset_dager\":1}");

        var resultat = Genereringsberegning.Beregn(2030, [a, b]);

        Assert.All(resultat, r => Assert.True(r.ErFeil));
        Assert.Contains(resultat, r => r.Feil!.Contains("Sirkulær", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Manglende_anker_feiler_uten_aa_gjette()
    {
        var avhengig = Regel("rammenotat", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"finnesikke\",\"offset_dager\":-7}");

        var r = Assert.Single(Genereringsberegning.Beregn(2030, [avhengig]));

        Assert.True(r.ErFeil);
        Assert.Null(r.Dato);
        Assert.Contains("Mangler anker", r.Feil!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Valgaarssensitiv_i_stortingsvalgaar_blir_tentativ_uten_gjettet_dag()
    {
        // 2025 er stortingsvalgår; frist faller samme år (forskyvning 0).
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        var r = Assert.Single(Genereringsberegning.Beregn(2025, [regel]));

        Assert.True(r.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, r.Presisjon);
        Assert.True(r.Valgaarsflagg);
    }

    [Fact]
    public void Valgaarssensitiv_i_kommunevalgaar_beholder_konkret_dato_med_mildt_flagg()
    {
        // 2027 er kommunevalgår.
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        var r = Assert.Single(Genereringsberegning.Beregn(2027, [regel]));

        Assert.False(r.Tentativ);
        Assert.Equal(Datopresisjon.Dag, r.Presisjon);
        Assert.True(r.Valgaarsflagg);
    }

    [Fact]
    public void Tentativitet_arves_nedover_ankerkjeden()
    {
        // Tentativt valgårssensitivt anker i stortingsvalgår; avhengig frist er ikke selv valgårssensitiv.
        var anker = Regel("haustkonferanse", Regeltype.FastDato, "{\"maaned\":8,\"dag\":20,\"aar_forskyvning\":0}", valgaarssensitiv: true);
        var avhengig = Regel("gulbok", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"haustkonferanse\",\"offset_dager\":21}");

        var resultat = Genereringsberegning.Beregn(2025, [anker, avhengig]);
        var avhR = resultat.Single(r => r.Regel.Loep == "gulbok");

        Assert.True(avhR.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, avhR.Presisjon);
    }

    [Fact]
    public void Manuelt_satt_anker_overstyrer_tentativ_beregning()
    {
        var regel = Regel("gulbok", Regeltype.FastDato, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);
        var manuell = new Dictionary<string, DateOnly> { ["gulbok"] = new(2025, 9, 22) };

        var r = Assert.Single(Genereringsberegning.Beregn(2025, [regel], manuell));

        Assert.Equal(new DateOnly(2025, 9, 22), r.Dato);
        Assert.False(r.Tentativ);
    }

    [Fact]
    public void RelativUkedag_med_fra_dag_beregnes_og_lander_i_uken_20_til_26()
    {
        // «Mandagen i uken 20.–26. juli» = første mandag på/etter 20. juli. Skal alltid gi en
        // konkret dato (regresjon: fra_dag-regler kom ikke med i genereringen tidligere).
        var regel = Regel("fag-rammefordelingsforslag", Regeltype.RelativUkedag,
            "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}");

        var r = Assert.Single(Genereringsberegning.Beregn(2026, [regel]));

        Assert.False(r.ErFeil);
        Assert.NotNull(r.Dato);
        Assert.Equal(DayOfWeek.Monday, r.Dato!.Value.DayOfWeek);
        Assert.Equal(7, r.Dato.Value.Month);
        Assert.InRange(r.Dato.Value.Day, 20, 26);
    }

    [Fact]
    public void To_regler_med_samme_loep_beregnes_begge_uten_aa_slukes()
    {
        // Regresjon for #4: bruker la til en ny regel med et løp som allerede fantes i malen
        // («rammefordeling»). Tidligere ble den andre regelen stille droppet (TryAdd per løp).
        var seed = Regel("rammefordeling", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":-1}");
        var ny = Regel("rammefordeling", Regeltype.RelativUkedag, "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}");
        ny.Tittel = "FAGs frist for rammefordelingsforslag";

        var resultat = Genereringsberegning.Beregn(2026, [seed, ny]);

        Assert.Equal(2, resultat.Count);
        Assert.Contains(resultat, r => r.Regel.Tittel == "FAGs frist for rammefordelingsforslag" && r.Dato is { Month: 7 });
        Assert.Contains(resultat, r => r.Regel.Tittel == "rammefordeling" && r.Dato is { Month: 3 });
    }
}
