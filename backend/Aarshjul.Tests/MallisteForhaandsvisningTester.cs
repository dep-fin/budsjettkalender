using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Testkontrakter;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

public sealed class MallisteForhaandsvisningTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IMaltjeneste Mal => new Maltjeneste(_t.Factory);

    [Fact]
    public async Task Hele_malen_beholder_regelidentitet_duplikatets_genereringsanker_og_alle_lagrede_data()
    {
        await FasedataBevis.LeggTilHistorikkAsync(_t.Factory);
        var a = await Mal.OpprettAsync(Regel("Z første", "liste-anker", "{\"maaned\":3,\"dag\":20}"));
        var b = await Mal.OpprettAsync(Regel("A andre", "LISTE-ANKER", "{\"maaned\":4,\"dag\":17}"));
        var c = await Mal.OpprettAsync(Regel("Barn", "liste-barn", "{\"anker_loep\":\"liste-anker\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        var foer = await FasedataBevis.HentAsync(_t.Factory);

        var snapshot = await Mal.ForhaandsvisAlleAsync(2026);

        Assert.Equal(2026, snapshot.Budsjettaar);
        Assert.Equal(snapshot.Regler.Count, snapshot.Beregninger.Count);
        Assert.Equal(new DateOnly(2026, 3, 20), Resultat(snapshot, a).Dato);
        Assert.Equal(new DateOnly(2026, 4, 17), Resultat(snapshot, b).Dato);
        Assert.Equal(new DateOnly(2026, 3, 21), Resultat(snapshot, c).Dato);
        Assert.Equal(foer, await FasedataBevis.HentAsync(_t.Factory));
        var generering = new GenereringsTjeneste(_t.Factory, new Synlighetsregel(Options.Create(new SynlighetsregelOpsjoner())));
        await generering.GenererAsync(new(2026));
        var genererte = await generering.HentGenererteAsync(2026);
        Assert.Equal(new DateOnly(2026, 3, 21), Assert.Single(genererte, r => r.Loep == "liste-barn").Dato);
        Assert.Equal(2, genererte.Count(r => string.Equals(r.Loep, "liste-anker", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(1, 1, 3, 20)]
    [InlineData(9999, 9999, 3, 19)]
    [InlineData(2026, 2026, 3, 20)]
    public async Task Enkelt_aarsvalg_bruker_hele_datoomraadet_uten_femaarsbegrensning(int aar, int kalenderaar, int maaned, int dag)
    {
        var id = await Mal.OpprettAsync(Regel("Grense", "grense", "{\"maaned\":3,\"dag\":20}"));
        var foer = await Mal.HentAlleAsync();
        var snapshot = await Mal.ForhaandsvisAlleAsync(aar);
        Assert.Equal(new DateOnly(kalenderaar, maaned, dag), Resultat(snapshot, id).Dato);
        Assert.Equal(aar, Resultat(snapshot, id).Budsjettaar);
        Assert.Null(Resultat(snapshot, id).Feil);
        Assert.Equal(foer, await Mal.HentAlleAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task Ugyldig_maalaar_avvises_uten_aa_endre_malen(int aar)
    {
        await Mal.OpprettAsync(Regel("Uendret", "uendret", "{\"maaned\":3,\"dag\":20}"));
        var foer = await Mal.HentAlleAsync();
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.ForhaandsvisAlleAsync(aar));
        Assert.Equal(foer, await Mal.HentAlleAsync());
    }

    [Fact]
    public async Task Arvet_tentativitet_og_kontrollerte_kjedefeil_returneres_per_regel_uten_aa_skjule_uavhengige()
    {
        var a = await Mal.OpprettAsync(Regel("Valg", "valg", "{\"maaned\":9,\"dag\":20,\"aar_forskyvning\":-1}", sensitiv: true));
        var b = await Mal.OpprettAsync(Regel("Barn", "barn", "{\"anker_loep\":\"valg\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        var mangler = await Mal.OpprettAsync(Regel("Mangler", "mangler", "{\"anker_loep\":\"manuelt\",\"offset_dager\":0}", Regeltype.RelativTilMilepael));
        var kjede = await Mal.OpprettAsync(Regel("Kjedefeil", "kjedefeil", "{\"anker_loep\":\"mangler\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        var json = Guid.NewGuid();
        _t.Db.Gjentaksregler.Add(new() { Id = json, Tittel = "Legacy JSON", Loep = "json", Regelparametre = "{" });
        await _t.Db.SaveChangesAsync();
        var foer = await Mal.HentAlleAsync();

        var snapshot = await Mal.ForhaandsvisAlleAsync(2026);

        Assert.Equal(5, snapshot.Regler.Count);
        Assert.Equal(new DateOnly(2025, 9, 19), Resultat(snapshot, a).Dato);
        Assert.Equal(new DateOnly(2025, 9, 20), Resultat(snapshot, b).Dato);
        foreach (var id in new[] { a, b }) { Assert.True(Resultat(snapshot, id).Tentativ); Assert.Equal(Datopresisjon.Maaned, Resultat(snapshot, id).Presisjon); }
        foreach (var id in new[] { mangler, kjede }) { Assert.Null(Resultat(snapshot, id).Dato); Assert.Contains("Mangler anker", Resultat(snapshot, id).Feil); }
        Assert.Null(Resultat(snapshot, json).Dato);
        Assert.NotNull(Resultat(snapshot, json).Feil);
        Assert.Equal(foer, await Mal.HentAlleAsync());
    }

    [Fact]
    public async Task Tom_mal_er_et_gyldig_sideeffektfritt_snapshot()
    {
        var snapshot = await Mal.ForhaandsvisAlleAsync(2026);
        Assert.Equal(2026, snapshot.Budsjettaar);
        Assert.Empty(snapshot.Regler);
        Assert.Empty(snapshot.Beregninger);
    }

    private static MalRegelForhaandsvisning Resultat(MalForhaandsvisning snapshot, Guid id)
        => snapshot.Beregninger[snapshot.Regler.ToList().FindIndex(r => r.Id == id)];
    private static MalRegelInndata Regel(string tittel, string loep, string json, Regeltype type = Regeltype.FastDato, bool sensitiv = false)
        => new() { Tittel = tittel, Loep = loep, Regelparametre = json, Regeltype = type, Valgaarssensitiv = sensitiv };
    public void Dispose() => _t.Dispose();
}
