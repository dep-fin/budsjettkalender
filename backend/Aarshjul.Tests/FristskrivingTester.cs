using Aarshjul.Application;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Grupper;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>Verifiserer manuell innlegging: synlighet må velges aktivt, og POL settes aldri automatisk.</summary>
public class FristskrivingTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public FristskrivingTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        _t.Db.SaveChanges();
    }

    private FristskrivingTjeneste Tjeneste() => new(_t.Factory);

    private static FristInndata GyldigInndata(params string[] koder) => new()
    {
        Tittel = "Rammefordeling",
        Dato = new DateOnly(2027, 3, 15),
        Budsjettaar = 2028,
        Kategori = Kategori.Budsjett,
        Synlighetskoder = koder.ToList()
    };

    [Fact]
    public async Task Lagring_uten_synlighet_avvises()
    {
        var inndata = GyldigInndata(); // ingen grupper valgt

        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().OpprettAsync(inndata));
    }

    [Fact]
    public async Task Oppretter_frist_med_valgt_synlighet()
    {
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "FAG"));

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(FristStatus.Godkjent, frist.Status);
        Assert.Equal(Opphav.Admin, frist.Opphav);
        Assert.Equal(["FA", "FAG"], frist.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));
    }

    [Fact]
    public async Task Pol_lagres_kun_nar_den_er_eksplisitt_valgt()
    {
        var utenPol = await Tjeneste().OpprettAsync(GyldigInndata("FA"));
        var medPol = await Tjeneste().OpprettAsync(GyldigInndata("FA", "POL"));

        var f1 = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == utenPol);
        var f2 = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == medPol);

        Assert.DoesNotContain("POL", f1.Synlighet.Select(s => s.GruppeKode));
        Assert.Contains("POL", f2.Synlighet.Select(s => s.GruppeKode));
    }

    [Fact]
    public async Task Ukjent_gruppekode_avvises()
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste().OpprettAsync(GyldigInndata("FINNES-IKKE")));
    }

    [Fact]
    public async Task Oppdatering_som_beholder_en_eksisterende_gruppe_kaster_ikke()
    {
        // Vanligste redigering: behold FA, bytt FAG -> POL. Tidligere «clear + re-add» kastet her.
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "FAG"));

        await Tjeneste().OppdaterAsync(id, GyldigInndata("FA", "POL"));

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(["FA", "POL"], frist.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k));
    }

    [Fact]
    public async Task Innholdsredigering_bevarer_eksisterende_inaktiv_synlighet()
    {
        var grupper = new Gruppetjeneste(_t.Factory);
        await grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        IFristskriving skriving = Tjeneste();
        var id = await skriving.OpprettAsync(GyldigInndata("SKOK"));
        await grupper.OppdaterAsync("SKOK", "Skatteøkonomisk avdeling", aktiv: false);

        var inndata = (await skriving.HentForRedigeringAsync(id))!;
        inndata.Tittel = "Endret innhold";
        await skriving.OppdaterAsync(id, inndata);

        var lagret = (await skriving.HentForRedigeringAsync(id))!;
        Assert.Equal("Endret innhold", lagret.Tittel);
        Assert.Equal(["SKOK"], lagret.Synlighetskoder);
        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)));
        Assert.Equal("Endret innhold", (await lesing.HentEnAsync(new Synlighetskontekst(false, ["SKOK"]), id))!.Tittel);
        Assert.Null(await lesing.HentEnAsync(new Synlighetskontekst(false, ["FA"]), id));
    }

    [Fact]
    public async Task Inaktiv_tilordning_kan_fjernes_men_ikke_legges_til_eller_gjeninnfores()
    {
        var grupper = new Gruppetjeneste(_t.Factory);
        await grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        IFristskriving skriving = Tjeneste();
        var id = await skriving.OpprettAsync(GyldigInndata("SKOK"));
        var annenId = await skriving.OpprettAsync(GyldigInndata("FA"));
        await grupper.OppdaterAsync("SKOK", "Skatteøkonomisk avdeling", aktiv: false);

        await Assert.ThrowsAsync<Valideringsfeil>(() => skriving.OpprettAsync(GyldigInndata("SKOK")));
        await Assert.ThrowsAsync<Valideringsfeil>(() => skriving.OppdaterAsync(annenId, GyldigInndata("FA", "SKOK")));
        await skriving.OppdaterAsync(id, GyldigInndata("FA", "SKOK"));
        Assert.Equal(["FA", "SKOK"], (await skriving.HentForRedigeringAsync(id))!.Synlighetskoder.OrderBy(k => k));
        await skriving.OppdaterAsync(id, GyldigInndata("FA"));
        await Assert.ThrowsAsync<Valideringsfeil>(() => skriving.OppdaterAsync(id, GyldigInndata("FA", "SKOK")));
        Assert.Equal(["FA"], (await skriving.HentForRedigeringAsync(id))!.Synlighetskoder);
        Assert.Equal(["FA"], (await skriving.HentForRedigeringAsync(annenId))!.Synlighetskoder);
    }

    [Fact]
    public async Task Tilgjengelige_budsjettaar_paavirkes_ikke_av_aarsfilter()
    {
        await Tjeneste().OpprettAsync(MedAar(2027, "FA"));
        await Tjeneste().OpprettAsync(MedAar(2028, "FA"));

        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)));
        var aar = await lesing.HentTilgjengeligeBudsjettaarAsync(new Synlighetskontekst(false, ["FA"]), inkluderHistorikk: true);

        Assert.Equal([2027, 2028], aar);
    }

    private static FristInndata MedAar(int aar, params string[] koder)
    {
        var inndata = GyldigInndata(koder);
        inndata.Budsjettaar = aar;
        return inndata;
    }

    [Fact]
    public async Task Opprett_og_flytt_budsjettaar_bevarer_en_identitet_og_verdier_i_alle_leseutvalg()
    {
        IFristskriving skriving = Tjeneste();
        var inndata = MedAar(2031, "FA", "POL");
        inndata.Tittel = "Arbeidsår før redigering";
        inndata.Dato = new DateOnly(2030, 11, 20);
        var id = await skriving.OpprettAsync(inndata);
        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2030, 1, 1)));
        var admin = new Synlighetskontekst(true, []);
        Assert.Equal(id, Assert.Single(await lesing.HentSynligeAsync(admin,
            new FristFilter { Budsjettaar = [2031], InkluderHistorikk = true })).Id);

        var endret = (await skriving.HentForRedigeringAsync(id))!;
        endret.Tittel = "Samme frist etter flytting";
        endret.Budsjettaar = 2032;
        endret.Datopresisjon = Datopresisjon.Maaned;
        endret.Datokvalifikator = Datokvalifikator.Ultimo;
        endret.Notat = "Bevart mellom register, kalender og budsjettår";
        await skriving.OppdaterAsync(id, endret);

        Assert.Empty(await lesing.HentSynligeAsync(admin, new FristFilter { Budsjettaar = [2031], InkluderHistorikk = true }));
        foreach (var filter in new[]
        {
            new FristFilter { Budsjettaar = [2032], InkluderHistorikk = true },
            new FristFilter { FraDato = new DateOnly(2030, 11, 1), TilDato = new DateOnly(2030, 11, 30), InkluderHistorikk = true },
            new FristFilter { InkluderHistorikk = true }
        })
        {
            var frist = Assert.Single(await lesing.HentSynligeAsync(admin, filter));
            Assert.Equal(id, frist.Id);
            Assert.Equal("Samme frist etter flytting", frist.Tittel);
            Assert.Equal(2032, frist.Budsjettaar);
            Assert.Equal(new DateOnly(2030, 11, 20), frist.Dato);
            Assert.Equal(Datopresisjon.Maaned, frist.Datopresisjon);
            Assert.Equal(Datokvalifikator.Ultimo, frist.Datokvalifikator);
            Assert.Equal(endret.Notat, frist.Notat);
            Assert.Equal(["FA", "POL"], frist.Synlighet.Order());
        }
    }

    [Fact]
    public async Task Oppdatering_erstatter_synlighet_og_beregner_sorteringsdag()
    {
        var id = await Tjeneste().OpprettAsync(GyldigInndata("FA", "POL"));

        var endret = GyldigInndata("FAG");
        endret.Datopresisjon = Datopresisjon.Maaned;
        endret.Datokvalifikator = Datokvalifikator.Ultimo;
        endret.Dato = new DateOnly(2027, 2, 10);
        await Tjeneste().OppdaterAsync(id, endret);

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == id);
        Assert.Equal(["FAG"], frist.Synlighet.Select(s => s.GruppeKode));
        Assert.Equal(new DateOnly(2027, 2, 28), frist.Sorteringsdag);
    }

    public void Dispose() => _t.Dispose();
}
