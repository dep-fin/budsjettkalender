using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KalenderTester
{
    [Fact]
    public Task Opprett_rediger_og_valider_dag_tid_tentativ_og_aktivt_POL_valg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frist/ny");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("Tittel");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK uten synlighet");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("synlig");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.OpprettFrist("AK heldag");
        await n.OpprettFrist("AK klokkeslett", tid: "13:45");
        await n.OpprettFrist("AK tentativ", "POL", tentativ: true);
        var frister = await n.Frister();
        var heldag = frister.Single(f => (string?)f!["tittel"] == "AK heldag")!;
        var timed = frister.Single(f => (string?)f!["tittel"] == "AK klokkeslett")!;
        Assert.Null(heldag["klokkeslett"]);
        Assert.Equal("13:45:00", (string?)timed["klokkeslett"]);
        Assert.Equal(new[] { "POL" }, frister.Single(f => (string?)f!["tittel"] == "AK tentativ")!["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.Gaa($"/admin/frist/{heldag["id"]}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK heldag redigert");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK heldag redigert" })).ToBeVisibleAsync();
        await n.LoggInn("demo-pol");
        Assert.Single(await n.Frister(), f => ((string?)f!["tittel"])?.StartsWith("AK ") == true);
        await n.Gaa("/kalender");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("AK tentativ");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("medio");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kalender_aarshjul_filter_historikk_og_personasett_stemmer_med_serverrespons() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var gruppe in new[] { "FA", "FIN-FAG", "POL" }) await n.OpprettFrist($"AK bare {gruppe}", gruppe);
        foreach (var (persona, tillatt) in new[] { ("demo-fa", "FA"), ("demo-finfag", "FIN-FAG"), ("demo-pol", "POL"), ("demo-fag", "ingen") })
        {
            await n.LoggInn(persona);
            var data = await n.Frister();
            var egne = data.Where(f => ((string?)f!["tittel"])?.StartsWith("AK bare ") == true).Select(f => (string)f!["tittel"]!).ToArray();
            Assert.Equal(tillatt == "ingen" ? [] : new[] { $"AK bare {tillatt}" }, egne);
            await n.Gaa("/kalender");
            await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
            await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
            foreach (var f in data) await Expect(n.Side.Locator("main")).ToContainTextAsync((string)f!["tittel"]!);
            // Intern navigasjon beholder kretsens filtervalg.
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Årshjul", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).ToBeCheckedAsync();
            foreach (var f in data) await Expect(n.Side.Locator("main")).ToContainTextAsync((string)f!["tittel"]!);
            await n.Side.GetByLabel("Budsjett", new() { Exact = true }).UncheckAsync();
            await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
            if (data.Any(f => (int)f!["kategori"]! == 2))
                await Expect(n.Side.Locator(".hjul-tittel").First).ToBeVisibleAsync();
            else await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen frister i utvalget ditt.");
            await n.Side.ReloadAsync();
            await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
            await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).ToBeCheckedAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa("/kalender");
        await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator(".fristliste")).ToHaveCountAsync(1);
        await Expect(n.Side.Locator(".fristkort")).ToHaveCountAsync(3);
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "kalender.png"), FullPage = true });
        n.BekreftIngenKretsfeil();
    });
}
