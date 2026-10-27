using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KildelenkeTester
{
    private const string Url = "https://www.regjeringen.no/no/kilde69?a=1&tekst=%22%3E%3Cscript%3E";

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
