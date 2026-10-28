using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class TidslinjefokusTester
{
    [Theory]
    [InlineData(1440, "Enter")]
    [InlineData(1440, "mus")]
    [InlineData(390, "Enter")]
    [InlineData(390, "mus")]
    public Task Rask_fristaapning_etter_gruppeutvidelse_beholder_aapnerfokus_etter_Escape(int bredde, string handling) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX86 rask tidslinjefrist", dato: "2030-11-15", budsjettaar: 2031);
        await n.OpprettFrist("UX86 annen tidslinjefrist", dato: "2030-11-16", budsjettaar: 2031);
        await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);

        // Originale websocket-bytes sendes i samme rekkefølge. Etter den synlige
        // utvidelsen holdes neste transportleveranse mens brukeren går videre.
        // Verken appens JS, Blazor eller dialogens native fokusretur erstattes.
        var utvidet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task ko = Task.CompletedTask;
        bool vent = false;
        ILocator? gruppe = null;
        await n.Side.RouteWebSocketAsync("**/_blazor**", ws =>
        {
            var server = ws.ConnectToServer();
            server.OnMessage(frame => ko = ko.ContinueWith(async forrige =>
            {
                await forrige;
                if (frame.Binary is not null) ws.Send(frame.Binary);
                else ws.Send(frame.Text!);
                if (!vent) return;
                // Vent på at nettleseren har behandlet den leverte renderen.
                await n.Side.EvaluateAsync("() => new Promise(requestAnimationFrame)");
                if (await gruppe!.GetAttributeAsync("aria-expanded") != "true") return;
                vent = false;
                utvidet.TrySetResult();
                await fortsett.Task;
            }).Unwrap());
        });
        try
        {
            await n.Gaa("/budsjettaar");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync("2031");
            var tidslinje = n.Side.GetByRole(AriaRole.Region, new() { Name = "Tidslinje for budsjettår 2031", Exact = true });
            gruppe = tidslinje.GetByRole(AriaRole.Button, new() { Name = "Vis 2 frister i november 2030", Exact = true });
            await Expect(gruppe).ToBeVisibleAsync();
            vent = true;
            if (handling == "mus") await gruppe.ClickAsync();
            else await gruppe.PressAsync("Enter");
            await utvidet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(gruppe).ToHaveAttributeAsync("aria-expanded", "true");
            var frist = tidslinje.GetByRole(AriaRole.Button, new() { Name = "UX86 rask tidslinjefrist", Exact = true });
            if (handling == "mus") await frist.ClickAsync();
            else await frist.PressAsync("Enter");
            fortsett.TrySetResult();

            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX86 rask tidslinjefrist", Exact = true });
            await Expect(dialog.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"tidslinjefokus-dialog-{bredde}-{handling}.png"), FullPage = true });
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(frist).ToBeFocusedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"tidslinjefokus-retur-{bredde}-{handling}.png"), FullPage = true });
            n.BekreftIngenKretsfeil();
        }
        finally
        {
            fortsett.TrySetResult();
            await ko;
        }
    });
}
