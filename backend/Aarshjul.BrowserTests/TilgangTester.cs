using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class TilgangTester
{
    [Fact]
    public Task Demo_rollegjerder_og_utlogging_er_reelle_navigasjoner() => Nettleser.Kjor(async n =>
    {
        var stier = new[] { "/", "/kalender", "/aarshjul", "/admin/ko", "/internkalender", "/forslag/mine", "/varsler" };
        foreach (var sti in stier)
        {
            await n.Side.GotoAsync(sti);
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
        }
        foreach (var sti in new[] { "/api/frister", "/api/eksport/frister-json", "/api/eksport/word?alt=true", "/api/eksport/ics?alt=true" })
        {
            var svar = await n.Side.Context.APIRequest.GetAsync(sti, new() { MaxRedirects = 0 });
            Assert.Contains(svar.Status, new[] { 302, 401, 403 });
            Assert.DoesNotContain("Frist for innspill", await svar.TextAsync());
        }
        foreach (var persona in new[] { "demo-admin", "demo-sbr", "demo-fa", "demo-finfag", "demo-fag", "demo-pol" })
        {
            await n.LoggInn(persona);
            foreach (var sti in new[] { "/", "/kalender", "/aarshjul" })
            {
                await n.Gaa(sti);
                await Expect(n.Side.Locator("main h1")).ToBeVisibleAsync();
            }
            var admin = persona is "demo-admin" or "demo-sbr";
            var bidrag = persona is "demo-fa" or "demo-finfag";
            foreach (var sti in new[] { "/admin/ko", "/internkalender", "/forslag/mine", "/varsler" })
            {
                await n.Side.GotoAsync(sti);
                var tillatt = sti.StartsWith("/admin") || sti == "/internkalender" ? admin : bidrag;
                if (tillatt) await Expect(n.Side.Locator("main h1")).ToBeVisibleAsync();
                else await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
            }
            if (!admin)
            {
                var svar = await n.Side.Context.APIRequest.GetAsync("/api/eksport/frister-json", new() { MaxRedirects = 0 });
                Assert.Contains(svar.Status, new[] { 302, 403 });
            }
            await n.Gaa("/kalender");
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Logg ut", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
            await n.Side.GotoAsync("/kalender");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
        }
        n.BekreftIngenKretsfeil();
    });
}
