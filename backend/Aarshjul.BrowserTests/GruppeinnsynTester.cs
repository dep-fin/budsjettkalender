using System.Text.Json;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GruppeinnsynTester
{
    [Fact]
    public Task Faktisk_administrator_ser_gruppens_frister_uten_skrivehandlinger_eller_andre_gruppeetiketter() => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); await p.Velg("FA");
        await Expect(p.Side.GetByText("FA og POL", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(p.Side.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveCountAsync(0);
        await Expect(p.Side.GetByText("POL", new() { Exact = true })).ToHaveCountAsync(0);
        var knapp = p.Side.GetByRole(AriaRole.Button, new() { Name = "FA og POL", Exact = true });
        await knapp.PressAsync("Enter");
        var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = "FA og POL", Exact = true });
        await Expect(dialog).ToBeVisibleAsync(); await RenLesing(dialog);
        await p.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync(); await Expect(knapp).ToBeFocusedAsync();
    });

    [Theory]
    [InlineData("FA", 4)]
    [InlineData("FIN-FAG", 1)]
    [InlineData("FAG", 2)]
    [InlineData("POL", 2)]
    [InlineData("SKOK", 1)]
    public Task Liste_detaljer_og_antall_er_noeyaktig_gruppens_publiserte_leseutvalg(string kode, int antall) => Gruppeprove.Kjor(async p =>
    {
        var forventet = await p.Lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe(kode), new() { InkluderHistorikk = true });
        Assert.Equal(antall, forventet.Count);
        await p.Gaa("/admin/innsyn?aar=2031"); await p.Velg(kode);
        await Expect(p.Rader).ToHaveCountAsync(antall);
        Assert.Equal(forventet.Select(f => f.Id.ToString()), await p.Rader.EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.fristId)"));
        await Expect(p.Resultat.GetByRole(AriaRole.Status)).ToHaveTextAsync($"{antall} publiserte {(antall == 1 ? "frist" : "frister")}");
        foreach (var frist in forventet)
        {
            var rad = p.Rader.Filter(new() { Has = p.Side.GetByRole(AriaRole.Button, new() { Name = frist.Tittel, Exact = true }) });
            await Expect(rad).ToContainTextAsync($"Budsjettår {frist.Budsjettaar}");
            await Expect(rad).ToContainTextAsync(Kategorinavn(frist.Kategori));
            if (frist.Klokkeslett is { } tid) await Expect(rad).ToContainTextAsync(tid.ToString("HH:mm"));
            var knapp = rad.GetByRole(AriaRole.Button, new() { Name = frist.Tittel, Exact = true });
            await knapp.ClickAsync();
            var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = frist.Tittel, Exact = true });
            await Expect(dialog).ToBeVisibleAsync(); await RenLesing(dialog);
            await Expect(dialog).ToContainTextAsync(frist.Budsjettaar.ToString());
            await Expect(dialog).ToContainTextAsync(Kategorinavn(frist.Kategori));
            if (frist.Notat is not null) await Expect(dialog).ToContainTextAsync(frist.Notat);
            if (frist.ErTentativ)
            {
                await Expect(dialog).ToContainTextAsync("medio november 2031");
                await Expect(dialog).ToContainTextAsync("dato ikke fastsatt");
                await Expect(dialog).Not.ToContainTextAsync("20. november");
            }
            if (frist.KildeUrl is not null)
            {
                var kilde = dialog.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") });
                await Expect(kilde).ToHaveAttributeAsync("href", Gruppeprove.KildeUrl);
                await Expect(kilde).ToHaveAttributeAsync("target", "_blank");
                await Expect(kilde).ToHaveAttributeAsync("rel", "noopener noreferrer");
                await Expect(kilde).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
                Assert.True(await dialog.EvaluateAsync<bool>("el => !!(el.querySelector('[aria-label=Kilderundskriv]').compareDocumentPosition(el.querySelector('[aria-label=\"Om fristen\"]')) & Node.DOCUMENT_POSITION_FOLLOWING)"));
                await p.Bilder("innsyn-kildedetaljer");
            }
            else await Expect(dialog.GetByRole(AriaRole.Region, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
            await p.Side.Keyboard.PressAsync("Escape"); await Expect(dialog).Not.ToBeVisibleAsync(); await Expect(knapp).ToBeFocusedAsync();
        }
        var dom = await p.Side.ContentAsync();
        foreach (var skjult in new[] { p.SkjultId.ToString(), p.ForslagId.ToString(), "UX89 skjult publisert", "UX89 skjult forslag", "UX89 hemmelig forslagsnotat", "UX89 hemmelig innsender", "UX89-hemmelig-filnavn.pdf" })
            Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
        foreach (var aar in new[] { "2098", "2099" })
        {
            await Expect(p.Side.Locator("body")).Not.ToContainTextAsync(aar);
            await Expect(p.Side.Locator($"option[value='{aar}']")).ToHaveCountAsync(0);
        }
        Assert.All(p.Kontroll.Lesinger, l => { Assert.False(l.SerAlt); Assert.Equal(new[] { kode }, l.Grupper); Assert.True(l.Historikk); });
    });

    [Fact]
    public Task Aapne_forslag_endrer_verken_publisert_liste_detaljer_telling_eller_tomt_gruppeinnsyn() => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); await p.Velg("FA");
        var foer = await p.Resultat.TextContentAsync();
        var data = JsonSerializer.Serialize(await p.Lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe("FA"), new() { InkluderHistorikk = true }));
        await using (var db = await p.Fabrikk.CreateDbContextAsync())
        {
            var original = await db.Frister.SingleAsync(f => f.Tittel == "Kun FA");
            db.Forslag.Add(new Forslag { Id = Guid.NewGuid(), Tittel = "UX89 sensitiv endring", Notat = "UX89 sensitivt notat", KildeEllerInnsender = "UX89 sensitiv innsender",
                EndrerFristId = original.Id, ForslagType = ForslagType.Endring, Budsjettaar = 2099 });
            await db.SaveChangesAsync();
        }
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Oppdater innsyn", Exact = true }).ClickAsync();
        await Expect(p.Resultat).ToHaveTextAsync(foer!);
        Assert.Equal(data, JsonSerializer.Serialize(await p.Lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe("FA"), new() { InkluderHistorikk = true })));
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Kun FA", Exact = true }).ClickAsync();
        var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Kun FA", Exact = true });
        await Expect(dialog).ToBeVisibleAsync(); await RenLesing(dialog);
        Assert.DoesNotContain("UX89 sensitiv", await p.Side.ContentAsync());
        await p.Side.Keyboard.PressAsync("Escape"); await p.Velg("TOM");
        await Expect(p.Resultat.GetByRole(AriaRole.Status)).ToHaveTextAsync("0 publiserte frister");
        await Expect(p.Rader).ToHaveCountAsync(0); await Expect(p.Resultat).Not.ToContainTextAsync("forslag");
        await p.Bilder("innsyn-tomt");
    });

    [Theory]
    [InlineData("POL")]
    [InlineData("")]
    [InlineData("SKOK")]
    public Task Forsinket_gammelt_gruppeutvalg_kan_ikke_erstatte_siste_valg_eller_tomt_valg(string neste) => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); var gammel = p.Kontroll.Neste("liste:FA");
        await p.Valg.SelectOptionAsync("FA"); await gammel.VentStart(); await Expect(p.Rader).ToHaveCountAsync(0);
        await p.Valg.SelectOptionAsync(neste);
        if (neste.Length == 0) await Expect(p.Resultat).ToContainTextAsync("Ingen gruppe valgt");
        else await Expect(p.Resultat.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToContainTextAsync($"({neste})");
        await gammel.Slipp(); await Expect(p.Valg).ToHaveValueAsync(neste);
        await Expect(p.Resultat).Not.ToContainTextAsync("Kun FA"); await Expect(p.Resultat).Not.ToContainTextAsync("UX89 historisk regnskap");
        await Expect(p.Rader).ToHaveCountAsync(neste.Length == 0 ? 0 : neste == "POL" ? 2 : 1);
        await p.Bilder("innsyn-siste-" + (neste.Length == 0 ? "tomt" : neste));
    });

    [Theory]
    [InlineData("POL")]
    [InlineData("")]
    public Task Sen_detalj_fra_forrige_gruppe_aapner_aldri_i_ny_kontekst(string neste) => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); await p.Velg("FA"); var gammel = p.Kontroll.Neste("detalj:FA");
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Kun FA", Exact = true }).ClickAsync(); await gammel.VentStart();
        await p.Valg.SelectOptionAsync(neste);
        if (neste.Length == 0) await Expect(p.Resultat).ToContainTextAsync("Ingen gruppe valgt");
        else await Expect(p.Resultat.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToContainTextAsync($"({neste})");
        await gammel.Slipp(); await Expect(p.Side.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Expect(p.Valg).ToHaveValueAsync(neste); await Expect(p.Resultat).Not.ToContainTextAsync("Kun FA");
    });

    [Fact]
    public Task Fratatt_deling_og_deaktivert_valg_avvises_ved_ny_detaljlesing() => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); await p.Velg("FA");
        await using (var db = await p.Fabrikk.CreateDbContextAsync())
        {
            var frist = await db.Frister.SingleAsync(f => f.Tittel == "Kun FA");
            db.FristSynlighet.RemoveRange(await db.FristSynlighet.Where(s => s.FristId == frist.Id).ToArrayAsync()); await db.SaveChangesAsync();
        }
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Kun FA", Exact = true }).ClickAsync();
        await Expect(p.Resultat.GetByRole(AriaRole.Alert)).ToContainTextAsync("ikke lenger tilgjengelig");
        await Expect(p.Side.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0); await Expect(p.Rader).ToHaveCountAsync(3);
        await p.Grupper.OppdaterAsync("FA", "Finansavdelingen", false);
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "FA og POL", Exact = true }).ClickAsync();
        await Expect(p.Valg).ToHaveValueAsync(""); await Expect(p.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("ikke lenger aktiv");
        await Expect(p.Resultat).ToContainTextAsync("Ingen gruppe valgt"); await Expect(p.Rader).ToHaveCountAsync(0);
        await Expect(p.Valg.Locator("option[value='FA']")).ToHaveCountAsync(0);
    });

    [Theory]
    [InlineData("aktive")]
    [InlineData("liste:FA")]
    public Task Feil_og_ny_henting_beholder_eksplisitt_gruppe_uten_gamle_frister_eller_raa_feil(string operasjon) => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/innsyn"); await p.Velg("POL"); var feil = p.Kontroll.Neste(operasjon, feil: true);
        await p.Valg.SelectOptionAsync("FA"); await feil.VentStart(); await Expect(p.Rader).ToHaveCountAsync(0); await feil.Slipp();
        await Expect(p.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke hente gruppeinnsyn");
        await Expect(p.Valg).ToHaveValueAsync("FA"); await Expect(p.Side.Locator("body")).Not.ToContainTextAsync("UX89-RÅ-DATABASEFEIL");
        await p.Bilder("innsyn-feil-" + operasjon.Replace(':', '-'));
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(p.Rader).ToHaveCountAsync(4); await Expect(p.Valg).ToHaveValueAsync("FA");
        await Expect(p.Resultat).Not.ToContainTextAsync("UX89 politisk frist");
    });

    private static string Kategorinavn(Kategori kategori) => kategori switch { Kategori.Gulbok => "Gul bok", Kategori.Regnskap => "Regnskap", _ => "Budsjett" };
    private static async Task RenLesing(ILocator flate)
    {
        foreach (var navn in new[] { "Rediger", "Foreslå frist", "Foreslå endring", "Vis i kalenderen" })
        {
            await Expect(flate.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true })).ToHaveCountAsync(0);
            await Expect(flate.GetByRole(AriaRole.Link, new() { Name = navn, Exact = true })).ToHaveCountAsync(0);
        }
        foreach (var tekst in new[] { "Synlig for:", "venter endring", "UX89-hemmelig-filnavn.pdf", "ux89-intern-loep" }) await Expect(flate).Not.ToContainTextAsync(tekst);
    }
}
