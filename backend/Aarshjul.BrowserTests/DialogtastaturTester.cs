using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DialogtastaturTester
{
    [Fact]
    public Task Escape_bruker_synlig_dialog_og_forsinket_visning_kan_ikke_aapne_eller_stjele_fokus() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var gruppe in new[] { "FAG", "FA", "FIN-FAG" })
            await n.OpprettFrist("UX82 rask Escape " + gruppe, gruppe, dato: "2030-11-15");

        foreach (var (persona, gruppe) in new[] { ("demo-fag", "FAG"), ("demo-fa", "FA"), ("demo-finfag", "FIN-FAG") })
        {
            await n.LoggInn(persona);
            await ForsinkVisningskall(n.Side);
            await n.Gaa("/kalender");
            await KalenderTester.VelgMaaned(n, "2030-11");
            var tittel = "UX82 rask Escape " + gruppe;
            var dag = KalenderTester.Dag(n, "15. november 2030");
            var agenda = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 15. november 2030", Exact = true });
            var hendelse = agenda.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await dag.PressAsync("Enter");
            await Expect(agenda.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
            await HoldNeste(n.Side);
            try
            {
                await hendelse.PressAsync("Enter");
                await VentPaaKall(n.Side);
                await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(agenda).ToBeVisibleAsync();
                await Expect(hendelse).ToBeFocusedAsync();
                await TaBilder(n, persona);
                // Lukk agendaen før det gamle detaljkallet fullfører. Det må verken
                // gjenåpne dialogen eller flytte fokus bort fra kalenderdagen.
                await n.Side.Keyboard.PressAsync("Escape");
                await Expect(dag).ToBeFocusedAsync();
                await FrigiKall(n.Side);
                await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
                await Expect(dag).ToBeFocusedAsync();
            }
            finally { await FrigiKall(n.Side); }

            if (persona == "demo-fag") continue;
            await dag.PressAsync("Enter");
            await Expect(agenda.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
            var foreslaa = agenda.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true });
            await AvbrytForslagFoerVisning(n, foreslaa, "Foreslå en frist", agenda);
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(dag).ToBeFocusedAsync();

            var direkte = KalenderTester.Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await direkte.PressAsync("Enter");
            var detaljer = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
            await Expect(detaljer.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
            await AvbrytForslagFoerVisning(n, detaljer.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true }),
                "Foreslå endring på en publisert frist", detaljer);
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(direkte).ToBeFocusedAsync();
        }
        n.BekreftIngenKretsfeil();
    });

    private static async Task AvbrytForslagFoerVisning(Nettleser n, ILocator handling, string skjematittel, ILocator retur)
    {
        await HoldNeste(n.Side);
        try
        {
            await handling.PressAsync("Enter");
            await VentPaaKall(n.Side);
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = skjematittel, Exact = true })).ToBeVisibleAsync();
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(retur).ToBeVisibleAsync();
            await Expect(handling).ToBeFocusedAsync();
            await FrigiKall(n.Side);
            await Expect(retur).ToBeVisibleAsync();
            await Expect(handling).ToBeFocusedAsync();
        }
        finally { await FrigiKall(n.Side); }
    }

    private static Task HoldNeste(IPage side) => side.EvaluateAsync("() => window.dialogport = { vent: true }");
    private static Task VentPaaKall(IPage side) => side.WaitForFunctionAsync("() => window.dialogport.startet === true");
    private static async Task FrigiKall(IPage side)
    {
        if (!await side.EvaluateAsync<bool>("() => !!window.dialogport?.startet")) return;
        await side.EvaluateAsync("() => window.dialogport.frigi()");
        await side.WaitForFunctionAsync("() => window.dialogport.ferdig === true");
    }

    private static Task ForsinkVisningskall(IPage side) => side.RouteAsync("**/kalenderdialog*.js", async route =>
    {
        // Hold bare fullføringen av modulens offentlige vis-kall. DOM, native
        // cancel, Blazor-hendelser og den opprinnelige funksjonen kjører reelt.
        var svar = await route.FetchAsync();
        var kilde = await svar.TextAsync();
        Assert.Contains("export function vis(", kilde);
        kilde = kilde.Replace("export function vis(", "function visUtenForsinkelse(", StringComparison.Ordinal);
        await route.FulfillAsync(new() { Response = svar, Body = kilde + """

            export async function vis(...args) {
                const port = window.dialogport;
                if (!port?.vent) return visUtenForsinkelse(...args);
                port.vent = false;
                port.startet = true;
                await new Promise(resolve => port.frigi = resolve);
                try { return visUtenForsinkelse(...args); }
                finally { port.ferdig = true; }
            }
            """ });
    });

    private static async Task TaBilder(Nettleser n, string persona)
    {
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"escape-retur-{persona}-{bredde}.png"), FullPage = true });
        }
        await n.Side.SetViewportSizeAsync(1440, 1000);
    }
}
