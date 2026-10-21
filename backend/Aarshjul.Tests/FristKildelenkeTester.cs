using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Frister;

namespace Aarshjul.Tests;

public sealed class FristKildelenkeTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2026, 1, 1);
    private static readonly Synlighetskontekst Fa = new(false, ["FA"]);
    private IFristlesing Lesing => new FristTjeneste(_t.Factory, new FastKlokke(Idag));

    public FristKildelenkeTester()
    {
        foreach (var kode in new[] { "FA", "POL" })
            _t.Db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = kode, Navn = kode });
        _t.Db.SaveChanges();
    }

    [Fact]
    public async Task Synlig_frist_faar_URL_fra_sitt_tilknyttede_dokument()
    {
        var dokument = Dokument("https://www.regjeringen.no/no/syntetisk-kilde/id69/");
        Dokument("https://regjeringen.no/no/annet-dokument/");
        var frist = Frist(dokument.Id);
        await _t.Db.SaveChangesAsync();

        var resultat = Assert.Single(await Lesing.HentSynligeAsync(Fa, new()));

        Assert.Equal(frist.Id, resultat.Id);
        Assert.Equal("https://www.regjeringen.no/no/syntetisk-kilde/id69/", resultat.KildeUrl);
    }

    [Theory]
    [InlineData("http://regjeringen.no/kilde")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/no/kilde")]
    [InlineData("//regjeringen.no/no/kilde")]
    [InlineData("C:\\dokumenter\\lokal.pdf")]
    [InlineData("\\\\server\\deling\\lokal.pdf")]
    [InlineData("javascript:alert(69)")]
    [InlineData("data:text/html,<script>alert(69)</script>")]
    [InlineData("file:///C:/dokumenter/lokal.pdf")]
    [InlineData("ftp://regjeringen.no/kilde")]
    [InlineData("https://example.org/kilde")]
    [InlineData("https://regjeringen.no.example.org/kilde")]
    [InlineData("https://evilregjeringen.no/kilde")]
    [InlineData("https://sub.regjeringen.no/kilde")]
    [InlineData("https://regjeringen.no./kilde")]
    [InlineData("https://127.0.0.1/kilde")]
    [InlineData("https://[::1]/kilde")]
    [InlineData("https://localhost/kilde")]
    [InlineData("https://bruker@regjeringen.no/kilde")]
    [InlineData("https://bruker:passord@www.regjeringen.no/kilde")]
    [InlineData("https://regjeringen.no@example.org/kilde")]
    [InlineData("https://@regjeringen.no/kilde")]
    [InlineData("https://regjeringen.no:444/kilde")]
    [InlineData("https://www.regjeringen.no:80/kilde")]
    [InlineData("https://regjeringen.no:ugyldig/kilde")]
    [InlineData("https://")]
    [InlineData("ikke en adresse")]
    [InlineData("https://regjeringen.no/no/\nugyldig")]
    [InlineData("https://regjeringen.no/%zz")]
    public async Task Avvist_URL_beholder_fristen_uten_lenke_i_alle_leseutvalg(string? url)
    {
        var frist = Frist(Dokument(url).Id);
        await _t.Db.SaveChangesAsync();

        var liste = Assert.Single(await Lesing.HentSynligeAsync(Fa, new()));
        var landing = Assert.Single(await Lesing.HentLandingsutvalgAsync(Fa, Idag, new()));
        var enkelt = Assert.IsType<FristDto>(await Lesing.HentEnAsync(Fa, frist.Id));
        foreach (var resultat in new[] { liste, landing, enkelt })
        {
            Assert.Equal(frist.Id, resultat.Id);
            Assert.Null(resultat.KildeUrl);
        }
    }

    [Theory]
    [InlineData("https://regjeringen.no/no/kilde", "https://regjeringen.no/no/kilde")]
    [InlineData("https://www.regjeringen.no/no/kilde", "https://www.regjeringen.no/no/kilde")]
    [InlineData("HTTPS://ReGjeRinGeN.No/no/Kilde", "https://regjeringen.no/no/Kilde")]
    [InlineData("https://WWW.REGJERINGEN.NO/no/Kilde", "https://www.regjeringen.no/no/Kilde")]
    [InlineData("https://regjeringen.no:443/no/kilde", "https://regjeringen.no/no/kilde")]
    [InlineData("https://www.regjeringen.no/no/kilde?a=1&tekst=%22%3E%3Cscript%3E#avsnitt", "https://www.regjeringen.no/no/kilde?a=1&tekst=%22%3E%3Cscript%3E#avsnitt")]
    public async Task Tillatt_URL_returneres_i_liste_landing_og_enkeltoppslag(string url, string forventet)
    {
        var frist = Frist(Dokument(url).Id);
        await _t.Db.SaveChangesAsync();

        Assert.Equal(forventet, Assert.Single(await Lesing.HentSynligeAsync(Fa, new())).KildeUrl);
        Assert.Equal(forventet, Assert.Single(await Lesing.HentLandingsutvalgAsync(Fa, Idag, new())).KildeUrl);
        Assert.Equal(forventet, (await Lesing.HentEnAsync(Fa, frist.Id))!.KildeUrl);
    }

    [Fact]
    public async Task Frister_uten_dokumentrad_eller_kobling_beholdes_uten_konstruert_URL()
    {
        var manuell = Frist(null, opphav: Opphav.Admin);
        var generert = Frist(null);
        generert.GjentaRegelId = Guid.NewGuid();
        var foreldrelos = Frist(Guid.NewGuid());
        var lokalPdf = Frist(Dokument(null).Id);
        await _t.Db.SaveChangesAsync();

        Assert.Equal(4, (await Lesing.HentSynligeAsync(Fa, new())).Count);
        Assert.Equal(4, (await Lesing.HentLandingsutvalgAsync(Fa, Idag, new())).Count);
        foreach (var frist in new[] { manuell, generert, foreldrelos, lokalPdf })
        {
            Assert.Null((await Lesing.HentEnAsync(Fa, frist.Id))!.KildeUrl);
            Assert.Null((await Lesing.HentSynligeAsync(Fa, new())).Single(f => f.Id == frist.Id).KildeUrl);
            Assert.Null((await Lesing.HentLandingsutvalgAsync(Fa, Idag, new())).Single(f => f.Id == frist.Id).KildeUrl);
        }
    }

    [Fact]
    public async Task Dokumentkobling_utvider_ikke_synlighet_eller_publiserer_forslag()
    {
        var pol = Frist(Dokument("https://regjeringen.no/skjult-pol-markor").Id, "POL");
        var upublisert = Frist(Dokument("https://regjeringen.no/upublisert-markor").Id);
        upublisert.Status = FristStatus.Forslag;
        await _t.Db.SaveChangesAsync();

        Assert.Empty(await Lesing.HentSynligeAsync(Fa, new()));
        Assert.Empty(await Lesing.HentLandingsutvalgAsync(Fa, Idag, new()));
        Assert.Null(await Lesing.HentEnAsync(Fa, pol.Id));
        Assert.Null(await Lesing.HentEnAsync(Fa, upublisert.Id));
        var polKontekst = new Synlighetskontekst(false, ["POL"]);
        Assert.Equal("https://regjeringen.no/skjult-pol-markor", Assert.Single(await Lesing.HentSynligeAsync(polKontekst, new())).KildeUrl);
    }

    private BehandletDokument Dokument(string? url)
    {
        var dokument = new BehandletDokument
        {
            Id = Guid.NewGuid(), Kilde = "syntetisk", DokumentNokkel = Guid.NewGuid().ToString(), Url = url
        };
        _t.Db.BehandledeDokumenter.Add(dokument);
        return dokument;
    }

    private Frist Frist(Guid? dokumentId, string gruppe = "FA", Opphav opphav = Opphav.Robot)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = "Syntetisk frist", Dato = new(2026, 1, 10),
            Budsjettaar = 2027, DokumentId = dokumentId, Kilde = "lokal.pdf", Opphav = opphav,
            Synlighet = [new() { GruppeKode = gruppe }]
        };
        _t.Db.Frister.Add(frist);
        return frist;
    }

    public void Dispose() => _t.Dispose();
}
