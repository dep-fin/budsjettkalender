using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Grupper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Aarshjul.Tests;

public sealed class GruppetjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IGruppetjeneste Grupper => new Gruppetjeneste(_t.Factory);

    public GruppetjenesteTester()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        _t.Db.SaveChanges();
    }

    [Fact]
    public async Task Ny_egendefinert_gruppe_kan_velges_og_gir_bare_sitt_publikum_lagrede_frister()
    {
        await Grupper.OpprettAsync(" skok ", " Skatteøkonomisk avdeling ");

        var gruppe = Assert.Single(await Grupper.HentAktiveAsync(), g => g.Kode == "SKOK");
        Assert.Equal("Skatteøkonomisk avdeling", gruppe.Navn);
        Assert.False(gruppe.ErStandard);
        await new FristskrivingTjeneste(_t.Factory).OpprettAsync(Frist("SKOK"));
        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)));
        var filter = new FristFilter { InkluderHistorikk = true };
        var synlig = Assert.Single(await lesing.HentSynligeAsync(new Synlighetskontekst(false, ["SKOK"]), filter));
        Assert.Equal("Ny avdelingsfrist", synlig.Tittel);
        Assert.Equal(["SKOK"], synlig.Synlighet);
        Assert.Empty(await lesing.HentSynligeAsync(new Synlighetskontekst(false, ["FA"]), filter));
        Assert.Empty(await lesing.HentSynligeAsync(new Synlighetskontekst(false, ["POL"]), filter));
    }

    [Fact]
    public async Task Navneendring_og_deaktivering_beholder_koden_og_historiske_fristreferanser()
    {
        await Grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        var id = await new FristskrivingTjeneste(_t.Factory).OpprettAsync(Frist("SKOK"));

        await Grupper.OppdaterAsync("SKOK", " Nytt avdelingsnavn ", aktiv: false);

        Assert.Equal(new GruppeadministrasjonDto("SKOK", "Nytt avdelingsnavn", false, false),
            Assert.Single(await Grupper.HentAlleAsync(), g => g.Kode == "SKOK"));
        Assert.DoesNotContain(await Grupper.HentAktiveAsync(), g => g.Kode == "SKOK");
        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)));
        var lagret = await lesing.HentEnAsync(new Synlighetskontekst(false, ["SKOK"]), id);
        Assert.NotNull(lagret);
        Assert.Equal(["SKOK"], lagret.Synlighet);
        await Assert.ThrowsAsync<Valideringsfeil>(() => new FristskrivingTjeneste(_t.Factory).OpprettAsync(Frist("SKOK")));

        await Grupper.OppdaterAsync("SKOK", "Nytt avdelingsnavn", aktiv: true);
        Assert.Single(await Grupper.HentAktiveAsync(), g => g.Kode == "SKOK");
    }

    [Fact]
    public async Task Deaktivering_bevarer_Entra_medlemskap_og_tilgang_naar_claim_fortsatt_gjelder()
    {
        await Grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        var id = await new FristskrivingTjeneste(_t.Factory).OpprettAsync(Frist("SKOK"));
        IBrukeroppslag oppslag = new BrukeroppslagTjeneste(_t.Factory,
            Options.Create(new EntraGruppeOpsjoner { AttributtTilGruppe = new() { ["entra-skok"] = "SKOK" } }),
            Options.Create(new AdministratortilgangOpsjoner()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "ansatt"), new Claim("groups", "entra-skok")]));
        Assert.Equal(["SKOK"], (await oppslag.HentEllerOpprettAsync(principal)).Grupper);

        await Grupper.OppdaterAsync("SKOK", "Skatteøkonomisk avdeling", aktiv: false);

        var bruker = await oppslag.HentEllerOpprettAsync(principal);
        Assert.Equal(["SKOK"], bruker.Grupper);
        Assert.Equal(["SKOK"], (await oppslag.HentEllerOpprettAsync(principal)).Grupper);
        await using var db = await _t.Factory.CreateDbContextAsync();
        Assert.Equal("SKOK", (await db.Brukere.Include(b => b.Grupper).SingleAsync(b => b.Id == "ansatt"))
            .Grupper.Single().GruppeKode);
        var lesing = new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)));
        Assert.NotNull(await lesing.HentEnAsync(new Synlighetskontekst(bruker.ErAdministrator, bruker.Grupper), id));
    }

    [Fact]
    public async Task Inaktiv_gruppe_gir_ikke_nytt_Entra_medlemskap_og_bortfalt_claim_fjerner_gammelt()
    {
        await Grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        IBrukeroppslag oppslag = new BrukeroppslagTjeneste(_t.Factory,
            Options.Create(new EntraGruppeOpsjoner { AttributtTilGruppe = new() { ["entra-skok"] = "SKOK", ["entra-pol"] = "POL" } }),
            Options.Create(new AdministratortilgangOpsjoner()));
        var claims = new[] { new Claim("groups", "entra-skok"), new Claim("groups", "entra-pol") };
        var ansatt = new ClaimsPrincipal(new ClaimsIdentity(claims.Append(new Claim("oid", "ansatt"))));
        Assert.Equal(["SKOK"], (await oppslag.HentEllerOpprettAsync(ansatt)).Grupper);
        await Grupper.OppdaterAsync("SKOK", "Skatteøkonomisk avdeling", aktiv: false);

        var nyAnsatt = new ClaimsPrincipal(new ClaimsIdentity(claims.Append(new Claim("oid", "ny-ansatt"))));
        Assert.Empty((await oppslag.HentEllerOpprettAsync(nyAnsatt)).Grupper);
        var utenClaim = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "ansatt")]));
        Assert.Empty((await oppslag.HentEllerOpprettAsync(utenClaim)).Grupper);
        Assert.Empty((await oppslag.HentEllerOpprettAsync(ansatt)).Grupper);
        await using var db = await _t.Factory.CreateDbContextAsync();
        Assert.Empty(await db.BrukerGrupper.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1SKOK")]
    [InlineData("SK OK")]
    [InlineData("SKØK")]
    [InlineData("SKOK/ADMIN")]
    [InlineData("SKOK\nPOL")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLM")]
    public async Task Ugyldig_kode_avvises_uten_ny_gruppe(string? kode)
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OpprettAsync(kode!, "Avdeling"));
        Assert.Equal(4, (await Grupper.HentAktiveAsync()).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Tomt_navn_avvises_ved_oppretting_og_endring(string? navn)
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OpprettAsync("SKOK", navn!));
        await Grupper.OpprettAsync("SKOK", "Uendret");
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OppdaterAsync("SKOK", navn!, aktiv: false));
        Assert.Equal(new GruppeadministrasjonDto("SKOK", "Uendret", true, false),
            Assert.Single(await Grupper.HentAlleAsync(), g => g.Kode == "SKOK"));
    }

    [Fact]
    public async Task Navn_over_lagringsgrensen_avvises_og_maksimale_lengder_kan_lagres()
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OpprettAsync("SKOK", new string('N', 257)));
        var kode = "S" + new string('K', 63);
        await Grupper.OpprettAsync(kode, new string('N', 256));
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OppdaterAsync(kode, new string('N', 257), aktiv: false));
        Assert.Equal(256, Assert.Single(await Grupper.HentAktiveAsync(), g => g.Kode == kode).Navn.Length);
    }

    [Theory]
    [InlineData("fa")]
    [InlineData(" FIN-FAG ")]
    [InlineData("FAG")]
    [InlineData("POL")]
    [InlineData("skok")]
    public async Task Duplisert_kode_avvises_og_beholder_ogsa_inaktiv_gruppes_identitet(string kode)
    {
        await Grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        await Grupper.OppdaterAsync("SKOK", "Skatteøkonomisk avdeling", aktiv: false);
        var foer = await Grupper.HentAlleAsync();
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OpprettAsync(kode, "Erstatter"));
        Assert.Equal(foer, await Grupper.HentAlleAsync());
    }

    [Fact]
    public async Task Oppdatering_av_ukjent_kode_oppretter_ikke_en_erstatning()
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OppdaterAsync("NY-KODE", "Avdeling", aktiv: true));
        Assert.Equal(new[] { "FA", "FAG", "FIN-FAG", "POL" },
            (await Grupper.HentAlleAsync()).Select(g => g.Kode).OrderBy(k => k).ToArray());
        Assert.All(await Grupper.HentAlleAsync(), g => Assert.True(g.ErStandard));
    }

    [Fact]
    public async Task Innebygd_gruppe_beholder_kode_og_standardidentitet_ved_vedlikehold()
    {
        await Grupper.OppdaterAsync("POL", "Politisk ledelse – nytt navn", aktiv: false);
        Assert.Equal(new GruppeadministrasjonDto("POL", "Politisk ledelse – nytt navn", false, true),
            Assert.Single(await Grupper.HentAlleAsync(), g => g.Kode == "POL"));
        await Assert.ThrowsAsync<Valideringsfeil>(() => Grupper.OpprettAsync("pol", "Erstatning"));
        await Grupper.OppdaterAsync("POL", "Politisk ledelse", aktiv: true);
        Assert.Equal(4, (await Grupper.HentAlleAsync()).Count);
        Assert.All(await Grupper.HentAlleAsync(), g => Assert.True(g.ErStandard));
    }

    private static FristInndata Frist(params string[] koder) => new()
    {
        Tittel = "Ny avdelingsfrist", Dato = new DateOnly(2030, 11, 20), Budsjettaar = 2031,
        Kategori = Kategori.Budsjett, Synlighetskoder = koder.ToList()
    };

    [Theory]
    [InlineData("FA")]
    [InlineData("FIN-FAG")]
    [InlineData("FAG")]
    [InlineData("POL")]
    [InlineData("SKOK")]
    public async Task Navn_og_aktivitet_bevarer_alle_identiteter_historiske_delinger_og_medlemskapskilder(string kode)
    {
        await Grupper.OpprettAsync("SKOK", "Egendefinert");
        var skriving = new FristskrivingTjeneste(_t.Factory);
        foreach (var aar in new[] { 2020, 2031 })
        {
            var inndata = Frist(kode);
            inndata.Dato = new DateOnly(aar - 1, 11, 20);
            inndata.Budsjettaar = aar;
            await skriving.OpprettAsync(inndata);
        }
        await using (var db = await _t.Factory.CreateDbContextAsync())
        {
            db.Brukere.AddRange(
                new Bruker { Id = "entra", Navn = "Entra", ErFin = true, Grupper = [new() { BrukerId = "entra", GruppeKode = kode, Kilde = GruppeMedlemskapKilde.Entra }] },
                new Bruker { Id = "manuell", Navn = "Manuell", ErFin = true, Grupper = [new() { BrukerId = "manuell", GruppeKode = kode, Kilde = GruppeMedlemskapKilde.Manuell }] });
            await db.SaveChangesAsync();
        }
        await using var foerDb = await _t.Factory.CreateDbContextAsync();
        var grupper = await foerDb.Synlighetsgrupper.AsNoTracking().OrderBy(g => g.Kode)
            .Select(g => new { g.Id, g.Kode, g.ErStandard }).ToArrayAsync();
        var delinger = await foerDb.FristSynlighet.AsNoTracking().OrderBy(s => s.FristId).ThenBy(s => s.GruppeKode)
            .Select(s => new { s.FristId, s.GruppeKode }).ToArrayAsync();
        var medlemskap = await foerDb.BrukerGrupper.AsNoTracking().OrderBy(b => b.BrukerId).ThenBy(b => b.GruppeKode)
            .Select(b => new { b.BrukerId, b.GruppeKode, b.Kilde }).ToArrayAsync();
        foreach (var aktiv in new[] { false, true })
        {
            await Grupper.OppdaterAsync(kode, " Nytt navn <bevart> ", aktiv);
            Assert.Equal(aktiv, (await Grupper.HentAktiveAsync()).Any(g => g.Kode == kode));
            await using var etter = await _t.Factory.CreateDbContextAsync();
            Assert.Equal(grupper, await etter.Synlighetsgrupper.OrderBy(g => g.Kode).Select(g => new { g.Id, g.Kode, g.ErStandard }).ToArrayAsync());
            Assert.Equal(delinger, await etter.FristSynlighet.OrderBy(s => s.FristId).ThenBy(s => s.GruppeKode).Select(s => new { s.FristId, s.GruppeKode }).ToArrayAsync());
            Assert.Equal(medlemskap, await etter.BrukerGrupper.OrderBy(b => b.BrukerId).ThenBy(b => b.GruppeKode).Select(b => new { b.BrukerId, b.GruppeKode, b.Kilde }).ToArrayAsync());
            var synlige = await new FristTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 1, 1)))
                .HentSynligeAsync(Synlighetskontekst.ForGruppe(kode), new() { InkluderHistorikk = true });
            Assert.Equal(2, synlige.Count);
            Assert.Equal(new[] { 2020, 2031 }, synlige.Select(f => f.Budsjettaar));
            Assert.All(synlige, f => Assert.Equal(new[] { kode }, f.Synlighet));
        }
    }

    public void Dispose() => _t.Dispose();
}
