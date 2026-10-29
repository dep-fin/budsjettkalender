using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed partial class DagligEpostTester
{
    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV11_beskyttet_stans_kalender_publisering_omstart_og_gjenopptakelse_med_tastatur(int bredde)
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag"); await n.LoggInn("demo-admin");
            await n.OpprettFrist("EV11 før global stans", "FAG");
            await n.Side.SetViewportSizeAsync(bredde, 844); await n.Gaa("/admin/innstillinger/epostdrift");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Stans daglig utsending", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Bekreft driftsendring" })).ToBeFocusedAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft driftsendring", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Driftsinnstillingen er lagret");
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/kalender");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            await n.LoggInn("demo-admin"); await n.OpprettFrist("EV11 publisert under global stans", "FAG");
            await SettTid(n, "2030-01-02T07:00"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/innstillinger/epostdrift");
            await Expect(n.Side.GetByText("Daglig utsending er stanset", new() { Exact = true })).ToBeVisibleAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Gjenoppta daglig utsending", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft driftsendring", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Driftsinnstillingen er lagret");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var melding = n.Side.GetByRole(AriaRole.Article); await Expect(melding).ToHaveCountAsync(1);
            await Expect(melding).ToContainTextAsync("EV11 før global stans"); await Expect(melding).ToContainTextAsync("EV11 publisert under global stans");
        });

    [Fact]
    public async Task EV11_sikker_sendefeil_omstart_og_retry_foer_og_ved_fristen()
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag"); await n.LoggInn("demo-admin"); await n.OpprettFrist("EV11 sikker feil", "FAG");
            await SettTid(n, "2030-01-01T07:00");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Avvis sikkert før mottak", Exact = true }).CheckAsync();
            await Kjor(n, "0 bekreftet"); await n.OmstartAsync(); await n.LoggInn("demo-admin");
            await SettTid(n, "2030-01-01T07:04"); await Kjor(n, "0 bekreftet");
            await SettTid(n, "2030-01-01T07:05"); await Kjor(n, "1 bekreftet");
            await n.Gaa("/admin/innstillinger/epostdrift"); await Expect(n.Side.GetByText("Akseptert av transport", new() { Exact = false }).First).ToBeVisibleAsync();
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker"); await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        });

    [Fact]
    public async Task EV11_omstart_under_kvittering_bevarer_uavklart_og_sender_aldri_blindt_paa_nytt()
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag"); await n.LoggInn("demo-admin"); await n.OpprettFrist("EV11 mulig mottak", "FAG");
            await SettTid(n, "2030-01-01T07:00");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vent med kontrollert kvittering", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Kjør daglig oppsummering", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Kjøring pågår");
            // En annen side observerer faktisk mottak før prosessen stoppes.
            var jobbside = n.Side; await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
            await n.OmstartAsync(); await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/innstillinger/epostdrift"); await Expect(n.Side.GetByText("Uavklart", new() { Exact = false }).First).ToBeVisibleAsync();
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker"); await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        });
}
