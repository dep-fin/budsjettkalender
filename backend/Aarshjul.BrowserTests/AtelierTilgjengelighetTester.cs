using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AtelierTilgjengelighetTester
{
    [Fact]
    public Task Hopp_til_innhold_flytter_tastaturfokus_forbi_hovedmenyen() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Hopp til innhold", Exact = true }).FocusAsync();
        await n.Side.Keyboard.PressAsync("Enter");
        await Expect(n.Side.Locator("main:focus, main :focus")).ToHaveCountAsync(1);
        await n.Side.Keyboard.PressAsync("Tab");
        Assert.True(await n.Side.GetByRole(AriaRole.Main).EvaluateAsync<bool>("e => e.contains(document.activeElement)"));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kalenderens_veiledning_og_aarsutvalg_er_lesbare_med_vanlig_tekstkontrast() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX93 lesbar kalenderfrist", dato: "2030-11-15");
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        await Expect(n.Side.GetByText("Se fristene i kalenderen, og behold oversikten over budsjettårene.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg", Exact = true })).ToContainTextAsync("2031");
        foreach (var tekst in new[] { n.Side.Locator(".ingress"), n.Side.Locator(".kalenderutvalg").GetByText("Budsjettår:", new() { Exact = true }), n.Side.Locator(".brukernavn"), n.Side.Locator(".minidag-utenfor").First, n.Side.Locator(".kalenderdato-utenfor").First })
        {
            var kontrast = await Kontrast(tekst, "color");
            Assert.True(kontrast >= 4.5, $"Synlig vanlig tekst må ha minst 4,5:1 kontrast; målt {kontrast:F3}:1.");
        }
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Liste", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "UX93 lesbar kalenderfrist", Exact = true })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Navngitte_fristfelt_kan_fylles_og_naas_med_Tab_med_synlig_avgrensning() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frist/ny");
        var tittel = n.Side.GetByRole(AriaRole.Textbox, new() { Name = "Tittel", Exact = true });
        await tittel.FillAsync("UX93 synlige felt");
        await n.Side.Keyboard.PressAsync("Tab");
        var presisjon = n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true });
        await Expect(presisjon).ToBeFocusedAsync();
        await Expect(tittel).ToHaveValueAsync("UX93 synlige felt");
        foreach (var felt in new[] { tittel, presisjon })
        {
            var kontrast = await Kontrast(felt, "borderTopColor");
            Assert.True(kontrast >= 3, $"Skjemafelt må ha minst 3:1 kontrast mot feltflaten; målt {kontrast:F3}:1.");
        }
        n.BekreftIngenKretsfeil();
    });

    // WCAGs relative sRGB-luminans, målt på den faktisk rendrerte teksten og dens
    // første ugjennomsiktige bakgrunn. Ingen forventet farge hentes fra CSS-kilden.
    private static Task<double> Kontrast(ILocator tekst, string egenskap) => tekst.EvaluateAsync<double>("""
        (e, egenskap) => {
            const rgb = s => s.match(/[\d.]+/g).map(Number);
            const lys = c => c.slice(0,3).map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; }).reduce((v,c,i) => v + c * [.2126,.7152,.0722][i], 0);
            let b = e;
            while (b && rgb(getComputedStyle(b).backgroundColor)[3] === 0) b = b.parentElement;
            const f = lys(rgb(getComputedStyle(e)[egenskap]));
            const g = b ? lys(rgb(getComputedStyle(b).backgroundColor)) : 1;
            return (Math.max(f,g) + .05) / (Math.min(f,g) + .05);
        }
        """, egenskap);
}
