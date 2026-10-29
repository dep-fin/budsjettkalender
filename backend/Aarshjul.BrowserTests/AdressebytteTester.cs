using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed partial class DagligEpostTester
{
    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV08_adressebytte_bevarer_saervalg_og_to_omstarter_med_faktisk_dagskjoring_uten_etterslep(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:00");
            await n.OpprettFrist("EV08 grunnlag 2031", "FAG"); await n.OpprettFrist("EV08 grunnlag 2032", "FAG", budsjettaar: 2032);
            await n.LoggInn("demo-fag"); await MeldPaa(n, true, true); await n.Gaa("/demo/epostmottaker");
            var gammelLenke = await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).GetAttributeAsync("href");
            Assert.True(gammelLenke?.StartsWith("/epostvarsling/bekreft#", StringComparison.Ordinal) == true, "Bekreftelsen skal bruke fragment.");
            await Bekreft(n); await n.Gaa("/epostvarsling");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre varselvalg", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varselvalgene er lagret");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00"); await n.OpprettFrist("EV08 kølagt før bytte", "FAG");
            await n.Gaa("/demo/epostkjoring"); await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Utsett kontrollert transport" }).CheckAsync(); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(bredde, 844);
            var bytte = n.Side.GetByRole(AriaRole.Form, new() { Name = "Bytt jobbadresse", Exact = true });
            var felt = bytte.GetByLabel("Ny egen jobbadresse", new() { Exact = true });
            var samtykke = bytte.GetByRole(AriaRole.Checkbox); var knapp = bytte.GetByRole(AriaRole.Button);
            await felt.FillAsync("ny@fin.dep.no.example.org"); await samtykke.CheckAsync();
            await knapp.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("én gyldig jobbadresse");
            await Expect(felt).ToHaveValueAsync("ny@fin.dep.no.example.org");
            await felt.FillAsync("ny@jd.dep.no"); await samtykke.UncheckAsync(); await knapp.ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Bekreft at den nye adressen"); await Expect(felt).ToHaveValueAsync("ny@jd.dep.no");
            // Ulagrede varselvalg tilhører sitt eget skjema og skal ikke følge adressebyttet.
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true }).UncheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true }).UncheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2032", Exact = true }).CheckAsync();
            await samtykke.CheckAsync(); await knapp.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varslingen er stanset");
            await EV08Saervalg(n); Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Adressebyttet skal passe mobilbredden.");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send ny bekreftelseslenke", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Vent minst ett minutt");
            await n.Gaa("/demo/epostmottaker"); await Expect(n.Side.GetByText("Til: ny@jd.dep.no", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
            var nyLenke = await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).GetAttributeAsync("href");
            Assert.True(nyLenke?.StartsWith("/epostvarsling/bekreft#", StringComparison.Ordinal) == true && nyLenke != gammelLenke, "Adressebytte krever en ny fragmentlenke.");
            await n.Gaa(gammelLenke!); await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("ugyldig");
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync(); await EV08Saervalg(n);
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:01"); await n.OpprettFrist("EV08 under venting", "FAG");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa(nyLenke!); await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Abonnementet er aktivt"); Assert.False(n.Side.Url.Contains('#'), "Bekreftelsen skal fjernes fra historikken.");
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Aktiv", new() { Exact = true })).ToBeVisibleAsync(); await EV08Saervalg(n);
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00");
            await n.OpprettFrist("EV08 etter bekreftelse", "FAG"); await n.OpprettFrist("EV08 annet år", "FAG", budsjettaar: 2032);
            await n.OpprettFrist("EV08 annen kategori", "FAG", kategori: "Regnskap"); await n.OpprettFrist("EV08 skjult POL", "POL");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker"); var post = n.Side.GetByRole(AriaRole.Article);
            await Expect(post).ToHaveCountAsync(1); await Expect(post).ToContainTextAsync("Til: ny@jd.dep.no"); await Expect(post).ToContainTextAsync("Ny frist: EV08 etter bekreftelse");
            foreach (var utelatt in new[] { "kølagt før bytte", "under venting", "grunnlag", "annet år", "annen kategori", "skjult POL", "ev03@fin.dep.no" }) await Expect(post).Not.ToContainTextAsync(utelatt);
        });

    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV08_ventende_kan_bytte_men_gammel_fane_kan_ikke_gjenopplive_avmelding_og_ny_handling_kan_melde_paa_igjen(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:00");
            await n.LoggInn("demo-fag"); await MeldPaa(n, false, true);
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00");
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(bredde, 844);
            await n.Side.GetByLabel("Ny egen jobbadresse", new() { Exact = true }).FillAsync("venter@jd.dep.no");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter at den nye adressen er min egen jobbadresse.", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bytt jobbadresse", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Jobbadresse: venter@jd.dep.no", new() { Exact = true })).ToBeVisibleAsync();
            var gammel = await n.Side.Context.NewPageAsync(); await n.Gaa("/epostvarsling", gammel);
            await gammel.GetByLabel("Ny egen jobbadresse", new() { Exact = true }).FillAsync("stale@fin.dep.no");
            await gammel.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter at den nye adressen er min egen jobbadresse.", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg av", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
            await gammel.GetByRole(AriaRole.Button, new() { Name = "Bytt jobbadresse", Exact = true }).ClickAsync();
            await Expect(gammel.GetByRole(AriaRole.Alert)).ToContainTextAsync("endret samtidig");
            await Expect(gammel.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:01");
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            var skjema = n.Side.GetByRole(AriaRole.Form, new() { Name = "Bytt jobbadresse", Exact = true });
            await skjema.GetByLabel("Ny egen jobbadresse", new() { Exact = true }).FillAsync("igjen@fin.dep.no");
            await skjema.GetByRole(AriaRole.Checkbox).CheckAsync(); await skjema.GetByRole(AriaRole.Button, new() { Name = "Bytt adresse og meld meg på igjen", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true })).Not.ToBeCheckedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true })).ToBeCheckedAsync();
            await Bekreft(n); await n.Gaa("/epostvarsling"); await Expect(n.Side.GetByText("Status: Aktiv", new() { Exact = true })).ToBeVisibleAsync();
        });

    private static async Task EV08Saervalg(Nettleser n)
    {
        foreach (var navn in new[] { "Nye frister", "Vesentlige endringer", "Budsjettår 2031", "Budsjett" })
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = navn, Exact = true })).ToBeCheckedAsync();
        foreach (var navn in new[] { "Budsjettår 2032", "Alle budsjettår", "Alle kategorier" })
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = navn, Exact = true })).Not.ToBeCheckedAsync();
    }
}
