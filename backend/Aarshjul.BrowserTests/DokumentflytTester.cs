using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DokumentflytTester
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Ekte_PDF_kontrolleres_foer_direkte_eller_justert_godkjenning_og_gir_en_felles_frist(bool juster) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var foer = (await n.Frister()).Count;
        const string filnavn = "UX13 faktisk lokal.pdf";
        const string kilde = "Opplastet: " + filnavn;
        const string original = "UX13 rammefordeling 20. november 2030";
        await LastOpp(n, filnavn, original);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Kontroller forslagene", Exact = true }).ClickAsync();
        var kort = n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX13 rammefordeling" });
        await Expect(kort).ToHaveCountAsync(1);
        await Expect(kort).ToContainTextAsync("20.11.2030");
        await Expect(kort).ToContainTextAsync("2031");
        var datobevis = kort.GetByRole(AriaRole.Row).Filter(new() { HasText = "2030-11-20" });
        await Expect(datobevis).ToContainTextAsync(original);
        await Expect(datobevis.GetByRole(AriaRole.Cell).Last).ToHaveTextAsync("0.65");
        Assert.Equal(foer, (await n.Frister()).Count);
        await ForGodkjenningErFristenUsynlig(n);
        var opphav = n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true });
        await opphav.SelectOptionAsync("Robot");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("NyFrist");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Budsjett");
        await Expect(kort).ToHaveCountAsync(1);
        var dokumentId = await DokumentId(n, filnavn);
        var tittel = juster ? "UX13 kontrollert tentativ rammefordeling" : "UX13 rammefordeling";
        if (juster)
        {
            await kort.GetByRole(AriaRole.Button, new() { NameRegex = new("^(Juster|Vurder)$") }).PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.Locator("main")).ToContainTextAsync("«Lagre» publiserer");
            await Expect(n.Side.GetByRole(AriaRole.Table)).ToContainTextAsync(original);
            await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX13 avbrutt endring");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
            await KontrollerKontekst(n, kilde, 1);
            await Expect(kort.GetByRole(AriaRole.Heading)).ToHaveTextAsync("UX13 rammefordeling");
            Assert.Equal(foer, (await n.Frister()).Count);
            await kort.GetByRole(AriaRole.Button, new() { NameRegex = new("^(Juster|Vurder)$") }).ClickAsync();
            await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX13 rammefordeling");
            await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
            await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-21");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync("Ultimo");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Gulbok");
            await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Kontrollert mot originalt PDF-utdrag <trygg tekst>");
            await Expect(Gruppe(n.Side, "POL")).Not.ToBeCheckedAsync();
            foreach (var valg in await n.Side.Locator("fieldset.synlighet input").AllAsync()) await valg.UncheckAsync();
            await Expect(n.Side.Locator("fieldset.synlighet input:checked")).ToHaveCountAsync(0);
            await Gruppe(n.Side, "FA").CheckAsync();
            await Expect(n.Side.GetByRole(AriaRole.Table)).ToContainTextAsync("2030-11-20");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        }
        else
        {
            await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
            foreach (var valg in await kort.Locator("fieldset.synlighet input").AllAsync()) await valg.UncheckAsync();
            await Expect(kort.Locator("fieldset.synlighet input:checked")).ToHaveCountAsync(0);
            await Gruppe(kort, "FA").CheckAsync();
            await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        }
        await KontrollerKontekst(n, kilde, 0);
        await Expect(n.Side.GetByText("Ingen forslag til behandling.", new() { Exact = true })).ToBeVisibleAsync();
        var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        Assert.True(Guid.TryParse((string?)publisert["id"], out _));
        Assert.Equal(foer + 1, (await n.Frister()).Count);
        Assert.Equal(dokumentId, (string?)publisert["dokumentId"]);
        Assert.Null(publisert["kildeUrl"]);
        Assert.Equal(juster ? "2030-11-21" : "2030-11-20", (string?)publisert["dato"]);
        Assert.Equal(2031, (int)publisert["budsjettaar"]!);
        Assert.Equal(juster ? 1 : 0, (int)publisert["kategori"]!);
        Assert.Equal(juster ? 1 : 0, (int)publisert["datopresisjon"]!);
        Assert.Equal(juster ? 2 : (int?)null, (int?)publisert["datokvalifikator"]);
        Assert.Equal(new[] { "FA" }, publisert["synlighet"]!.AsArray().Select(v => (string)v!).ToArray());
        if (juster) Assert.Equal("Kontrollert mot originalt PDF-utdrag <trygg tekst>", (string?)publisert["notat"]);
        await KalenderforslagTester.KontrollerVisninger(n, publisert, juster ? "ultimo november 2030" : "20. november 2030", juster ? "Gul bok" : "Budsjett");
        await n.Gaa("/admin/frister?aar=2031");
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel });
        await Expect(rad).ToHaveCountAsync(1);
        await Expect(rad).ToContainTextAsync(juster ? "ultimo november 2030" : "20. november 2030");
        await Expect(rad).ToContainTextAsync(juster ? "Gul bok" : "Budsjett");
        await Expect(rad.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveAttributeAsync("href", $"/admin/frist/{publisert["id"]}?aar=2031");
        foreach (var persona in new[] { "demo-fa", "demo-fag" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await KalenderTester.VelgMaaned(n, "2030-11");
            var html = await n.Side.ContentAsync();
            foreach (var skjult in new[] { filnavn, "Opplastet:", "Konfidens", "Uttrekksbevis" }) Assert.DoesNotContain(skjult, html);
            if (persona == "demo-fa")
            {
                await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
                if (juster) await n.Side.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).ClickAsync();
                else await KalenderTester.AapneFrist(n, tittel);
                await Expect(n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Region, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
            }
            else Assert.DoesNotContain(await n.Frister(), f => (string?)f!["id"] == (string?)publisert["id"]);
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kildequery_bruker_eksakt_filnavn_i_snitt_med_arbeidsaar_og_ukjent_gir_null_treff() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string fil = "UX13 a + b & c.pdf";
        await LastOpp(n, fil, "UX13 riktig rammefordeling 20. november 2030");
        await LastOpp(n, "UX13 annen.pdf", "UX13 annen rammefordeling 20. november 2030");
        await LastOpp(n, fil, "UX13 annet aar rammefordeling 20. november 2030", 2032);
        var kilde = "Opplastet: " + fil;
        await n.Gaa("/admin/ko?aar=2031&kilde=" + Uri.EscapeDataString(kilde));
        await Expect(Kilde(n)).ToHaveValueAsync(kilde);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("UX13 riktig rammefordeling");
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "2032: 1 åpne forslag", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Article).GetByRole(AriaRole.Button, new() { Name = "Avvis", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft avvisning", Exact = true }).ClickAsync();
        await Expect(Kilde(n)).ToHaveValueAsync(kilde);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        foreach (var ukjent in new[] { kilde + " ekstra", kilde.ToLowerInvariant(), "Opplastet: UX13 a" })
        {
            await n.Gaa("/admin/ko?aar=2032&kilde=" + Uri.EscapeDataString(ukjent));
            await Expect(Kilde(n)).ToHaveValueAsync(ukjent);
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        }
        await n.Gaa("/admin/ko?aar=2032&kilde=" + Uri.EscapeDataString(kilde));
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("UX13 annet aar rammefordeling");
        Assert.DoesNotContain(await n.Frister(), f => ((string?)f!["tittel"])?.StartsWith("UX13 ") == true);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Ekte_PDF_uten_dato_eller_korrupt_fil_gir_forstaaelig_resultat_uten_publisering(bool korrupt) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/last-opp?aar=2031");
        var foer = (await n.Frister()).ToJsonString();
        await Fil(n).SetInputFilesAsync(new FilePayload
        {
            Name = korrupt ? "UX13 korrupt.pdf" : "UX13 uten dato.pdf", MimeType = "application/pdf",
            Buffer = korrupt ? Encoding.ASCII.GetBytes("%PDF-1.4 UX13-korrupt-privatdiagnostikk") : DokumentTester.Pdf("UX13 rammefordeling uten noen fastsatt dato")
        });
        await Les(n).ClickAsync();
        if (korrupt)
        {
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Klarte ikke lese dokumentet");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).Not.ToContainTextAsync("UX13-korrupt-privatdiagnostikk");
            await Expect(Les(n)).ToBeEnabledAsync();
        }
        else
        {
            await Expect(n.Side.GetByRole(AriaRole.Status).Filter(new() { HasText = "Fant ingen datoer" })).ToBeVisibleAsync();
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Legg inn en frist manuelt", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        }
        Assert.Equal(foer, (await n.Frister()).ToJsonString());
        await n.Gaa("/admin/ko?aar=2031&kilde=" + Uri.EscapeDataString("Opplastet: " + (korrupt ? "UX13 korrupt.pdf" : "UX13 uten dato.pdf")));
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Filvalg_er_sideeffektfritt_og_aarsbytte_fjerner_gamle_metadata_resultat_og_feil() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/last-opp?aar=2031");
        await Fil(n).SetInputFilesAsync(new FilePayload { Name = "UX13 stor.pdf", MimeType = "application/pdf", Buffer = new byte[15 * 1024 * 1024 + 1] });
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("15");
        await Expect(Les(n)).ToBeDisabledAsync();
        await Fil(n).SetInputFilesAsync(new FilePayload { Name = "UX13 feil.txt", MimeType = "text/plain", Buffer = Encoding.ASCII.GetBytes("tekst") });
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kun PDF");
        await Expect(Les(n)).ToBeDisabledAsync();
        await Fil(n).SetInputFilesAsync(new FilePayload { Name = "UX13 valgt.pdf", MimeType = "application/pdf", Buffer = DokumentTester.Pdf("UX13 valgt rammefordeling 20. november 2030") });
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        var probe = await n.Side.Context.NewPageAsync();
        await n.Gaa("/admin/ko?aar=2031&kilde=" + Uri.EscapeDataString("Opplastet: UX13 valgt.pdf"), probe);
        await Expect(probe.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        await probe.CloseAsync();
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true }).SelectOptionAsync("2027");
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX13 valgt.pdf");
        await Expect(Les(n)).ToBeDisabledAsync();
        await LastOpp(n, "UX13 ferdig.pdf", "UX13 ferdig rammefordeling 20. november 2030");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true }).SelectOptionAsync("2027");
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX13 ferdig.pdf");
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Kontroller forslagene", Exact = true })).ToHaveCountAsync(0);
        await Expect(Les(n)).ToBeDisabledAsync();
        await LastOpp(n, "UX13 ferdig.pdf", "UX13 ferdig rammefordeling 20. november 2030", 2032, allerede: true);
        await n.Gaa("/admin/ko?aar=alle&kilde=" + Uri.EscapeDataString("Opplastet: UX13 ferdig.pdf"));
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("2031");
        Assert.DoesNotContain(await n.Frister(), f => ((string?)f!["tittel"])?.StartsWith("UX13 ") == true);
        n.BekreftIngenKretsfeil();
    });

    internal static async Task LastOpp(Nettleser n, string navn, string tekst, int aar = 2031, bool allerede = false)
    {
        await n.Gaa("/admin/last-opp?aar=" + aar);
        await Fil(n).SetInputFilesAsync(new FilePayload { Name = navn, MimeType = "application/pdf", Buffer = DokumentTester.Pdf(tekst) });
        await Les(n).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync(allerede ? "allerede lest inn tidligere" : "laget 1 forslag");
    }

    private static async Task ForGodkjenningErFristenUsynlig(Nettleser n)
    {
        var opprinnelig = n.Side;
        n.Side = await n.Side.Context.NewPageAsync();
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^UX13 ") })).ToHaveCountAsync(0);
        await n.Gaa("/budsjettaar");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^UX13 ") })).ToHaveCountAsync(0);
        await n.Side.CloseAsync();
        n.Side = opprinnelig;
    }

    private static async Task KontrollerKontekst(Nettleser n, string kilde, int antall)
    {
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø", Exact = true })).ToBeVisibleAsync();
        await Expect(Kilde(n)).ToHaveValueAsync(kilde);
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true })).ToHaveValueAsync("2031");
        foreach (var (navn, verdi) in new[] { ("Opphav", "Robot"), ("Type", "NyFrist"), ("Kategori", "Budsjett") })
            await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = navn, Exact = true })).ToHaveValueAsync(verdi);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(antall);
    }

    private static async Task<string> DokumentId(Nettleser n, string filnavn)
    {
        // Kun lagret dokumentkobling observeres her; selve filbehandlingen er reell og ustubbet.
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        using var q = db.CreateCommand();
        q.CommandText = "SELECT Id FROM BehandledeDokumenter WHERE Tittel = $fil AND Url IS NULL";
        q.Parameters.AddWithValue("$fil", filnavn);
        return Guid.Parse((string)(await q.ExecuteScalarAsync())!).ToString();
    }
    private static ILocator Fil(Nettleser n) => n.Side.GetByLabel("Dokument (PDF)", new() { Exact = true });
    private static ILocator Les(Nettleser n) => n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true });
    private static ILocator Kilde(Nettleser n) => n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kilde eller innsender", Exact = true });
    private static ILocator Gruppe(IPage side, string kode) => side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({kode}\\)$") });
    private static ILocator Gruppe(ILocator kort, string kode) => kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({kode}\\)$") });
}
