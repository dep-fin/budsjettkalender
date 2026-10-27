using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class TilgangTester
{
    [Fact]
    public Task Atelier_hovedinnganger_og_bidrag_foelger_dagens_roller() => Nettleser.Kjor(async n =>
    {
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            var nav = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true });
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Årshjul", Exact = true })).ToHaveCountAsync(0);
            await nav.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Fristliste", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
            var bidrag = persona is "demo-fa" or "demo-finfag";
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Mine forslag", Exact = true })).ToHaveCountAsync(bidrag ? 1 : 0);
            await Expect(nav.GetByRole(AriaRole.Link, new() { NameRegex = new("^Varsler") })).ToHaveCountAsync(bidrag ? 1 : 0);
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Nytt forslag", Exact = true })).ToHaveCountAsync(bidrag ? 1 : 0);
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Administrasjon", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
            if (bidrag)
            {
                await nav.GetByRole(AriaRole.Link, new() { Name = "Nytt forslag", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Foreslå en frist", Exact = true })).ToBeVisibleAsync();
                await nav.GetByRole(AriaRole.Link, new() { Name = "Mine forslag", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Mine forslag", Exact = true })).ToBeVisibleAsync();
                await nav.GetByRole(AriaRole.Link, new() { NameRegex = new("^Varsler") }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Varsler", Exact = true })).ToBeVisibleAsync();
            }
            await n.Gaa("/aarshjul");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår", Exact = true })).ToBeVisibleAsync();
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Atelier_administrasjon_samler_seks_virksomme_omraader_og_bevarer_direktelenker() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Administrasjon", Exact = true }).ClickAsync();
        var meny = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Administrasjonsområder", Exact = true });
        var omraader = new[] { "Budsjettår og frister", "Årsmal", "Grupper og innsyn", "Eksport og deling", "Internkalender", "Administrator-innstillinger" };
        Assert.Equal(omraader, await meny.GetByRole(AriaRole.Link).AllTextContentsAsync());
        foreach (var (omraade, overskrift, lenker) in new[]
        {
            (omraader[0], "Frister", new[] { ("Fristregister", "Frister"), ("Ny frist", "Ny frist"), ("Kø", "Godkjenningskø"), ("Last opp", "Last opp dokument"), ("Generér", "Generér budsjettår") }),
            (omraader[1], "Årsmal (gjentaksregler)", new[] { ("Gjentaksregler", "Årsmal (gjentaksregler)"), ("Ny gjentaksregel", "Ny malregel") }),
            (omraader[2], "Synlighetsgrupper", new[] { ("Grupper", "Synlighetsgrupper"), ("Innsyn", "Innsyn") }),
            (omraader[3], "Eksporter frister", new[] { ("Eksporter", "Eksporter frister"), ("Del kalender", "Del kalender (abonnement)"), ("Data", "Eksporter / importer data") }),
            (omraader[4], "Internkalender for SBR", new[] { ("Internrunder", "Internkalender for SBR"), ("Mine gjøremål", "Mine gjøremål"), ("Gjøremålsregler", "Generelle regler") }),
            (omraader[5], "Administrator-innstillinger", new[] { ("Innstillinger", "Administrator-innstillinger") })
        })
        {
            await meny.GetByRole(AriaRole.Link, new() { Name = omraade, Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = overskrift, Exact = true })).ToBeVisibleAsync();
            foreach (var (lenke, tittel) in lenker)
            {
                var undernav = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true });
                await undernav.GetByRole(AriaRole.Link, new() { Name = lenke, Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
                await Expect(meny.GetByRole(AriaRole.Link, new() { Name = omraade, Exact = true })).ToHaveAttributeAsync("aria-current", "location");
                await Expect(meny.Locator("a[aria-current]")).ToHaveCountAsync(1);
                await Expect(undernav.GetByRole(AriaRole.Link, new() { Name = lenke, Exact = true })).ToHaveAttributeAsync("aria-current", "page");
                await Expect(undernav.Locator("a[aria-current]")).ToHaveCountAsync(1);
            }
        }
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne dataforvaltning", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true })).ToBeVisibleAsync();
        foreach (var (sti, omraade) in new[] { ("/admin/frist/ny", omraader[0]), ("/admin/mal/ny", omraader[1]), ("/admin/innsyn", omraader[2]), ("/admin/abonnement", omraader[3]), ("/internkalender/mine", omraader[4]), ("/admin/innstillinger", omraader[5]) })
        {
            await n.Gaa(sti);
            await Expect(meny.GetByRole(AriaRole.Link, new() { Name = omraade, Exact = true })).ToHaveAttributeAsync("aria-current", "location");
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Atelier_mobilmeny_tastatur_og_fokus_bevarer_alle_innganger() => Nettleser.Kjor(async n =>
    {
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Side.SetViewportSizeAsync(390, 844);
            await n.Gaa("/");
            var menyknapp = n.Side.GetByRole(AriaRole.Button, new() { Name = "Meny", Exact = true });
            var nav = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true });
            await Expect(menyknapp).ToHaveAttributeAsync("aria-expanded", "false");
            await Expect(nav).ToBeHiddenAsync();
            await menyknapp.FocusAsync();
            await menyknapp.PressAsync("Enter");
            await Expect(menyknapp).ToHaveAttributeAsync("aria-expanded", "true");
            await Expect(nav).ToBeVisibleAsync();
            await menyknapp.PressAsync("Tab");
            var kalender = nav.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true });
            await Expect(kalender).ToBeFocusedAsync();
            Assert.True(await kalender.EvaluateAsync<bool>("e => { const s = getComputedStyle(e); return s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= 2; }"));
            await kalender.PressAsync("Escape");
            await Expect(menyknapp).ToBeFocusedAsync();
            await Expect(menyknapp).ToHaveAttributeAsync("aria-expanded", "false");
            await menyknapp.PressAsync("Enter");
            await menyknapp.PressAsync("Tab");
            await kalender.PressAsync("Tab");
            var budsjettaar = nav.GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true });
            await Expect(budsjettaar).ToBeFocusedAsync();
            await budsjettaar.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår", Exact = true })).ToBeFocusedAsync();
            await Expect(menyknapp).ToHaveAttributeAsync("aria-expanded", "false");
            await Expect(nav).ToBeHiddenAsync();
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            if (persona == "demo-admin")
            {
                await menyknapp.PressAsync("Enter");
                await nav.GetByRole(AriaRole.Link, new() { Name = "Administrasjon", Exact = true }).ClickAsync();
                var omraader = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Administrasjonsområder", Exact = true });
                foreach (var navn in new[] { "Budsjettår og frister", "Årsmal", "Grupper og innsyn", "Eksport og deling", "Internkalender", "Administrator-innstillinger" })
                {
                    var lenke = omraader.GetByRole(AriaRole.Link, new() { Name = navn, Exact = true });
                    await lenke.FocusAsync();
                    await lenke.PressAsync("Enter");
                    await Expect(lenke).ToHaveAttributeAsync("aria-current", "location");
                    await Expect(n.Side.Locator("main h1")).ToBeVisibleAsync();
                    Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
                }
                await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "atelier-admin-mobil.png"), FullPage = true });
                await n.Side.SetViewportSizeAsync(1440, 1000);
                await n.Gaa("/admin/frister");
                await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "atelier-admin-desktop.png"), FullPage = true });
            }
            else if (persona == "demo-fag")
            {
                await n.Gaa("/kalender");
                await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "atelier-leser-mobil.png"), FullPage = true });
                await n.Side.SetViewportSizeAsync(1440, 1000);
                await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "atelier-leser-desktop.png"), FullPage = true });
            }
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Demo_rollegjerder_og_utlogging_er_reelle_navigasjoner() => Nettleser.Kjor(async n =>
    {
        var stier = new[] { "/", "/kalender", "/aarshjul", "/budsjettaar", "/admin/ko", "/admin/grupper", "/admin/innstillinger", "/internkalender", "/forslag/mine", "/varsler" };
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
            foreach (var sti in new[] { "/", "/kalender", "/aarshjul", "/budsjettaar" })
            {
                await n.Gaa(sti);
                await Expect(n.Side.Locator("main h1")).ToBeVisibleAsync();
            }
            var admin = persona is "demo-admin" or "demo-sbr";
            var bidrag = persona is "demo-fa" or "demo-finfag";
            foreach (var sti in new[] { "/admin/ko", "/admin/grupper", "/admin/innstillinger", "/internkalender", "/forslag/mine", "/forslag/ny", "/varsler" })
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
