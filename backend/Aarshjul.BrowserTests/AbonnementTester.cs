using System.Net;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AbonnementTester
{
    [Fact]
    public async Task Publikum_velges_aktivt_og_avbrutt_oppretting_lager_ingen_lenke()
    {
        try
        {
            await Nettleser.Kjor(async n =>
            {
                await n.LoggInn("demo-admin");
                await n.Gaa("/admin/abonnement");
                var gruppe = n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true });
                var opprett = n.Side.GetByRole(AriaRole.Button, new() { Name = "Opprett lenke", Exact = true });
                await Expect(gruppe).ToHaveValueAsync("");
                await Expect(opprett).ToBeDisabledAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Ingen lenker ennå", Exact = true })).ToBeVisibleAsync();
                await TokenfrieBilder(n.Side, "tom");
                await gruppe.SelectOptionAsync("POL");
                await opprett.ClickAsync();
                var dialog = n.Side.GetByRole(AriaRole.Dialog);
                await Expect(dialog).ToContainTextAsync("Politisk ledelse (POL)");
                await Expect(dialog).ToContainTextAsync("uten innlogging");
                await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeFocusedAsync();
                await TokenfrieBilder(n.Side, "opprett-bekreftelse");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
                await Expect(dialog).Not.ToBeVisibleAsync();
                await Expect(opprett).ToBeFocusedAsync();
                await Expect(gruppe).ToHaveValueAsync("POL");
                await Expect(n.Side.Locator("tbody tr")).ToHaveCountAsync(0);
                await opprett.ClickAsync();
                await Expect(dialog).ToBeVisibleAsync();
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(dialog).Not.ToBeVisibleAsync();
                await Expect(opprett).ToBeFocusedAsync();
                await n.Gaa("/admin/abonnement");
                await Expect(gruppe).ToHaveValueAsync("");
                await Expect(n.Side.Locator("tbody tr")).ToHaveCountAsync(0);
                n.BekreftIngenKretsfeil();
            });
        }
        catch (Exception)
        {
            throw new Xunit.Sdk.XunitException("Abonnementsvalget feilet. Hemmelige lenker er skjult.");
        }
    }

    [Fact]
    public async Task Anonym_feed_filtreres_oppdateres_og_tilbakekalles_fra_UI()
    {
        try
        {
            await Nettleser.Kjor(async n =>
            {
                await n.LoggInn("demo-admin");
                await n.OpprettFrist("AK feed FA", "FA");
                await n.OpprettFrist("AK feed POL", "POL");
                await n.OpprettFrist("AK feed FA senere", "FA", dato: "2031-12-01", budsjettaar: 2032);
                var frist = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK feed FA")!;
                await n.Gaa("/admin/eksport");
                await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync("FA");
                await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2030-11-20");
                await n.Side.GetByLabel("Til dato", new() { Exact = true }).FillAsync("2030-11-20");
                var fil = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned kalender (.ics)", Exact = true }).ClickAsync());
                var filsti = Path.Combine(n.Mappe, "kalender-foer-endring.ics");
                await fil.SaveAsAsync(filsti);
                var filbytes = await File.ReadAllBytesAsync(filsti);
                var filtekst = System.Text.Encoding.UTF8.GetString(filbytes);
                Assert.True(filtekst.Contains("SUMMARY:AK feed FA\r\n") && !filtekst.Contains("SUMMARY:AK feed FA senere\r\n"), "Kalenderfilas periode ble ikke bevart.");
                await n.Gaa("/admin/abonnement");
                using var anonym = new HttpClient(); // Ingen democookie.
                var lenker = new Dictionary<string, string>();
                foreach (var gruppe in new[] { "FA", "POL" })
                {
                    await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync(gruppe);
                    await n.Side.GetByRole(AriaRole.Button, new() { Name = "Opprett lenke" }).ClickAsync();
                    await Bekreft(n.Side, "Ja, opprett lenke");
                    var rad = n.Side.Locator("tbody tr").Filter(new() { HasText = $"({gruppe})" });
                    await Expect(rad.Locator("code")).ToHaveCountAsync(1);
                    var url = await rad.Locator("code").InnerTextAsync();
                    lenker[gruppe] = url;
                    using var svar = await anonym.GetAsync(url);
                    Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
                    var kalender = await svar.Content.ReadAsStringAsync();
                    Assert.True(kalender.Contains($"SUMMARY:AK feed {gruppe}\r\n"), "Gruppens frist mangler i feeden.");
                    Assert.False(kalender.Contains($"SUMMARY:AK feed {(gruppe == "FA" ? "POL" : "FA")}\r\n"), "En annen gruppes frist lekket.");
                    Assert.False(kalender.Contains("Rydde i seksjonens fellesområde"), "Internt gjøremål lekket.");
                    if (gruppe == "FA") Assert.True(kalender.Contains("SUMMARY:AK feed FA senere\r\n"), "Filperioden begrenset abonnementet.");
                }
                await n.Gaa($"/admin/frist/{frist["id"]}");
                await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK feed FA oppdatert");
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
                var oppdatert = await anonym.GetStringAsync(lenker["FA"]);
                Assert.True(oppdatert.Contains("SUMMARY:AK feed FA oppdatert\r\n"), "Feeden ble ikke oppdatert.");
                Assert.True(oppdatert.Contains($"UID:{frist["id"]}@aarshjul.fin.dep.no"), "UID ble endret ved redigering.");
                var filbytesEtter = await File.ReadAllBytesAsync(filsti);
                Assert.True(filbytes.SequenceEqual(filbytesEtter), "Den nedlastede kalenderfila endret seg.");
                Assert.False(filtekst.Contains("SUMMARY:AK feed FA oppdatert\r\n"), "Den nedlastede fila ble behandlet som et løpende abonnement.");
                await n.Gaa("/admin/abonnement");
                foreach (var gruppe in new[] { "FA", "POL" })
                {
                    var rad = n.Side.Locator("tbody tr").Filter(new() { HasText = $"({gruppe})" });
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Skru av" }).ClickAsync();
                    await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Nye feedkall vil gi 404");
                    await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
                    using (var beholdt = await anonym.GetAsync(lenker[gruppe])) Assert.Equal(HttpStatusCode.OK, beholdt.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Skru av" }).ClickAsync();
                    await Bekreft(n.Side, "Ja, skru av");
                    await Expect(rad).ToContainTextAsync("Avskrudd");
                    await Expect(rad.Locator("code")).ToHaveCountAsync(0);
                    using var av = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.NotFound, av.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Skru på" }).ClickAsync();
                    await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("Den samme lenken blir aktiv igjen");
                    await Bekreft(n.Side, "Ja, skru på");
                    await Expect(rad.GetByRole(AriaRole.Button, new() { Name = "Skru av" })).ToBeVisibleAsync();
                    Assert.True(await rad.Locator("code").InnerTextAsync() == lenker[gruppe], "Reaktivering endret den hemmelige lenken.");
                    using var paa = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.OK, paa.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
                    await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("slettes permanent");
                    await n.Side.Keyboard.PressAsync("Escape");
                    await Expect(n.Side.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
                    using (var beholdt = await anonym.GetAsync(lenker[gruppe])) Assert.Equal(HttpStatusCode.OK, beholdt.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
                    await Bekreft(n.Side, "Ja, slett permanent");
                    await Expect(rad).ToHaveCountAsync(0);
                    using var slettet = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.NotFound, slettet.StatusCode);
                }
                using var ukjent = await anonym.GetAsync(n.Adresse + "/kalender/feed/ukjent-akseptanse.ics");
                Assert.Equal(HttpStatusCode.NotFound, ukjent.StatusCode);
                n.BekreftIngenKretsfeil();
            });
        }
        catch (Exception)
        {
            // Playwright legger ved ARIA-snapshot ved feil. Denne siden inneholder bearer-token.
            // Behold feilen som feil uten URL, DOM, indre unntak eller token i TRX/CI.
            throw new Xunit.Sdk.XunitException("Abonnementsflyten feilet. Hemmelige lenker er skjult.");
        }
    }

    private static async Task Bekreft(IPage side, string tekst)
    {
        var dialog = side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = tekst, Exact = true }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(side.GetByRole(AriaRole.Status)).ToBeFocusedAsync();
    }

    internal static async Task TokenfrieBilder(IPage side, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        // Denne hjelperen brukes bare før første abonnement opprettes i disponibel database.
        Assert.True(await side.Locator("code").CountAsync() == 0, "Et tokenfritt bilde kunne ikke tas.");
        var mappe = Path.Combine(rot, "abonnement-tokenfritt");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Abonnementssiden flyter utenfor skjermen.");
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }
}
