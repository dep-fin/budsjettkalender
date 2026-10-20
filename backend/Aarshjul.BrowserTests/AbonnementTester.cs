using System.Net;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AbonnementTester
{
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
                var frist = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK feed FA")!;
                await n.Gaa("/admin/abonnement");
                using var anonym = new HttpClient(); // Ingen democookie.
                var lenker = new Dictionary<string, string>();
                foreach (var gruppe in new[] { "FA", "POL" })
                {
                    await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync(gruppe);
                    await n.Side.GetByRole(AriaRole.Button, new() { Name = "Opprett lenke" }).ClickAsync();
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
                }
                await n.Gaa($"/admin/frist/{frist["id"]}");
                await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK feed FA oppdatert");
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
                var oppdatert = await anonym.GetStringAsync(lenker["FA"]);
                Assert.True(oppdatert.Contains("SUMMARY:AK feed FA oppdatert\r\n"), "Feeden ble ikke oppdatert.");
                Assert.True(oppdatert.Contains($"UID:{frist["id"]}@aarshjul.fin.dep.no"), "UID ble endret ved redigering.");
                await n.Gaa("/admin/abonnement");
                foreach (var gruppe in new[] { "FA", "POL" })
                {
                    var rad = n.Side.Locator("tbody tr").Filter(new() { HasText = $"({gruppe})" });
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Skru av" }).ClickAsync();
                    await Expect(rad).ToContainTextAsync("Avskrudd");
                    using var av = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.NotFound, av.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Skru på" }).ClickAsync();
                    await Expect(rad.GetByRole(AriaRole.Button, new() { Name = "Skru av" })).ToBeVisibleAsync();
                    using var paa = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.OK, paa.StatusCode);
                    await rad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
                    await Expect(rad).ToHaveCountAsync(0);
                    using var slettet = await anonym.GetAsync(lenker[gruppe]);
                    Assert.Equal(HttpStatusCode.NotFound, slettet.StatusCode);
                }
                using var ukjent = await anonym.GetAsync(n.Adresse + "/kalender/feed/ukjent-akseptanse.ics");
                Assert.Equal(HttpStatusCode.NotFound, ukjent.StatusCode);
                n.BekreftIngenKretsfeil();
            });
        }
        catch (Exception e)
        {
            // Playwright legger ved ARIA-snapshot ved feil. Denne siden inneholder bearer-token.
            // Behold feilen som feil uten URL, DOM, indre unntak eller token i TRX/CI.
            throw new Xunit.Sdk.XunitException($"Abonnementsflyten feilet ({e.GetType().Name}). Hemmelige lenker er skjult.");
        }
    }
}
