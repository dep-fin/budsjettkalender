using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class VentemerkeTester
{
    [Fact]
    public Task Ventemerke_er_kun_for_administrator_og_forsvinner_etter_siste_behandling_og_ny_lesing() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.OpprettFrist("VENT71 original");
        var original = (await n.Frister()).Single(f => (string?)f!["tittel"] == "VENT71 original")!;
        var id = (string)original["id"]!;
        await n.Gaa($"/admin/frist/{id}");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FAG\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var synlighet = (await n.Frister()).Single(f => (string?)f!["id"] == id)!["synlighet"]!.ToJsonString();
        await n.OpprettFrist("VENT71 uten forslag");
        await n.OpprettFrist("VENT71 skjult FINFAG", "FIN-FAG");
        await n.OpprettFrist("VENT71 aktivt valgt POL", "POL");
        var alle = await n.Frister();
        var skjultId = (string)alle.Single(f => (string?)f!["tittel"] == "VENT71 skjult FINFAG")!["id"]!;
        var polId = (string)alle.Single(f => (string?)f!["tittel"] == "VENT71 aktivt valgt POL")!["id"]!;

        await n.LoggInn("demo-fa");
        await SendEndring(n, id, "VENT71 første forslag");
        await SendEndring(n, id, "VENT71 andre forslag");
        await n.LoggInn("demo-finfag");
        await SendEndring(n, skjultId, "VENT71 skjult forslag");

        // ID-er leses bare for lekkasjekontroll. Innsending og behandling skjer i ekte UI.
        var forslagsIder = new List<string>();
        using (var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}"))
        {
            await db.OpenAsync();
            using var kommando = db.CreateCommand();
            kommando.CommandText = "SELECT Id FROM Forslag WHERE Tittel LIKE 'VENT71 %'";
            using var rader = await kommando.ExecuteReaderAsync();
            while (await rader.ReadAsync()) forslagsIder.Add(rader.GetString(0));
        }
        Assert.Equal(3, forslagsIder.Count);

        await n.LoggInn("demo-admin");
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        await KalenderTester.AapneFrist(n, "VENT71 original");
        await Expect(Detaljer(n.Side, "VENT71 original").GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(1);
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
        await KalenderTester.AapneFrist(n, "VENT71 uten forslag");
        await Expect(Detaljer(n.Side, "VENT71 uten forslag").GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);
        IngenForslagmetadata(await n.Side.ContentAsync(), forslagsIder);

        foreach (var persona in new[] { "demo-fag", "demo-fa" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await KalenderTester.VelgMaaned(n, "2030-11");
            await KalenderTester.AapneFrist(n, "VENT71 original");
            await Expect(Detaljer(n.Side, "VENT71 original")).ToBeVisibleAsync();
            await Expect(n.Side.GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);
            var dom = await n.Side.ContentAsync();
            IngenForslagmetadata(dom, forslagsIder);
            foreach (var markor in new[] { skjultId, polId, "VENT71 skjult FINFAG", "VENT71 aktivt valgt POL" })
                Assert.DoesNotContain(markor, dom, StringComparison.OrdinalIgnoreCase);
            Assert.False((bool)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["venterEndring"]!);
        }

        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/innsyn");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync("FA");
        await Expect(Kalenderkort(n.Side, "VENT71 original")).ToBeVisibleAsync();
        await Expect(Kalenderkort(n.Side, "VENT71 original").Locator(".merke-gruppe").Filter(new() { HasTextRegex = new("^FA$") })).ToBeVisibleAsync();
        await Expect(n.Side.GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);
        IngenForslagmetadata(await n.Side.ContentAsync(), forslagsIder);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync("POL");
        await Expect(Kalenderkort(n.Side, "VENT71 aktivt valgt POL")).ToBeVisibleAsync();
        await Expect(Kalenderkort(n.Side, "VENT71 original")).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);

        await n.Gaa("/admin/ko");
        var forste = ForslagTester.Kort(n.Side, "VENT71 første forslag");
        await forste.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(forste.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await forste.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
        await Expect(forste).ToHaveCountAsync(0);
        await n.Gaa("/kalender");
        await n.Side.ReloadAsync();
        await KalenderTester.VelgMaaned(n, "2030-11");
        await KalenderTester.AapneFrist(n, "VENT71 første forslag");
        await Expect(Detaljer(n.Side, "VENT71 første forslag").GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(1);
        Assert.Equal(synlighet, (await n.Frister()).Single(f => (string?)f!["id"] == id)!["synlighet"]!.ToJsonString());

        await n.Gaa("/admin/ko");
        var siste = ForslagTester.Kort(n.Side, "VENT71 andre forslag");
        await siste.GetByRole(AriaRole.Button, new() { Name = "Avvis", Exact = true }).ClickAsync();
        await siste.GetByRole(AriaRole.Button, new() { Name = "Bekreft avvisning" }).ClickAsync();
        await Expect(siste).ToHaveCountAsync(0);
        await n.Gaa("/kalender");
        await n.Side.ReloadAsync();
        await KalenderTester.VelgMaaned(n, "2030-11");
        await KalenderTester.AapneFrist(n, "VENT71 første forslag");
        await Expect(Detaljer(n.Side, "VENT71 første forslag")).ToBeVisibleAsync();
        await Expect(Detaljer(n.Side, "VENT71 første forslag").GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);
        var ferdig = (await n.Frister()).Single(f => (string?)f!["id"] == id)!;
        Assert.False((bool)ferdig["venterEndring"]!);
        Assert.Equal(synlighet, ferdig["synlighet"]!.ToJsonString());

        await n.LoggInn("demo-pol");
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        await KalenderTester.AapneFrist(n, "VENT71 aktivt valgt POL");
        await Expect(Detaljer(n.Side, "VENT71 aktivt valgt POL")).ToBeVisibleAsync();
        var polDom = await n.Side.ContentAsync();
        foreach (var markor in new[] { "VENT71 første forslag", "VENT71 original", id, skjultId })
            Assert.DoesNotContain(markor, polDom, StringComparison.OrdinalIgnoreCase);
        await Expect(n.Side.GetByText("venter endring", new() { Exact = true })).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Kalenderkort(IPage side, string tittel)
        => side.Locator(".fristkort").Filter(new() { Has = side.GetByText(tittel, new() { Exact = true }) });

    private static ILocator Detaljer(IPage side, string tittel)
        => side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });

    private static async Task SendEndring(Nettleser n, string id, string tittel)
    {
        await n.Gaa($"/forslag/endre/{id}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("VENT71-hemmelig-foreslått-notat");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel })).ToContainTextAsync("Venter");
    }

    private static void IngenForslagmetadata(string dom, IEnumerable<string> ider)
    {
        foreach (var markor in new[] { "VENT71 første forslag", "VENT71 andre forslag", "VENT71 skjult forslag",
            "VENT71-hemmelig-foreslått-notat" }.Concat(ider))
            Assert.DoesNotContain(markor, dom, StringComparison.OrdinalIgnoreCase);
    }
}
