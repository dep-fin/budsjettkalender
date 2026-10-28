using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DagligEpostTester
{
    [Fact]
    public async Task Paamelding_publisering_utsatt_plan_omstart_egen_oppsummering_og_avmelding()
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:30");
            await n.OpprettFrist("EV03 synlig <script>usikker()</script>", "FAG", tid: "13:45");
            await n.OpprettFrist("EV03 tentativ periode", "FAG", tentativ: true);
            await n.OpprettFrist("EV03 skjult POL", "POL");
            await SettTid(n, "2030-01-01T07:00");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Utsett kontrollert transport" }).CheckAsync();
            await Kjor(n, "0 bekreftet");
            await n.OmstartAsync(); // Samme SQLite-fil: abonnement, hendelser og utsatt plan.
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var melding = n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" });
            await Expect(melding).ToHaveCountAsync(1);
            await Expect(melding).ToContainTextAsync("EV03 synlig <script>usikker()</script>");
            await Expect(melding).ToContainTextAsync("20.11.2030 kl. 13:45");
            await Expect(melding).ToContainTextAsync("Tentativt: medio november 2030");
            await Expect(melding).ToContainTextAsync("Budsjettår 2031");
            await Expect(melding).Not.ToContainTextAsync("EV03 skjult POL");
            await Expect(melding.Locator("script")).ToHaveCountAsync(0);
            var lenke = melding.GetByRole(AriaRole.Link, new() { Name = "Åpne kalenderen (krever innlogging)" });
            await Expect(lenke).ToHaveAttributeAsync("href", n.Adresse + "/kalender");
            await lenke.ClickAsync(); await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            var anonym = await n.NySide(); await anonym.GotoAsync(n.Adresse + "/kalender");
            await Expect(anonym.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" })).ToHaveCountAsync(0);
            Assert.False(anonym.Url.EndsWith("/kalender", StringComparison.Ordinal), "Vanlig kalenderlenke må kreve innlogging.");
            await n.OmstartAsync(); // Bekreftet overlevering overlever prosessomstart.
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" })).ToHaveCountAsync(0);
            await n.Gaa("/epostvarsling");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg av", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00");
            await n.OpprettFrist("EV03 etter avmelding", "FAG");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" })).ToHaveCountAsync(0);
        });

    [Fact]
    public async Task Nye_hendelser_etter_dagens_oppsummering_venter_til_neste_Oslo_dag()
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fa");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:30");
            await n.OpprettFrist("EV03 første", "FA");
            await SettTid(n, "2030-01-01T06:59"); await Kjor(n, "Før sendetid");
            await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.OpprettFrist("EV03 neste dag", "FA");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
            await Expect(n.Side.GetByRole(AriaRole.Article)).Not.ToContainTextAsync("EV03 neste dag");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(2);
            await Expect(n.Side.GetByRole(AriaRole.Article).Nth(1)).ToContainTextAsync("EV03 neste dag");
            await Expect(n.Side.GetByRole(AriaRole.Article).Nth(1)).Not.ToContainTextAsync("EV03 første");
        });

    [Fact]
    public async Task Ubekreftet_mottaker_og_andre_brukere_faar_ingen_innhold_eller_jobbstyring()
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-fag"); await MeldPaa(n);
            await n.Side.GotoAsync("/demo/epostkjoring");
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Kjør daglig oppsummering" })).ToHaveCountAsync(0);
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00");
            await n.OpprettFrist("EV03 ikke bekreftet", "FAG");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
            await n.LoggInn("demo-pol"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
            await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true })).ToHaveCountAsync(0);
        });

    private static async Task MeldPaa(Nettleser n)
    {
        await n.Gaa("/epostvarsling");
        await n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true }).FillAsync("ev03@fin.dep.no");
        await n.Side.GetByRole(AriaRole.Checkbox).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg på", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
    }
    private static async Task Aktiver(Nettleser n, string persona)
    {
        await n.LoggInn(persona); await MeldPaa(n); await n.Gaa("/demo/epostmottaker");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" })).ToBeEnabledAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
    }
    private static async Task SettTid(Nettleser n, string tid)
    {
        await n.Gaa("/demo/epostkjoring");
        await n.Side.GetByLabel("Kontrollert UTC-tid", new() { Exact = true }).FillAsync(tid);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Sett kontrollert tid" }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Kontrollert tid er satt");
    }
    private static async Task Kjor(Nettleser n, string svar)
    {
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Kjør daglig oppsummering" }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync(svar);
    }
    private static async Task Trygt(Func<Nettleser, Task> scenario)
    {
        try { await Nettleser.Kjor(async n => { await scenario(n); n.BekreftIngenKretsfeil(); }, kontrollertEpost: true); }
        catch (Exception feil)
        {
            var sted = string.Join("\n", (feil.StackTrace ?? "").Split('\n').Where(x => x.Contains("DagligEpostTester.", StringComparison.Ordinal)));
            throw new Xunit.Sdk.XunitException($"EV03-nettleserflyten feilet ({feil.GetType().Name}). Adresse og hemmelig bekreftelse er skjult.\n{sted}");
        }
    }
}
