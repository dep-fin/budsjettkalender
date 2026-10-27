using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;

namespace Aarshjul.Tests;

// Eksisterende IFristlesing-grense: uavhengig av kalenderpresentasjonens eksplisitte tomvalg.
public sealed class KalenderutvalgTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private readonly Synlighetskontekst _leser = new(false, ["FAG"]);
    private IFristlesing Lesing => new FristTjeneste(_t.Factory, new FastKlokke(new(2026, 1, 1)));

    public KalenderutvalgTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();
        LeggTil("2026 budsjett", 2026, Kategori.Budsjett, new(2025, 11, 15));
        LeggTil("2027 budsjett", 2027, Kategori.Budsjett, new(2025, 11, 15));
        LeggTil("2028 budsjett", 2028, Kategori.Budsjett, new(2025, 11, 15));
        LeggTil("2028 gul bok", 2028, Kategori.Gulbok, new(2025, 11, 15));
        LeggTil("2026 annen måned", 2026, Kategori.Budsjett, new(2025, 12, 15));
        LeggTil("Skjult POL", 2042, Kategori.Budsjett, new(2025, 11, 15), "POL");
        LeggTil("Upublisert", 2043, Kategori.Budsjett, new(2025, 11, 15), status: FristStatus.Forslag);
        _t.Db.SaveChanges();
    }

    private void LeggTil(string tittel, int aar, Kategori kategori, DateOnly dato, string gruppe = "FAG", FristStatus status = FristStatus.Godkjent)
    {
        var frist = new Frist { Id = Guid.NewGuid(), Tittel = tittel, Budsjettaar = aar, Kategori = kategori, Dato = dato, Status = status };
        frist.Synlighet.Add(new() { GruppeKode = gruppe });
        _t.Db.Frister.Add(frist);
    }

    [Fact]
    public async Task Ikke_sammenhengende_aar_gir_eksakt_union_i_snitt_med_kategori_periode_og_synlighet()
    {
        var frister = await Lesing.HentSynligeAsync(_leser, new()
        {
            Budsjettaar = [2026, 2028, 2042, 2043], Kategorier = [Kategori.Budsjett],
            FraDato = new(2025, 11, 1), TilDato = new(2025, 11, 30), InkluderHistorikk = true
        });
        Assert.Equal(new[] { "2026 budsjett", "2028 budsjett" }, frister.Select(f => f.Tittel));
    }

    [Fact]
    public async Task Aarsvalg_med_historikk_avsloerer_ikke_skjulte_eller_upubliserte_aar()
    {
        Assert.Equal(new[] { 2026, 2027, 2028 }, await Lesing.HentTilgjengeligeBudsjettaarAsync(_leser, inkluderHistorikk: true));
        Assert.Empty(await Lesing.HentTilgjengeligeBudsjettaarAsync(_leser, inkluderHistorikk: false));
        await Lesing.HentSynligeAsync(_leser, new() { Budsjettaar = [2028], Kategorier = [Kategori.Gulbok], InkluderHistorikk = true });
        Assert.Equal(new[] { 2026, 2027, 2028 }, await Lesing.HentTilgjengeligeBudsjettaarAsync(_leser, inkluderHistorikk: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Andre_lesekonsumenter_beholder_null_eller_tomme_samlinger_som_alle(bool tommeSamlinger)
    {
        var frister = await Lesing.HentSynligeAsync(_leser, new()
        {
            Budsjettaar = tommeSamlinger ? [] : null, Kategorier = tommeSamlinger ? [] : null, InkluderHistorikk = true
        });
        Assert.Equal(new[] { "2026 annen måned", "2026 budsjett", "2027 budsjett", "2028 budsjett", "2028 gul bok" }, frister.Select(f => f.Tittel).Order());
    }

    public void Dispose() => _t.Dispose();
}
