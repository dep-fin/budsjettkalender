using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed partial class DagligEpostTester
{
    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV09_faktisk_post_gir_anonym_tastaturavmelding_som_bestaar_omstart_og_stanser_neste_dag(int bredde)
        => await Trygt(async n =>
        {
            var lenke = await EV09HentFaktiskLenke(n);
            var token = new Uri(lenke, UriKind.Absolute).Fragment[1..];
            var eier = n.Side;
            var anonym = await n.NySide(); await anonym.SetViewportSizeAsync(bredde, 844);
            await n.Gaa(lenke, anonym);
            var knapp = anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true });
            await Expect(knapp).ToBeEnabledAsync();
            Assert.False(anonym.Url.Contains('#'), "Fragmentet skal fjernes før avmeldingen.");
            var synligTekst = await anonym.Locator("body").InnerTextAsync();
            Assert.False(synligTekst.Contains(token, StringComparison.Ordinal), "Siden må ikke vise avmeldingshemmeligheten.");
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(synligTekst, @"\b[0-9A-Fa-f]{64}\b"), "Ingen hemmelig lenke skal være synlig.");
            await using (var skanning = await anonym.Context.APIRequest.HeadAsync("/epostvarsling/avmeld"))
            {
                Assert.Equal(200, skanning.Status);
                Assert.True(skanning.Headers.TryGetValue("cache-control", out var cache) && cache.Contains("no-store", StringComparison.Ordinal), "Bekreftelsessiden skal ikke mellomlagres.");
            }
            await EV09Status(eier, "Aktiv");
            Assert.True(await anonym.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Avmeldingen må passe mobilbredden.");
            await anonym.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"ev09-bekreft-{bredde}.png"), FullPage = true });
            await knapp.FocusAsync(); await anonym.Keyboard.PressAsync("Enter");
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset");
            await EV09Status(eier, "Avmeldt");
            await n.Gaa(lenke, anonym); await Expect(knapp).ToBeEnabledAsync(); await knapp.ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset");
            await n.Gaa("/epostvarsling/avmeld#" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), anonym);
            await anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true }).ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset");
            await EV09Status(eier, "Avmeldt"); n.BekreftIngenHemmelighet(token);
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00"); await n.OpprettFrist("EV09 etter avmelding", "FAG");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0); n.BekreftIngenHemmelighet(token);
        });

    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV09_gammel_postlenke_kan_ikke_stanse_ny_bekreftet_paamelding_eller_ventende_adressebytte(int bredde)
        => await Trygt(async n =>
        {
            var lenke = await EV09HentFaktiskLenke(n); var token = new Uri(lenke, UriKind.Absolute).Fragment[1..];
            var anonym = await n.NySide(); await anonym.SetViewportSizeAsync(bredde, 844);
            await n.Gaa(lenke, anonym); await anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true }).ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:01");
            await n.LoggInn("demo-fag"); await MeldPaa(n); await Bekreft(n);
            var eier = n.Side; await EV09Status(eier, "Aktiv");
            await n.Gaa(lenke, anonym); await anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true }).ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset"); await EV09Status(eier, "Aktiv");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:02");
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await n.Side.GetByLabel("Ny egen jobbadresse", new() { Exact = true }).FillAsync("ny@jd.dep.no");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter at den nye adressen er min egen jobbadresse.", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bytt jobbadresse", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            eier = n.Side; await n.Gaa(lenke, anonym); await anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true }).ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset"); await EV09Status(eier, "VenterPaaBekreftelse");
            await Bekreft(n); await EV09Status(n.Side, "Aktiv");
            await n.Gaa(lenke, anonym); await anonym.GetByRole(AriaRole.Button, new() { Name = "Bekreft avmelding", Exact = true }).ClickAsync();
            await Expect(anonym.GetByRole(AriaRole.Status)).ToContainTextAsync("videre e-postvarsling stanset"); await EV09Status(n.Side, "Aktiv"); n.BekreftIngenHemmelighet(token);
        });

    private static async Task<string> EV09HentFaktiskLenke(Nettleser n)
    {
        await Aktiver(n, "demo-fag");
        await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00"); await n.OpprettFrist("EV09 faktisk oppsummering", "FAG");
        await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
        await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        var href = await n.Side.GetByRole(AriaRole.Article).Locator("a[href*='/epostvarsling/avmeld#']").GetAttributeAsync("href");
        Assert.True(Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.AbsolutePath == "/epostvarsling/avmeld" && uri.Fragment.Length == 65, "Faktisk oppsummering skal ha en egen fragmentlenke.");
        var token = uri!.Fragment[1..];
        Assert.False((await n.Side.Locator("body").InnerTextAsync()).Contains(token, StringComparison.Ordinal), "Den kontrollerte tekstforhåndsvisningen må skjule avmeldingslenken.");
        return href!;
    }

    private static async Task EV09Status(IPage eier, string forventet)
    {
        await using var svar = await eier.Context.APIRequest.GetAsync("/api/epostabonnement");
        Assert.Equal(200, svar.Status); var json = JsonNode.Parse(await svar.TextAsync());
        Assert.Equal(forventet, (string?)json!["status"]);
    }
}
