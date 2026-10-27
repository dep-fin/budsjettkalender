using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

public sealed class KalenderperspektivTester
{
    [Fact]
    public Task Visningsbytte_beholder_dato_aarsunion_kategori_soek_og_tydelig_listeperiode() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX79 valgt 2026", dato: "2030-11-15", budsjettaar: 2026, notat: "Felles søkeord");
        await n.OpprettFrist("UX79 valgt 2028", dato: "2030-11-15", budsjettaar: 2028, notat: "Felles søkeord");
        await n.OpprettFrist("UX79 utelatt 2027", dato: "2030-11-15", budsjettaar: 2027, notat: "Felles søkeord");
        await n.OpprettFrist("UX79 annen uke", dato: "2030-11-01", budsjettaar: 2026, notat: "Felles søkeord");
        await n.OpprettFrist("UX79 annen kategori", dato: "2030-11-15", budsjettaar: 2026, kategori: "Gulbok", notat: "Felles søkeord");
        await n.OpprettFrist("UX79 annet søk", dato: "2030-11-15", budsjettaar: 2026);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(aarvalg.Locator("input:checked")).ToHaveCountAsync(0);
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        await Expect(Utvalg(n)).ToContainTextAsync("Budsjettår: 2026, 2028");
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await Expect(Utvalg(n)).ToContainTextAsync("Kategorier: Budsjett");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("søkeord");
        await Expect(Maanedskalender(n)).ToContainTextAsync("3 frister i måneden");
        var mini = Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 2 frister", Exact = true });
        await mini.ClickAsync();
        await Expect(mini).ToHaveAttributeAsync("aria-pressed", "true");
        await Bytt(n, "Uke");
        await Expect(Utvalg(n)).ToContainTextAsync("Uke 46, 2030 · 11. november 2030–17. november 2030");
        await Expect(Ukekalender(n)).ToContainTextAsync("2 frister i uken");
        await Bytt(n, "Liste");
        await Expect(Liste(n)).ToContainTextAsync("Liste for uke 46, 2030");
        foreach (var aar in new[] { 2026, 2028 })
        {
            var frist = Liste(n).GetByRole(AriaRole.Button, new() { Name = "UX79 valgt " + aar, Exact = true });
            await Expect(frist).ToContainTextAsync("Budsjettår " + aar);
            await frist.PressAsync("Enter");
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX79 valgt " + aar, Exact = true });
            await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "UX79 valgt " + aar, Exact = true })).ToBeFocusedAsync();
            await Expect(dialog).ToContainTextAsync("15. november 2030");
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(frist).ToBeFocusedAsync();
        }
        foreach (var tittel in new[] { "UX79 utelatt 2027", "UX79 annen uke", "UX79 annen kategori", "UX79 annet søk" })
            await Expect(Liste(n)).Not.ToContainTextAsync(tittel);
        await Bytt(n, "Måned");
        await Expect(Maanedskalender(n)).ToContainTextAsync("3 frister i måneden");
        await Expect(mini).ToHaveAttributeAsync("aria-pressed", "true");
        await Bytt(n, "Liste");
        await Expect(Liste(n)).ToContainTextAsync("Liste for november 2030");
        await Expect(Liste(n)).ToContainTextAsync("UX79 annen uke");
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("søkeord");
        await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).Not.ToBeCheckedAsync();
        // Kalenderlisten beholder sin kontekst ved separat historikk i Budsjettår.
        var meny = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true });
        await meny.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await BudsjettaarTester.VelgKompatibilitet(n);
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
        await meny.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await Expect(Liste(n)).ToContainTextAsync("Liste for november 2030");
        await Expect(mini).ToHaveAttributeAsync("aria-pressed", "true");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Hele_uken_viser_heldag_alle_tidspunkt_og_tentative_fra_begge_maaneder_uten_sorteringsdager() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX79 mandag", dato: "2030-11-25");
        await n.OpprettFrist("UX79 søndag", dato: "2030-12-01");
        await n.OpprettFrist("UX79 før uken", dato: "2030-11-24");
        await n.OpprettFrist("UX79 etter uken", dato: "2030-12-02");
        await n.OpprettFrist("UX79 heldag", dato: "2030-11-27");
        foreach (var (tittel, tid) in new[] { ("UX79 tidlig", "00:05"), ("UX79 samtidig A", "13:45"), ("UX79 samtidig B", "13:45"), ("UX79 sent", "23:55") })
            await n.OpprettFrist(tittel, tid: tid, dato: "2030-11-27");
        await n.OpprettFrist("UX79 tentativ november", tentativ: true, kvalifikator: "Primo", dato: "2030-11-15");
        await n.OpprettFrist("UX79 tentativ desember", tentativ: true, kvalifikator: "Ultimo", dato: "2030-12-15");
        var data = await n.Frister();
        Assert.Equal("2030-11-01", (string)data.Single(f => (string?)f!["tittel"] == "UX79 tentativ november")!["sorteringsdag"]!);
        Assert.Equal("2030-12-31", (string)data.Single(f => (string?)f!["tittel"] == "UX79 tentativ desember")!["sorteringsdag"]!);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 27. november 2030, 5 frister", Exact = true }).ClickAsync();
        await Bytt(n, "Uke");
        await Expect(Utvalg(n)).ToContainTextAsync("Uke 48, 2030 · 25. november 2030–1. desember 2030");
        await Expect(Ukekalender(n)).ToContainTextAsync("9 frister i uken · 7 med fast dag");
        var heldag = Ukekalender(n).GetByRole(AriaRole.Region, new() { Name = "Heldagsfrister 27. november 2030", Exact = true });
        var tidspunkter = Ukekalender(n).GetByRole(AriaRole.Region, new() { Name = "Fristtidspunkter 27. november 2030", Exact = true });
        await Expect(heldag.GetByRole(AriaRole.Button)).ToHaveCountAsync(1);
        await Expect(heldag).ToContainTextAsync("UX79 heldag");
        await Expect(tidspunkter.GetByRole(AriaRole.Button)).ToHaveCountAsync(4);
        await Expect(tidspunkter.Locator("time")).ToHaveTextAsync(new[] { "00:05", "13:45", "13:45", "23:55" });
        var titler = new[] { "UX79 mandag", "UX79 søndag", "UX79 heldag", "UX79 tidlig", "UX79 samtidig A", "UX79 samtidig B", "UX79 sent", "UX79 tentativ november", "UX79 tentativ desember" };
        foreach (var tittel in titler)
        {
            var knapp = Ukekalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await knapp.PressAsync("Enter");
            var detaljer = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
            await Expect(detaljer.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
            if (tittel.Contains("tentativ"))
            {
                await Expect(detaljer).ToContainTextAsync("Tentativ måned – dato ikke fastsatt");
                await Expect(detaljer).Not.ToContainTextAsync("15.");
            }
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(knapp).ToBeFocusedAsync();
        }
        foreach (var maaned in new[] { "november", "desember" })
            await Expect(Ukekalender(n).GetByRole(AriaRole.Region, new() { Name = "Tentative frister i " + maaned + " 2030", Exact = true }))
                .ToContainTextAsync("UX79 tentativ " + maaned);
        foreach (var dag in new[] { "25. november 2030", "26. november 2030", "27. november 2030", "28. november 2030", "29. november 2030", "30. november 2030", "1. desember 2030" })
            await Expect(Ukekalender(n).GetByRole(AriaRole.Region, new() { Name = dag, Exact = true })).Not.ToContainTextAsync("UX79 tentativ");
        await Bytt(n, "Liste");
        foreach (var tittel in titler) await Expect(Liste(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
        foreach (var tittel in new[] { "UX79 før uken", "UX79 etter uken" }) await Expect(Liste(n)).Not.ToContainTextAsync(tittel);
        await Liste(n).GetByRole(AriaRole.Button, new() { Name = "Neste uke", Exact = true }).ClickAsync();
        await Expect(Liste(n)).ToContainTextAsync("Liste for uke 49, 2030 · 2. desember 2030–8. desember 2030");
        await Expect(Liste(n)).ToContainTextAsync("UX79 etter uken");
        await Expect(Liste(n)).ToContainTextAsync("UX79 tentativ desember");
        await Expect(Liste(n)).Not.ToContainTextAsync("UX79 tentativ november");
        await Liste(n).GetByRole(AriaRole.Button, new() { Name = "Forrige uke", Exact = true }).ClickAsync();
        await Expect(Liste(n)).ToContainTextAsync("9 frister i uken");
        await n.Side.SetViewportSizeAsync(390, 844);
        await Bytt(n, "Uke");
        foreach (var tittel in titler)
        {
            var knapp = Ukekalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await knapp.FocusAsync();
            await knapp.PressAsync("Space");
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true }).GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(knapp).ToBeFocusedAsync();
        }
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "ux79-week-mobile.png"), FullPage = true });
        await Bytt(n, "Liste");
        var tastaturrekkefolge = new[] { "UX79 tentativ november", "UX79 tentativ desember", "UX79 mandag", "UX79 heldag", "UX79 tidlig", "UX79 samtidig A", "UX79 samtidig B", "UX79 sent", "UX79 søndag" };
        await Liste(n).GetByRole(AriaRole.Button, new() { Name = tastaturrekkefolge[0], Exact = true }).FocusAsync();
        foreach (var tittel in tastaturrekkefolge)
        {
            var knapp = Liste(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await Expect(knapp).ToBeFocusedAsync();
            await knapp.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true }).GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(knapp).ToBeFocusedAsync();
            await n.Side.Keyboard.PressAsync("Tab");
        }
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Uke_og_liste_navigerer_ISO_aar_skuddag_DateOnly_grenser_og_Oslo_idag() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX79 gammelt kalenderår", dato: "2020-12-31", budsjettaar: 2028);
        await n.OpprettFrist("UX79 nytt kalenderår", dato: "2021-01-01", budsjettaar: 2028);
        await n.OpprettFrist("UX79 skuddag", dato: "2028-02-29", budsjettaar: 2028);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2021-01");
        await Bytt(n, "Uke");
        await Expect(Utvalg(n)).ToContainTextAsync("Uke 53, 2020 · 28. desember 2020–3. januar 2021");
        foreach (var tittel in new[] { "UX79 gammelt kalenderår", "UX79 nytt kalenderår" }) await Expect(Ukekalender(n)).ToContainTextAsync(tittel);
        await Bytt(n, "Liste");
        await Expect(Liste(n)).ToContainTextAsync("UX79 gammelt kalenderår");
        await Liste(n).GetByRole(AriaRole.Button, new() { Name = "Neste uke", Exact = true }).ClickAsync();
        await Expect(Liste(n)).ToContainTextAsync("Liste for uke 1, 2021 · 4. januar 2021–10. januar 2021");
        await Expect(Liste(n)).ToContainTextAsync("0 frister i uken");
        await n.Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync("2028-02");
        await Expect(Utvalg(n)).ToContainTextAsync("Uke 5, 2028");
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 29. februar 2028, 1 frist", Exact = true }).ClickAsync();
        await Expect(Liste(n)).ToContainTextAsync("28. februar 2028–5. mars 2028");
        await Expect(Liste(n)).ToContainTextAsync("UX79 skuddag");
        await n.Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync("0001-01");
        await Expect(Liste(n)).ToContainTextAsync("1. januar 0001–7. januar 0001");
        await Expect(Liste(n).GetByRole(AriaRole.Button, new() { Name = "Forrige uke", Exact = true })).ToBeDisabledAsync();
        await Bytt(n, "Uke");
        await Expect(Ukekalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Åpne dagsagenda for") })).ToHaveCountAsync(7);
        await n.Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync("9999-12");
        await Expect(Utvalg(n)).ToContainTextAsync("29. november 9999–5. desember 9999");
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 31. desember 9999", Exact = true }).ClickAsync();
        await Expect(Utvalg(n)).ToContainTextAsync("27. desember 9999–31. desember 9999");
        await Expect(Ukekalender(n).GetByRole(AriaRole.Button, new() { Name = "Neste uke", Exact = true })).ToBeDisabledAsync();
        await Expect(Ukekalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Åpne dagsagenda for") })).ToHaveCountAsync(5);
        await Ukekalender(n).GetByRole(AriaRole.Button, new() { Name = "Åpne dagsagenda for 31. desember 9999", Exact = true }).PressAsync("Enter");
        var tom = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 31. desember 9999", Exact = true });
        await Expect(tom.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Expect(tom).ToContainTextAsync("Ingen frister denne dagen");
        await n.Side.Keyboard.PressAsync("Escape");
        await Bytt(n, "Liste");
        await Expect(Liste(n).GetByRole(AriaRole.Button, new() { Name = "Neste uke", Exact = true })).ToBeDisabledAsync();
        var idag = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        await Liste(n).GetByRole(AriaRole.Button, new() { Name = "I dag", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync(idag.ToString("yyyy-MM"));
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Velg " + idag.ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("nb-NO"))) })).ToHaveAttributeAsync("aria-current", "date");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Nye_visninger_bevarer_fullstendige_detaljer_kilde_aarsfarge_roller_og_serverens_skulte_utvalg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string tittel = "UX79 Fullstendig fristtittel med langt innhold som fortsatt skal kunne leses på liten skjerm";
        const string url = "https://www.regjeringen.no/no/ux79-rundskriv?a=1&b=2";
        await n.OpprettFrist(tittel, dato: "2030-11-15", budsjettaar: 2028, kategori: "Gulbok", notat: "Fullt notat med <trygge tegn>.");
        await n.OpprettFrist("UX79 kildefri", dato: "2030-11-17", budsjettaar: 2026);
        await n.OpprettFrist("UX79 skjult POL", "POL", dato: "2030-11-15", budsjettaar: 2042);
        var id = (string)(await n.Frister()).Single(f => (string?)f!["tittel"] == tittel)!["id"]!;
        var skjultId = (string)(await n.Frister()).Single(f => (string?)f!["tittel"] == "UX79 skjult POL")!["id"]!;
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        // Syntetisk dokument-/gruppeoppsett; appen har ingen dokumentmetadata-editor.
        using (var oppsett = db.CreateCommand())
        {
            oppsett.CommandText = """
                INSERT INTO BehandledeDokumenter (Id, Kilde, DokumentNokkel, Tittel, Url, ForstSett, BehandletStatus, UttrekksForsoek)
                VALUES ($dokument, 'syntetisk', 'UX79-dokumentnokkel', 'UX79-dokumenttittel', $url, '2026-10-27 00:00:00+00:00', 0, 0);
                UPDATE Frister SET DokumentId = $dokument WHERE Id = $frist;
                INSERT INTO FristSynlighet (FristId, GruppeKode) SELECT Id, 'FAG' FROM Frister WHERE Tittel IN ($tittel, 'UX79 kildefri');
                INSERT INTO FristSynlighet (FristId, GruppeKode) SELECT Id, 'FIN-FAG' FROM Frister WHERE Tittel IN ($tittel, 'UX79 kildefri');
                """;
            oppsett.Parameters.AddWithValue("$dokument", Guid.NewGuid().ToString().ToUpperInvariant());
            oppsett.Parameters.AddWithValue("$frist", id.ToUpperInvariant());
            oppsett.Parameters.AddWithValue("$tittel", tittel);
            oppsett.Parameters.AddWithValue("$url", url);
            await oppsett.ExecuteNonQueryAsync();
        }
        await n.LoggInn("demo-fa");
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX79 upublisert");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-15");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2044");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX79 upublisert" })).ToContainTextAsync("Venter");
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await VelgMaaned(n, "2030-11");
            var maanedsfarge = await Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })
                .EvaluateAsync<string>("el => getComputedStyle(el).borderLeftColor");
            await Minikalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Velg 15\\. november 2030,") }).ClickAsync();
            foreach (var visning in new[] { "Uke", "Liste" })
            {
                await Bytt(n, visning);
                var flate = visning == "Uke" ? Ukekalender(n) : Liste(n);
                var frist = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
                Assert.Equal(maanedsfarge, await frist.EvaluateAsync<string>("el => getComputedStyle(el).borderLeftColor"));
                await Expect(frist).ToContainTextAsync("Budsjettår 2028");
                await frist.PressAsync("Enter");
                var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
                await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
                foreach (var tekst in new[] { tittel, "15. november 2030", "Heldag", "Gul bok", "2028", "Fullt notat med <trygge tegn>." }) await Expect(dialog).ToContainTextAsync(tekst);
                var kilde = dialog.GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true });
                await Expect(kilde).ToHaveAttributeAsync("href", url);
                await Expect(kilde).ToHaveAttributeAsync("target", "_blank");
                await Expect(kilde).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
                await Expect(kilde).ToHaveAttributeAsync("rel", "noopener noreferrer");
                await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
                await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true })).ToHaveCountAsync(persona is "demo-fa" or "demo-finfag" ? 1 : 0);
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(frist).ToBeFocusedAsync();
                await flate.GetByRole(AriaRole.Button, new() { Name = "UX79 kildefri", Exact = true }).PressAsync("Enter");
                var uten = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX79 kildefri", Exact = true });
                await Expect(uten.GetByRole(AriaRole.Heading, new() { Name = "UX79 kildefri", Exact = true })).ToBeFocusedAsync();
                await Expect(uten.GetByRole(AriaRole.Region, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(flate.GetByRole(AriaRole.Button, new() { Name = "UX79 kildefri", Exact = true })).ToBeFocusedAsync();
                if (persona != "demo-admin")
                {
                    var dom = await n.Side.ContentAsync();
                    foreach (var skjult in new[] { "UX79 skjult POL", skjultId, "UX79 upublisert", "UX79-dokumentnokkel", "UX79-dokumenttittel" })
                        Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
                    await Expect(n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true }).GetByRole(AriaRole.Checkbox, new() { Name = "2042", Exact = true })).ToHaveCountAsync(0);
                    await Expect(flate).ToContainTextAsync("2 frister i uken");
                }
            }
            if (persona != "demo-admin")
            {
                var svar = await n.Side.Context.APIRequest.GetAsync("/admin/frist/" + id, new() { MaxRedirects = 0 });
                Assert.Contains(svar.Status, new[] { 302, 403 });
            }
            await n.Side.SetViewportSizeAsync(390, 844);
            await Liste(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true }).GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "ux79-details-" + persona + "-mobile.png"), FullPage = true });
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Liste_og_uke_har_trygg_lastefeil_og_kan_proeve_igjen_med_bevart_utvalg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX79 gjenopprettet", dato: "2030-11-15", budsjettaar: 2028);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("gjenopprettet");
        await Expect(Maanedskalender(n)).ToContainTextAsync("1 frist i måneden");
        await Bytt(n, "Liste");
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        async Task FlyttTabell(bool tilbake)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = tilbake ? "ALTER TABLE FristerUtilgjengelig RENAME TO Frister" : "ALTER TABLE Frister RENAME TO FristerUtilgjengelig";
            await cmd.ExecuteNonQueryAsync();
        }
        await FlyttTabell(false);
        try
        {
            await Liste(n).GetByRole(AriaRole.Button, new() { Name = "Neste måned", Exact = true }).ClickAsync();
            await Expect(Liste(n).GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå. Prøv igjen.");
            await Bytt(n, "Uke");
            await Expect(Ukekalender(n).GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå. Prøv igjen.");
            await Expect(Utvalg(n)).ToContainTextAsync("Søk: «gjenopprettet»");
        }
        finally { await FlyttTabell(true); }
        await Ukekalender(n).GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(Ukekalender(n)).ToContainTextAsync("Ingen frister i utvalget ditt denne uken");
        await Expect(Ukekalender(n).GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Bytt(n, "Måned");
        await VelgMaaned(n, "2030-11");
        await Bytt(n, "Liste");
        await Expect(Liste(n)).ToContainTextAsync("UX79 gjenopprettet");
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("gjenopprettet");
        n.BekreftIngenKretsfeil();
    });

    internal static ILocator Ukekalender(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Ukekalender", Exact = true });
    internal static ILocator Liste(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Kalenderliste", Exact = true });
    internal static ILocator Utvalg(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg", Exact = true });
    internal static async Task Bytt(Nettleser n, string visning)
    {
        var knapp = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Kalendervisning", Exact = true })
            .GetByRole(AriaRole.Button, new() { Name = visning, Exact = true });
        await knapp.ClickAsync();
        await Expect(knapp).ToHaveAttributeAsync("aria-pressed", "true");
        var flate = visning == "Uke" ? Ukekalender(n) : visning == "Liste" ? Liste(n) : Maanedskalender(n);
        await Expect(flate).ToBeVisibleAsync();
        await Expect(flate).ToHaveAttributeAsync("aria-busy", "false");
    }
}
