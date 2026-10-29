using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KildelenkeTester
{
    private const string Url = "https://www.regjeringen.no/no/kilde69?a=1&tekst=%22%3E%3Cscript%3E";

    [Theory]
    [InlineData(false, 1440)]
    [InlineData(true, 390)]
    public Task Administrator_lagrer_endrer_fjerner_og_bevarer_manuell_kildelenke_etter_endringsforslag(bool juster, int bredde)
        => Nettleser.Kjor(async n =>
    {
        const string tittel = "KILDE140 manuelt registrert";
        await n.LoggInn("demo-admin"); await n.Side.SetViewportSizeAsync(bredde, 1000);
        await n.Gaa("/admin/frist/ny");
        var felt = n.Side.GetByLabel("Kildelenke (valgfritt)", new() { Exact = true });
        await Expect(felt).ToBeVisibleAsync();
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2031");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await felt.FillAsync("https://regjeringen.no.example.org/avvist140");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kildelenke må være");
        await Expect(felt).ToHaveAttributeAsync("aria-invalid", "true");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        await felt.FillAsync(Url);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var frist = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        var id = (string)frist["id"]!;
        Assert.Equal(Url, (string?)frist["kildeUrl"]);
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync(); await KnyttDokument(db, tittel, "https://regjeringen.no/no/fallback140");
        await n.Gaa($"/admin/frist/{id}");
        await Expect(felt).ToHaveValueAsync(Url);
        await Expect(n.Side.Locator("form")).ToContainTextAsync("Tomt felt fjerner den manuelle lenken");
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"kilde140-admin-{bredde}.png"), FullPage = true });
        await felt.FillAsync("https://regjeringen.no/no/endret140");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        await n.Gaa($"/admin/frist/{id}");
        await Expect(felt).ToHaveValueAsync("https://regjeringen.no/no/endret140");
        await felt.FillAsync("");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        Assert.Equal("https://regjeringen.no/no/fallback140", (string?)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["kildeUrl"]);
        await n.Gaa($"/admin/frist/{id}"); await felt.FillAsync(Url);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();

        await n.LoggInn("demo-fa"); await n.Gaa($"/forslag/endre/{id}");
        await Expect(n.Side.GetByLabel("Kildelenke (valgfritt)", new() { Exact = true })).ToHaveCountAsync(0);
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Endret innhold, bevart kilde");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel }).Filter(new() { HasText = "Venter" })).ToHaveCountAsync(1);
        await n.LoggInn("demo-admin"); await n.Gaa("/admin/ko?aar=alle");
        var kort = ForslagTester.Kort(n.Side, tittel);
        if (juster)
        {
            await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByLabel("Kildelenke (valgfritt)", new() { Exact = true })).ToHaveCountAsync(0);
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        }
        else
        {
            await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        }
        await Expect(ForslagTester.Kort(n.Side, tittel)).ToHaveCountAsync(0);
        Assert.Equal(Url, (string?)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["kildeUrl"]);
        await n.LoggInn("demo-fa"); await n.Side.SetViewportSizeAsync(bredde, 1000);
        await n.Gaa("/kalender"); await KalenderTester.VelgMaaned(n, "2030-11");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Liste", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).PressAsync("Enter");
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
        var lenke = dialog.GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true });
        await Expect(lenke).ToHaveAttributeAsync("href", Url);
        await Expect(lenke).ToHaveAttributeAsync("target", "_blank");
        await Expect(lenke).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
        await Expect(lenke).ToHaveAttributeAsync("rel", "noopener noreferrer");
        await lenke.FocusAsync(); await Expect(lenke).ToBeFocusedAsync();
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"kilde140-detaljer-{bredde}.png"), FullPage = true });
        await n.Side.Keyboard.PressAsync("Escape");
        await n.Gaa("/budsjettaar");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync("2031");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true }).GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true })).ToHaveAttributeAsync("href", Url);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Synlig_kildelenke_er_trygg_for_alle_roller_og_fravaerende_uten_tillatt_kilde() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        foreach (var tittel in new[] { "felles", "manuell", "generert", "lokal PDF", "avvist", "foreldreloes" })
            await n.OpprettFrist("KILDE69 " + tittel);
        await n.OpprettFrist("KILDE69 skjult frist", "FIN-FAG");
        await n.OpprettFrist("KILDE69 POL aktivt valgt", "POL");

        // Bare syntetisk testoppsett i scenarioets disponible SQLite. Appen har ingen
        // dokumentmetadata-editor; selve atferden prøves gjennom ekte Kestrel og nettleser.
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        var skjultDokument = await KnyttDokument(db, "KILDE69 skjult frist", "https://regjeringen.no/KILDE69-skjult-url");
        await KnyttDokument(db, "KILDE69 felles", Url);
        await KnyttDokument(db, "KILDE69 lokal PDF", null);
        await KnyttDokument(db, "KILDE69 avvist", "https://regjeringen.no.example.org/KILDE69-avvist-url");
        await KnyttDokument(db, "KILDE69 POL aktivt valgt", "https://regjeringen.no/KILDE69-pol-url");
        using (var oppsett = db.CreateCommand())
        {
            oppsett.CommandText = """
                INSERT INTO FristSynlighet (FristId, GruppeKode)
                SELECT Id, 'FAG' FROM Frister WHERE Tittel LIKE 'KILDE69 %' AND Tittel NOT IN ('KILDE69 skjult frist', 'KILDE69 POL aktivt valgt');
                UPDATE Frister SET GjentaRegelId = $regel WHERE Tittel = 'KILDE69 generert';
                UPDATE Frister SET DokumentId = $mangler WHERE Tittel = 'KILDE69 foreldreloes';
                """;
            oppsett.Parameters.AddWithValue("$regel", Guid.NewGuid().ToString().ToUpperInvariant());
            oppsett.Parameters.AddWithValue("$mangler", Guid.NewGuid().ToString().ToUpperInvariant());
            await oppsett.ExecuteNonQueryAsync();
        }

        foreach (var persona in new[] { "demo-fa", "demo-fag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await KalenderTester.VelgMaaned(n, "2030-11");
            await KalenderTester.AapneFrist(n, "KILDE69 felles");
            var kort = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "KILDE69 felles", Exact = true });
            var lenke = kort.GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true });
            await Expect(lenke).ToBeVisibleAsync();
            await Expect(lenke).ToHaveAttributeAsync("href", Url);
            await Expect(lenke).ToHaveAttributeAsync("referrerpolicy", "no-referrer");
            await Expect(lenke).ToHaveAttributeAsync("target", "_blank");
            var rel = (await lenke.GetAttributeAsync("rel"))!.Split(' ');
            Assert.Contains("noopener", rel);
            Assert.Contains("noreferrer", rel);
            Assert.Contains("&amp;tekst=", await lenke.EvaluateAsync<string>("el => el.outerHTML"));
            Assert.True(await kort.EvaluateAsync<bool>("el => !!(el.querySelector('[aria-label=Kilderundskriv]').compareDocumentPosition(el.querySelector('[aria-label=\"Om fristen\"]')) & Node.DOCUMENT_POSITION_FOLLOWING)"));
            await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
            foreach (var utenKilde in new[] { "manuell", "generert", "lokal PDF", "avvist", "foreldreloes" })
            {
                await KalenderTester.AapneFrist(n, "KILDE69 " + utenKilde);
                var utenKort = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "KILDE69 " + utenKilde, Exact = true });
                await Expect(utenKort).ToBeVisibleAsync();
                await Expect(utenKort.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") })).ToHaveCountAsync(0);
                await Expect(utenKort.GetByRole(AriaRole.Region, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
                await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
            }
            if (persona != "demo-admin")
            {
                var dom = await n.Side.ContentAsync();
                foreach (var skjult in new[] { "KILDE69 skjult frist", "KILDE69-skjult-url", "KILDE69-dokumenttittel", "KILDE69-dokumentnokkel", skjultDokument, "KILDE69 POL aktivt valgt", "KILDE69-pol-url" })
                    Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
            }
        }

        await n.LoggInn("demo-pol");
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        await KalenderTester.AapneFrist(n, "KILDE69 POL aktivt valgt");
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "KILDE69 POL aktivt valgt", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Les rundskrivet på regjeringen.no (åpnes i ny fane)", Exact = true }))
            .ToHaveAttributeAsync("href", "https://regjeringen.no/KILDE69-pol-url");
        var polDom = await n.Side.ContentAsync();
        Assert.DoesNotContain("KILDE69 felles", polDom);
        Assert.DoesNotContain("/no/kilde69", polDom);
        Assert.DoesNotContain("KILDE69-skjult-url", polDom);
        n.BekreftIngenKretsfeil();
    });

    private static async Task<string> KnyttDokument(SqliteConnection db, string tittel, string? url)
    {
        var dokumentId = Guid.NewGuid().ToString().ToUpperInvariant();
        using var kommando = db.CreateCommand();
        kommando.CommandText = """
            INSERT INTO BehandledeDokumenter (Id, Kilde, DokumentNokkel, Tittel, Url, ForstSett, BehandletStatus, UttrekksForsoek)
            VALUES ($id, 'syntetisk', $nokkel, 'KILDE69-dokumenttittel', $url, '2026-10-21 00:00:00+00:00', 0, 0);
            UPDATE Frister SET DokumentId = $id WHERE Tittel = $tittel;
            """;
        kommando.Parameters.AddWithValue("$id", dokumentId);
        kommando.Parameters.AddWithValue("$nokkel", "KILDE69-dokumentnokkel-" + dokumentId);
        kommando.Parameters.AddWithValue("$url", (object?)url ?? DBNull.Value);
        kommando.Parameters.AddWithValue("$tittel", tittel);
        await kommando.ExecuteNonQueryAsync();
        return dokumentId;
    }
}
