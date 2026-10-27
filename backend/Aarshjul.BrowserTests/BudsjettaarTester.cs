using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

public sealed class BudsjettaarTester
{
    [Fact]
    public Task Hele_budsjettaaret_har_samme_IDer_og_verdier_i_tidslinje_fristliste_og_leseport() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var (tittel, dato, kategori) in new[]
        {
            ("UX80 før standardvindu", "2024-08-20", "Budsjett"),
            ("UX80 historisk regnskap", "2025-01-20", "Regnskap"),
            ("UX80 budsjettåret", "2026-11-20", "Gulbok"),
            ("UX80 etter standardvindu", "2027-08-20", "Budsjett")
        })
            await n.OpprettFrist(tittel, dato: dato, budsjettaar: 2026, kategori: kategori, notat: "Samme innhold i begge visninger");
        await n.OpprettFrist("UX80 annet budsjettår", dato: "2026-11-20", budsjettaar: 2028);
        var data = (await n.Frister()).Where(f => (int)f!["budsjettaar"]! == 2026).ToArray();
        await n.Gaa("/budsjettaar");
        await VelgAar(n, 2026);
        var faser = n.Side.Locator("[aria-label='Budsjettårets fem faser']");
        await Expect(faser.Locator("strong")).ToHaveTextAsync(new[] { "Forberedelser", "Utarbeidelse av budsjettforslag", "Fremleggelse og vedtak", "Gjennomføring og revidering", "Regnskap" });
        await Expect(faser.GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
        await Expect(Tidslinje(n, 2026).GetByRole(AriaRole.Region, new() { Name = "Frister før tidslinjen", Exact = true })).ToContainTextAsync("UX80 før standardvindu");
        await Expect(Tidslinje(n, 2026).GetByRole(AriaRole.Region, new() { Name = "Frister etter tidslinjen", Exact = true })).ToContainTextAsync("UX80 etter standardvindu");
        foreach (var visning in new[] { "Tidslinje", "Fristliste" })
        {
            await Bytt(n, visning);
            var flate = Flate(n, visning, 2026);
            await KontrollerIDer(flate, data);
            await AapneGrupper(flate);
            foreach (var (tittel, dato, kategori) in new[]
            {
                ("UX80 før standardvindu", "20. august 2024", "Budsjett"),
                ("UX80 historisk regnskap", "20. januar 2025", "Regnskap"),
                ("UX80 budsjettåret", "20. november 2026", "Gul bok"),
                ("UX80 etter standardvindu", "20. august 2027", "Budsjett")
            })
            {
                var knapp = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
                await knapp.PressAsync("Enter");
                var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
                await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
                foreach (var tekst in new[] { dato, kategori, "2026", "Samme innhold i begge visninger" })
                    await Expect(dialog).ToContainTextAsync(tekst);
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(knapp).ToBeFocusedAsync();
            }
            await Expect(flate).Not.ToContainTextAsync("UX80 annet budsjettår");
            await VelgAar(n, 2028);
            await Expect(Flate(n, visning, 2028)).ToContainTextAsync("UX80 annet budsjettår");
            await Expect(Flate(n, visning, 2028)).Not.ToContainTextAsync("UX80 historisk regnskap");
            await VelgAar(n, 2026);
            await KontrollerIDer(Flate(n, visning, 2026), data);
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tette_samtidige_frister_og_alle_tentative_kvalifikatorer_kan_aapnes_med_tastatur_paa_mobil() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var (tittel, tid) in new[] { ("UX80 heldag", (string?)null), ("UX80 tidlig", "00:05"), ("UX80 sent", "23:55") })
            await n.OpprettFrist(tittel, dato: "2025-11-20", budsjettaar: 2026, tid: tid);
        foreach (var bokstav in new[] { "A", "B", "C", "D", "E", "F" })
            await n.OpprettFrist("UX80 samtidig " + bokstav, dato: "2025-11-20", budsjettaar: 2026, tid: "13:45");
        foreach (var kvalifikator in new[] { "", "Primo", "Medio", "Ultimo" })
            await n.OpprettFrist("UX80 tentativ " + (kvalifikator == "" ? "måned" : kvalifikator), dato: "2025-11-15", budsjettaar: 2026,
                tentativ: true, kvalifikator: kvalifikator);
        var data = (await n.Frister()).Where(f => (int)f!["budsjettaar"]! == 2026).ToArray();
        await n.Gaa("/budsjettaar");
        await VelgAar(n, 2026);
        await n.Side.SetViewportSizeAsync(390, 844);
        foreach (var visning in new[] { "Tidslinje", "Fristliste" })
        {
            await Bytt(n, visning);
            var flate = Flate(n, visning, 2026);
            await KontrollerIDer(flate, data);
            await AapneGrupper(flate);
            var egne = data.Where(f => ((string)f!["tittel"]!).StartsWith("UX80 ")).ToArray();
            foreach (var frist in egne)
            {
                var tittel = (string)frist!["tittel"]!;
                var knapp = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
                await Expect(knapp).ToBeVisibleAsync();
                await knapp.FocusAsync();
                await knapp.PressAsync("Space");
                var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
                await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
                if (tittel.Contains("tentativ"))
                {
                    await Expect(dialog).ToContainTextAsync("Tentativ måned – dato ikke fastsatt");
                    await Expect(dialog).ToContainTextAsync((tittel.EndsWith("måned") ? "" : tittel.Split(' ')[^1].ToLowerInvariant() + " ") + "november 2025");
                    await Expect(dialog).Not.ToContainTextAsync("15.");
                    await Expect(dialog).Not.ToContainTextAsync("kl.");
                }
                else
                {
                    await Expect(dialog).ToContainTextAsync("20. november 2025");
                    var tid = (string?)frist["klokkeslett"];
                    await Expect(dialog).ToContainTextAsync(tittel == "UX80 heldag" ? "Heldag" : tid![..5]);
                }
                Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(knapp).ToBeFocusedAsync();
            }
            var samtidige = flate.Locator("button[data-frist-id]").Filter(new() { HasText = "UX80 samtidig" });
            await Expect(samtidige).ToHaveCountAsync(6);
            Assert.Equal(new[] { "UX80 samtidig A", "UX80 samtidig B", "UX80 samtidig C", "UX80 samtidig D", "UX80 samtidig E", "UX80 samtidig F" },
                await samtidige.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('aria-label'))"));
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Begge_visninger_bruker_felles_trygge_kilder_detaljer_rollehandlinger_og_serverfiltrert_innhold() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string tittel = "UX80 Fullstendig frist med <trygg tekst> og lang tittel på mobil";
        const string url = "https://www.regjeringen.no/no/ux80-rundskriv?a=1&b=2";
        await n.OpprettFrist(tittel, dato: "2025-11-20", budsjettaar: 2026, kategori: "Regnskap", notat: "Hele notatet med <trygge tegn>.", tid: "13:45");
        await n.OpprettFrist("UX80 kildefri", dato: "2026-11-20", budsjettaar: 2026);
        await n.OpprettFrist("UX80 skjult POL", "POL", dato: "2025-11-20", budsjettaar: 2042);
        var alle = await n.Frister();
        var id = (string)alle.Single(f => (string?)f!["tittel"] == tittel)!["id"]!;
        var skjultId = (string)alle.Single(f => (string?)f!["tittel"] == "UX80 skjult POL")!["id"]!;
        using (var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}"))
        {
            await db.OpenAsync();
            using var oppsett = db.CreateCommand();
            // Dokumentmetadata har ingen editor; dette er bare oppsett i scenarioets disponible database.
            oppsett.CommandText = """
                INSERT INTO BehandledeDokumenter (Id, Kilde, DokumentNokkel, Tittel, Url, ForstSett, BehandletStatus, UttrekksForsoek)
                VALUES ($dokument, 'syntetisk', 'UX80-raanokkel', 'UX80-raadokument', $url, '2026-10-27 00:00:00+00:00', 0, 0);
                UPDATE Frister SET DokumentId = $dokument WHERE Id = $frist;
                INSERT INTO FristSynlighet (FristId, GruppeKode) SELECT Id, 'FAG' FROM Frister WHERE Tittel IN ($tittel, 'UX80 kildefri');
                INSERT INTO FristSynlighet (FristId, GruppeKode) SELECT Id, 'FIN-FAG' FROM Frister WHERE Tittel IN ($tittel, 'UX80 kildefri');
                """;
            oppsett.Parameters.AddWithValue("$dokument", Guid.NewGuid().ToString().ToUpperInvariant());
            oppsett.Parameters.AddWithValue("$frist", id.ToUpperInvariant());
            oppsett.Parameters.AddWithValue("$tittel", tittel);
            oppsett.Parameters.AddWithValue("$url", url);
            await oppsett.ExecuteNonQueryAsync();
        }
        await n.LoggInn("demo-fa");
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX80 upublisert");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2025-11-20");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2044");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX80 upublisert" })).ToContainTextAsync("Venter");
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            var data = (await n.Frister()).Where(f => (int)f!["budsjettaar"]! == 2026).ToArray();
            await n.Gaa("/budsjettaar");
            await VelgAar(n, 2026);
            foreach (var visning in new[] { "Tidslinje", "Fristliste" })
            {
                await Bytt(n, visning);
                var flate = Flate(n, visning, 2026);
                await KontrollerIDer(flate, data);
                await AapneGrupper(flate);
                var knapp = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
                await knapp.PressAsync("Enter");
                var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
                await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
                foreach (var tekst in new[] { "20. november 2025 kl. 13:45", "Regnskap", "2026", "Hele notatet med <trygge tegn>." })
                    await Expect(dialog).ToContainTextAsync(tekst);
                var kilde = dialog.GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true });
                await Expect(kilde).ToHaveAttributeAsync("href", url);
                await Expect(kilde).ToHaveAttributeAsync("target", "_blank");
                await Expect(kilde).ToHaveAttributeAsync("rel", "noopener noreferrer");
                await Expect(kilde).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
                Assert.True(await kilde.EvaluateAsync<bool>("el => !!(el.compareDocumentPosition(document.querySelector('section[aria-label=\"Om fristen\"]')) & Node.DOCUMENT_POSITION_FOLLOWING)"));
                await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
                await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true })).ToHaveCountAsync(persona is "demo-fa" or "demo-finfag" ? 1 : 0);
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(knapp).ToBeFocusedAsync();
                await flate.GetByRole(AriaRole.Button, new() { Name = "UX80 kildefri", Exact = true }).ClickAsync();
                var utenKilde = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX80 kildefri", Exact = true });
                await Expect(utenKilde.GetByRole(AriaRole.Heading, new() { Name = "UX80 kildefri", Exact = true })).ToBeFocusedAsync();
                await Expect(utenKilde.GetByRole(AriaRole.Region, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(flate.GetByRole(AriaRole.Button, new() { Name = "UX80 kildefri", Exact = true })).ToBeFocusedAsync();
                var dom = await n.Side.ContentAsync();
                foreach (var skjult in new[] { "UX80 upublisert", "UX80-raanokkel", "UX80-raadokument" }) Assert.DoesNotContain(skjult, dom);
                if (persona != "demo-admin")
                {
                    foreach (var skjult in new[] { "UX80 skjult POL", skjultId }) Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
                    await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).Locator("option[value='2042'], option[value='2044']")).ToHaveCountAsync(0);
                }
            }
            // Denne kretsen har ikke åpnet Kalender ennå. Ordinære årsstandardvalg må initialiseres også ved direkte overgang.
            var aarsvalg = (await n.Frister()).Select(f => (int)f!["budsjettaar"]!).Distinct().ToArray();
            await Fristliste(n, 2026).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true })
                .GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            foreach (var aar in aarsvalg) await Expect(n.Side.GetByLabel(aar.ToString(), new() { Exact = true })).ToBeCheckedAsync();
            await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2025-11");
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kalenderovergang_bevarer_2026_2028_soek_kategori_og_utvider_bare_med_maalets_budsjettaar() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX80 bevart fast", dato: "2026-11-15", budsjettaar: 2026, notat: "bevart søkeord");
        await n.OpprettFrist("UX80 skjult av kategori", dato: "2026-11-15", budsjettaar: 2027, kategori: "Regnskap", notat: "bevart søkeord");
        await n.OpprettFrist("UX80 tentativ overgang", dato: "2026-12-15", budsjettaar: 2028, tentativ: true, notat: "bevart søkeord");
        await n.Gaa("/kalender");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(aarvalg.Locator("input:checked")).ToHaveCountAsync(0);
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        await Expect(KalenderperspektivTester.Utvalg(n)).ToContainTextAsync("Budsjettår: 2026, 2028");
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await Expect(KalenderperspektivTester.Utvalg(n)).ToContainTextAsync("Kategorier: Budsjett");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("bevart");
        await Expect(KalenderperspektivTester.Utvalg(n)).ToContainTextAsync("Søk: «bevart»");
        var meny = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true });
        foreach (var (aar, tittel, maaned) in new[] { (2026, "UX80 bevart fast", "2026-11"), (2027, "UX80 skjult av kategori", "2026-11"), (2028, "UX80 tentativ overgang", "2026-12") })
        {
            await meny.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
            await VelgAar(n, aar);
            await Bytt(n, "Fristliste");
            await Fristliste(n, aar).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true })
                .GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync(maaned);
            await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("bevart");
            foreach (var valgt in new[] { "2026", "2028" }) await Expect(n.Side.GetByLabel(valgt, new() { Exact = true })).ToBeCheckedAsync();
            await Expect(n.Side.GetByLabel("Gul bok", new() { Exact = true })).Not.ToBeCheckedAsync();
            if (aar == 2026)
            {
                await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).Not.ToBeCheckedAsync();
                await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
            }
            if (aar == 2027)
            {
                await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).ToBeCheckedAsync();
                await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
                await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Endre kalenderutvalg", Exact = true })).ToBeVisibleAsync();
                await Expect(n.Side.Locator("main")).ToContainTextAsync("kategori");
                await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToHaveCountAsync(0);
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Endre kalenderutvalg", Exact = true }).ClickAsync();
                await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
                await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
            }
            if (aar == 2028)
            {
                await Expect(n.Side.Locator("main")).ToContainTextAsync("dato ikke fastsatt");
                await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Velg 15\\. desember 2026") })).ToHaveAttributeAsync("aria-pressed", "false");
                await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Tentative frister i desember 2026", Exact = true })).ToContainTextAsync(tittel);
            }
        }
        // Søket kan skjule målet uavhengig av kategoriene; også dette må forklares uten å tømmes.
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("ingen treff for målet");
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        await meny.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await VelgAar(n, 2026);
        await Bytt(n, "Fristliste");
        await Fristliste(n, 2026).GetByRole(AriaRole.Button, new() { Name = "UX80 bevart fast", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("ingen treff for målet");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("søk");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Endre kalenderutvalg", Exact = true })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Gammelt_aarshjul_bevarer_fleraars_kategori_historikk_og_scopede_valg_uten_aa_begrense_hele_aaret() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX80 gammel Regnskap", dato: "2020-01-20", budsjettaar: 2026, kategori: "Regnskap");
        await n.OpprettFrist("UX80 annet gammelt år", dato: "2020-01-20", budsjettaar: 2028);
        await n.OpprettFrist("UX80 fremtidig mellomår", dato: "2030-01-20", budsjettaar: 2027);
        await n.Gaa("/aarshjul");
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX80 gammel Regnskap");
        await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("UX80 annet gammelt år");
        await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("UX80 gammel Regnskap");
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX80 fremtidig mellomår");
        await n.Side.GetByLabel("Budsjett", new() { Exact = true }).UncheckAsync();
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX80 annet gammelt år");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("UX80 gammel Regnskap");
        var meny = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true });
        await meny.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await meny.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await VelgAar(n, 2026);
        await Bytt(n, "Fristliste");
        await Expect(Fristliste(n, 2026)).ToContainTextAsync("UX80 gammel Regnskap");
        await VelgKompatibilitet(n);
        foreach (var navn in new[] { "Vis historikk", "Regnskap", "2026", "2028" })
            await Expect(n.Side.GetByLabel(navn, new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Vis hele budsjettåret 2026", Exact = true }).ClickAsync();
        await Expect(Fristliste(n, 2026)).ToContainTextAsync("UX80 gammel Regnskap");
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).ToHaveCountAsync(0);
        await n.Gaa("/aarshjul"); // full lasting = ny krets, gamle standardvalg tilbake.
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true }).Locator("input:checked")).ToHaveCountAsync(0);
        await Expect(meny.GetByRole(AriaRole.Link, new() { Name = "Årshjul", Exact = true })).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tomt_budsjettutvalg_og_trygg_lastefeil_kan_haandteres_i_begge_visninger() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX80 gjenopprettet budsjettløp", dato: "2025-11-20", budsjettaar: 2026);
        await n.OpprettFrist("UX80 annet tilgjengelig budsjettløp", dato: "2025-11-20", budsjettaar: 2027);
        await n.Gaa("/budsjettaar");
        await VelgAar(n, 2026);
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        async Task FlyttTabell(bool gjenopprett)
        {
            using var kommando = db.CreateCommand();
            kommando.CommandText = gjenopprett ? "ALTER TABLE UX80_Frister RENAME TO Frister" : "ALTER TABLE Frister RENAME TO UX80_Frister";
            await kommando.ExecuteNonQueryAsync();
        }
        await FlyttTabell(false);
        try
        {
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync("2027");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå. Prøv igjen.");
            await Bytt(n, "Fristliste");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå. Prøv igjen.");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).Not.ToContainTextAsync("SQL");
        }
        finally { await FlyttTabell(true); }
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await VelgAar(n, 2026);
        await Expect(Fristliste(n, 2026)).ToContainTextAsync("UX80 gjenopprettet budsjettløp");
        using (var slett = db.CreateCommand())
        {
            slett.CommandText = "DELETE FROM FristSynlighet; DELETE FROM Frister";
            await slett.ExecuteNonQueryAsync();
        }
        await n.Gaa("/budsjettaar");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen tilgjengelige budsjettår");
        await Bytt(n, "Fristliste");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen tilgjengelige budsjettår");
        await n.Side.SetViewportSizeAsync(390, 844);
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Tidslinje(Nettleser n, int aar)
        => n.Side.GetByRole(AriaRole.Region, new() { Name = "Tidslinje for budsjettår " + aar, Exact = true });

    private static ILocator Fristliste(Nettleser n, int aar)
        => n.Side.GetByRole(AriaRole.Region, new() { Name = "Fristliste for budsjettår " + aar, Exact = true });

    private static ILocator Flate(Nettleser n, string visning, int aar) => visning == "Tidslinje" ? Tidslinje(n, aar) : Fristliste(n, aar);

    private static async Task AapneGrupper(ILocator flate)
    {
        foreach (var knapp in await flate.GetByRole(AriaRole.Button, new() { NameRegex = new("^Vis [0-9]+ frister i ") }).AllAsync())
        {
            await knapp.ClickAsync();
            await Expect(knapp).ToHaveAttributeAsync("aria-expanded", "true");
        }
    }

    internal static async Task VelgKompatibilitet(Nettleser n)
    {
        await Bytt(n, "Fristliste");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Flere år og historikk", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).ToBeVisibleAsync();
    }

    private static async Task VelgAar(Nettleser n, int aar)
    {
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync(aar.ToString());
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true })).ToHaveValueAsync(aar.ToString());
    }

    private static async Task Bytt(Nettleser n, string visning)
    {
        var knapp = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Budsjettårvisning", Exact = true })
            .GetByRole(AriaRole.Button, new() { Name = visning, Exact = true });
        await knapp.ClickAsync();
        await Expect(knapp).ToHaveAttributeAsync("aria-pressed", "true");
    }

    private static async Task KontrollerIDer(ILocator flate, JsonNode?[] data)
    {
        var knapper = flate.Locator("button[data-frist-id]");
        await Expect(knapper).ToHaveCountAsync(data.Length);
        Assert.Equal(data.Select(f => (string)f!["id"]!).Order(),
            (await knapper.EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.fristId)")).Order());
    }
}
