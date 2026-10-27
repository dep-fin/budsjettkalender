using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Testkontrakter;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

public sealed class MalregelForhaandsvisningTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IMaltjeneste Mal => new Maltjeneste(_t.Factory);

    [Fact]
    public async Task Fra_20_juli_viser_fem_korrekte_aar_uten_aa_lagre_utkastet()
    {
        var utkast = Regel("juli", Regeltype.RelativUkedag, """{"maaned":7,"ukedag":"man","fra_dag":20}""");

        var resultat = await Mal.ForhaandsvisAsync(utkast, 2026);

        Assert.Equal(new[] { 2026, 2027, 2028, 2029, 2030 }, resultat.Select(r => r.Budsjettaar));
        Assert.Equal(new DateOnly?[] { new(2026, 7, 20), new(2027, 7, 26), new(2028, 7, 24), new(2029, 7, 23), new(2030, 7, 22) }, resultat.Select(r => r.Dato));
        Assert.All(resultat, r => { Assert.False(r.ErFeil); Assert.Equal(Datopresisjon.Dag, r.Presisjon); });
        Assert.Empty(await Mal.HentAlleAsync());
    }

    [Theory]
    [InlineData(2, 3, 9)]
    [InlineData(5, 2, 23)]
    public async Task Forekomst_beholder_andre_og_femtes_siste_reserve(int uke, int maaned, int dag)
    {
        var resultat = await Mal.ForhaandsvisAsync(Regel("uke", Regeltype.RelativUkedag,
            $$"""{"maaned":{{maaned}},"uke":{{uke}},"ukedag":"man"}"""), 2026);
        Assert.Equal(new DateOnly(2026, maaned, dag), resultat[0].Dato);
        Assert.False(resultat[0].ErFeil);
    }

    [Fact]
    public async Task Skuddag_er_gyldig_mal_med_feil_bare_i_ikke_skuddaar()
    {
        var utkast = Regel("skuddag", Regeltype.FastDato, """{"maaned":2,"dag":29}""");
        var resultat = await Mal.ForhaandsvisAsync(utkast, 2026);
        Assert.Equal(new[] { 2026, 2027, 2029, 2030 }, resultat.Where(r => r.ErFeil).Select(r => r.Budsjettaar));
        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(resultat, r => !r.ErFeil).Dato);
        Assert.All(resultat.Where(r => r.ErFeil), r => { Assert.Null(r.Dato); Assert.Contains("Ugyldig måned/dag", r.Feil); });
        var id = await Mal.OpprettAsync(utkast);
        Assert.Equal(utkast.Regelparametre, (await Mal.HentForRedigeringAsync(id))!.Regelparametre);
    }

    [Theory]
    [InlineData(Regeltype.FastDato, false, int.MinValue)]
    [InlineData(Regeltype.FastDato, false, int.MaxValue)]
    [InlineData(Regeltype.RelativUkedag, false, int.MinValue)]
    [InlineData(Regeltype.RelativUkedag, false, int.MaxValue)]
    [InlineData(Regeltype.RelativUkedag, true, int.MinValue)]
    [InlineData(Regeltype.RelativUkedag, true, int.MaxValue)]
    [InlineData(Regeltype.RelativTilMilepael, false, int.MinValue)]
    [InlineData(Regeltype.RelativTilMilepael, false, int.MaxValue)]
    public async Task Hele_heltallsomraadet_er_lagringsgyldig_med_kontrollerte_previewfeil(Regeltype type, bool fraDag, int offset)
    {
        await Mal.OpprettAsync(Regel("anker", Regeltype.FastDato, """{"maaned":3,"dag":20}"""));
        var json = type switch
        {
            Regeltype.FastDato => $$"""{"maaned":1,"dag":1,"aar_forskyvning":{{offset}}}""",
            Regeltype.RelativUkedag when fraDag => $$"""{"maaned":7,"fra_dag":20,"ukedag":"man","aar_forskyvning":{{offset}}}""",
            Regeltype.RelativUkedag => $$"""{"maaned":3,"uke":2,"ukedag":"man","aar_forskyvning":{{offset}}}""",
            _ => $$"""{"anker_loep":"anker","offset_dager":{{offset}}}"""
        };
        var utkast = Regel("grense", type, json);
        var resultat = await Mal.ForhaandsvisAsync(utkast, 2026);
        Assert.Equal(5, resultat.Count);
        Assert.All(resultat, r => { Assert.True(r.ErFeil); Assert.Null(r.Dato); Assert.NotEmpty(r.Feil!); });
        var id = await Mal.OpprettAsync(utkast);
        Assert.Equal(json, (await Mal.HentForRedigeringAsync(id))!.Regelparametre);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9995)]
    public async Task Femaarig_visningsvindu_taales_ved_begge_datogrenser(int start)
    {
        var resultat = await Mal.ForhaandsvisAsync(Regel("dato", Regeltype.FastDato, """{"maaned":1,"dag":1}"""), start);
        Assert.Equal(5, resultat.Count);
        Assert.Equal(start, resultat[0].Budsjettaar);
        Assert.Equal(start + 4, resultat[^1].Budsjettaar);
        Assert.All(resultat, r => Assert.False(r.ErFeil));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9996)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task Ugyldig_previewvindu_avvises_uten_mutasjon(int start)
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.ForhaandsvisAsync(Regel("dato", Regeltype.FastDato, """{"maaned":1,"dag":1}"""), start));
        Assert.Empty(await Mal.HentAlleAsync());
    }

    [Theory]
    [InlineData("{\"maaned\":2,\"dag\":30}")]
    [InlineData("{\"maaned\":4,\"dag\":31}")]
    [InlineData("{}")]
    [InlineData("{")]
    public async Task Ugyldige_malfelt_avvises_foer_preview_og_lagring(string json)
    {
        var utkast = Regel("ugyldig", Regeltype.FastDato, json);
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.ForhaandsvisAsync(utkast, 2026));
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.OpprettAsync(utkast));
        Assert.Empty(await Mal.HentAlleAsync());
    }

    [Fact]
    public async Task Redigering_erstatter_eksakt_ID_og_ny_kopi_bevarer_duplikatets_ankerrepresentant()
    {
        var foerste = await Mal.OpprettAsync(Regel("anker", Regeltype.FastDato, """{"maaned":3,"dag":20}"""));
        var andre = await Mal.OpprettAsync(Regel("ANKER", Regeltype.FastDato, """{"maaned":4,"dag":17}"""));
        var kandidat = Regel("nytt-loep", Regeltype.RelativTilMilepael, """{"anker_loep":"anker","offset_dager":7}""");
        var foer = await Mal.HentAlleAsync();

        var redigert = await Mal.ForhaandsvisAsync(kandidat, 2026, andre);
        var kopi = await Mal.ForhaandsvisAsync(kandidat, 2026);

        Assert.Equal(new DateOnly(2026, 3, 27), redigert[0].Dato);
        Assert.Equal(redigert, kopi);
        Assert.Equal(foer, await Mal.HentAlleAsync());
        Assert.Equal("""{"maaned":3,"dag":20}""", (await Mal.HentForRedigeringAsync(foerste))!.Regelparametre);
        Assert.Equal("""{"maaned":4,"dag":17}""", (await Mal.HentForRedigeringAsync(andre))!.Regelparametre);
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.ForhaandsvisAsync(kandidat, 2026, Guid.NewGuid()));
    }

    [Fact]
    public async Task Preview_beholder_genereringens_representant_selv_om_visningssortering_er_annerledes()
    {
        await FasedataBevis.LeggTilHistorikkAsync(_t.Factory);
        await Mal.OpprettAsync(Regel("ux84-anker", Regeltype.FastDato, """{"maaned":3,"dag":20}"""));
        await Mal.OpprettAsync(Regel("UX84-ANKER", Regeltype.FastDato, """{"maaned":4,"dag":17}"""));
        var barn = Regel("ux84-barn", Regeltype.RelativTilMilepael, """{"anker_loep":"ux84-anker","offset_dager":1}""");
        var barnId = await Mal.OpprettAsync(barn);
        var historikk = await FasedataBevis.HentAsync(_t.Factory);
        var resultat = await Mal.ForhaandsvisAsync(barn, 2026, barnId);
        Assert.Equal(new DateOnly(2026, 3, 21), resultat[0].Dato); // Lørdag beholdes relativt.
        Assert.Equal(historikk, await FasedataBevis.HentAsync(_t.Factory));
        var generering = new GenereringsTjeneste(_t.Factory, new Synlighetsregel(Options.Create(new SynlighetsregelOpsjoner())));
        await generering.GenererAsync(new(2026));
        var generert = await generering.HentGenererteAsync(2026);
        Assert.Equal(resultat[0].Dato, Assert.Single(generert, r => r.Loep == "ux84-barn").Dato);
        Assert.Equal(2, generert.Count(r => string.Equals(r.Loep, "ux84-anker", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Valgaar_bruker_kalenderaaret_og_presisjon_arves_gjennom_to_ankre()
    {
        await Mal.OpprettAsync(Regel("valganker", Regeltype.FastDato, """{"maaned":9,"dag":20,"aar_forskyvning":-1}""", true));
        await Mal.OpprettAsync(Regel("mellomledd", Regeltype.RelativTilMilepael, """{"anker_loep":"valganker","offset_dager":0}"""));
        var resultat = await Mal.ForhaandsvisAsync(Regel("barn", Regeltype.RelativTilMilepael, """{"anker_loep":"mellomledd","offset_dager":1}"""), 2026);
        Assert.Equal(Datopresisjon.Maaned, resultat[0].Presisjon);
        Assert.True(resultat[0].Tentativ);
        Assert.Equal(2025, resultat[0].Dato!.Value.Year);
        Assert.Equal(Datopresisjon.Dag, resultat[2].Presisjon);
        Assert.False(resultat[2].Tentativ);
        Assert.Equal(new DateOnly(2027, 9, 21), resultat[2].Dato);
        var eget = await Mal.ForhaandsvisAsync(Regel("eget", Regeltype.FastDato, """{"maaned":9,"dag":20,"aar_forskyvning":-1}""", true), 2026);
        Assert.True(eget[0].Valgaarsflagg);
        Assert.True(eget[2].Valgaarsflagg);
        Assert.Equal(Datopresisjon.Dag, eget[2].Presisjon);
    }

    [Fact]
    public async Task Manuelt_manglende_og_sirkulaert_anker_viser_forklaring_og_kan_lagres_uten_gjettet_dato()
    {
        var manuelt = Regel("manuelt", Regeltype.RelativTilMilepael, """{"anker_loep":"fastsettes-manuelt","offset_dager":0}""");
        Assert.All(await Mal.ForhaandsvisAsync(manuelt, 2026), r => { Assert.Null(r.Dato); Assert.Contains("Mangler anker", r.Feil); });
        var id = await Mal.OpprettAsync(manuelt);
        Assert.Equal(manuelt.Regelparametre, (await Mal.HentForRedigeringAsync(id))!.Regelparametre);
        await Mal.OpprettAsync(Regel("sirkel-a", Regeltype.RelativTilMilepael, """{"anker_loep":"sirkel-b","offset_dager":0}"""));
        var sirkel = Regel("sirkel-b", Regeltype.RelativTilMilepael, """{"anker_loep":"sirkel-a","offset_dager":1}""");
        Assert.All(await Mal.ForhaandsvisAsync(sirkel, 2026), r => { Assert.Null(r.Dato); Assert.Contains("Sirkulær", r.Feil); });
    }

    private static MalRegelInndata Regel(string loep, Regeltype type, string json, bool sensitiv = false) => new()
    {
        Loep = loep, Tittel = "Prøve " + loep, Regeltype = type, Regelparametre = json, Valgaarssensitiv = sensitiv
    };

    public void Dispose() => _t.Dispose();
}
