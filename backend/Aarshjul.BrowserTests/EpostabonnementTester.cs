using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class EpostabonnementTester
{
    [Fact]
    public async Task Eierskap_ny_innlogging_og_to_omstarter_bevarer_bekreftelse_status_og_avmelding()
        => await KjorSikkert(async n =>
        {
            await n.LoggInn("demo-fa"); await n.Gaa("/epostvarsling");
            await n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true }).FillAsync("omstart@fin.dep.no");
            await n.Side.GetByRole(AriaRole.Checkbox).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg på", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await n.Gaa("/demo/epostmottaker");
            var lenke = await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).GetAttributeAsync("href");
            Assert.True(!string.IsNullOrEmpty(lenke), "Kontrollert mottaker mangler bekreftelseslenke.");
            await n.OmstartAsync();
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByText("Ingen bekreftelsesmelding", new() { Exact = false })).ToBeVisibleAsync();
            await n.Gaa(lenke!);
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" })).ToBeEnabledAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("ugyldig");
            await n.LoggInn("demo-fa"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await n.Gaa(lenke!);
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" })).ToBeEnabledAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Abonnementet er aktivt");
            Assert.True(!n.Side.Url.Contains('#'), "Bekreftelsen må fjernes fra nettleserhistorikken.");
            await n.OmstartAsync(); await n.LoggInn("demo-fa"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Aktiv", new() { Exact = true })).ToBeVisibleAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg av", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
            await n.Gaa(lenke!);
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" })).ToBeEnabledAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("ugyldig");
            await n.LoggInn("demo-fa"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
        });

    [Fact]
    public async Task Mobil_og_tastatur_viser_serverfeil_beholder_felt_og_begrenser_nyutsending()
        => await KjorSikkert(async n =>
        {
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await n.Side.SetViewportSizeAsync(390, 844);
            var adresse = n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true });
            await adresse.FillAsync("mobil@fin.dep.no.example.org");
            await n.Side.GetByRole(AriaRole.Checkbox).CheckAsync();
            var meldPaa = n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg på", Exact = true });
            await meldPaa.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("én gyldig jobbadresse");
            await Expect(adresse).ToHaveValueAsync("mobil@fin.dep.no.example.org");
            await adresse.FillAsync("mobil@jd.dep.no");
            await n.Side.GetByRole(AriaRole.Checkbox).UncheckAsync();
            await meldPaa.ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Bekreft standardvalgene");
            await n.Side.GetByRole(AriaRole.Checkbox).CheckAsync(); await meldPaa.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send ny bekreftelseslenke" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Vent minst ett minutt");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Påmeldingen må passe mobilbredden.");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg av", Exact = true }).FocusAsync();
            await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
        });

    private static async Task KjorSikkert(Func<Nettleser, Task> scenario)
    {
        try { await Nettleser.Kjor(async n => { await scenario(n); n.BekreftIngenKretsfeil(); }); }
        catch (Exception feil)
        {
            // Inkluder heller ikke oppryddingsfeil, DOM, ARIA-snapshot eller indre unntak.
            throw new Xunit.Sdk.XunitException($"E-postflyten feilet ({feil.GetType().Name}). Adresse og hemmelig bekreftelse er skjult.");
        }
    }

    [Theory]
    [InlineData("demo-fag")]
    [InlineData("demo-fa")]
    [InlineData("demo-admin")]
    public async Task Egen_paamelding_bekreftelse_og_avmelding(string persona)
    {
        var steg = "innlogging";
        try
        {
            await Nettleser.Kjor(async n =>
            {
                await n.LoggInn(persona);
                await Expect(n.Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
                await n.Side.GetByRole(AriaRole.Link, new() { Name = "E-postvarsling", Exact = true }).ClickAsync();
                steg = "registrering";
                await Expect(n.Side.GetByText("Status: Ikke påmeldt", new() { Exact = true })).ToBeVisibleAsync();
                await Expect(n.Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
                steg = "adressefelt";
                await n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true }).FillAsync("ev02@FIN.DEP.NO");
                steg = "standardvalg";
                await n.Side.GetByRole(AriaRole.Checkbox).CheckAsync();
                steg = "påmeldingsknapp";
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg på", Exact = true }).ClickAsync();
                steg = "ventende status";
                await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
                steg = "testmottaker";
                await n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne kontrollert testmottaker" }).ClickAsync();
                await Expect(n.Side.GetByText("Til: ev02@fin.dep.no", new() { Exact = true })).ToBeVisibleAsync();
                await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
                steg = "bekreftelse";
                await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" })).ToBeEnabledAsync();
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
                await n.Gaa("/epostvarsling");
                steg = "avmelding";
                await Expect(n.Side.GetByText("Status: Aktiv", new() { Exact = true })).ToBeVisibleAsync();
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg av", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByText("Status: Avmeldt", new() { Exact = true })).ToBeVisibleAsync();
                n.BekreftIngenKretsfeil();
            });
        }
        catch (Exception feil)
        {
            throw new Xunit.Sdk.XunitException($"E-postflyten feilet ved {steg} ({feil.GetType().Name}). Adresse og hemmelig bekreftelse er skjult.");
        }
    }
}
