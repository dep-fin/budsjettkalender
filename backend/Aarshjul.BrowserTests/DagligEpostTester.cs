using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DagligEpostTester
{
    [Theory]
    [InlineData(false, 390)]
    [InlineData(false, 1280)]
    [InlineData(true, 390)]
    [InlineData(true, 1280)]
    public async Task EV06_koe_publiserer_ny_manuell_og_automatisk_endring_med_innboks_og_gammel_fane(bool juster, int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-fa"); await MeldPaa(n, true, true); await Bekreft(n);
            await ForslagTester.Send(n, "EV06 køfrist");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/ko?aar=alle");
            var gammel = await n.Side.Context.NewPageAsync(); await n.Gaa("/admin/ko?aar=alle", gammel);
            var gammeltKort = ForslagTester.Kort(gammel, "EV06 køfrist");
            await gammeltKort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            var kort = ForslagTester.Kort(n.Side, "EV06 køfrist");
            await n.Side.SetViewportSizeAsync(bredde, 844);
            if (juster)
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true })).ToHaveCountAsync(0);
                await n.Side.GetByLabel("Klokkeslett (valgfritt – tomt = heldags)", new() { Exact = true }).FillAsync("13:45");
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            }
            else
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
                await Expect(kort.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true })).ToHaveCountAsync(0);
                await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
                await Expect(kort).ToHaveCountAsync(0);
            }
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø", Exact = true })).ToBeVisibleAsync();
            await Expect(ForslagTester.Kort(n.Side, "EV06 køfrist")).ToHaveCountAsync(0);
            await gammeltKort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
            await Expect(gammeltKort.GetByRole(AriaRole.Alert)).ToContainTextAsync("allerede behandlet");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Ny frist: EV06 køfrist");
            await n.Gaa("/varsler"); await Expect(n.Side.Locator(".varsel").Filter(new() { HasText = "godkjent" })).ToHaveCountAsync(2);
            var maal = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "EV06 køfrist")!;
            var maalId = (string)maal["id"]!;
            await SendEndring(n, maalId, "Utvidet <arbeidsoppgave>");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/ko?aar=alle"); await n.Side.SetViewportSizeAsync(bredde, 844);
            kort = ForslagTester.Kort(n.Side, "EV06 køfrist");
            if (juster) await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
            else await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            var markering = n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true });
            await markering.FocusAsync(); await n.Side.Keyboard.PressAsync("Space");
            var bekreft = n.Side.GetByRole(AriaRole.Button, new() { Name = juster ? "Lagre" : "Bekreft godkjenning", Exact = true });
            await bekreft.ClickAsync(); await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("kort forklaring");
            if (!juster)
            {
                // Avbryt og åpne samme kort: ingen markering/forklaring skal henge igjen.
                await kort.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
                await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
                await Expect(markering).Not.ToBeCheckedAsync(); await markering.CheckAsync();
            }
            await n.Side.GetByLabel("Kort endringsforklaring", new() { Exact = true }).FillAsync("Flere <fagområder>");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Godkjenningsfeltene må passe mobilbredden.");
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"ev06-vesentlig-{juster}-{bredde}.png"), FullPage = true });
            await bekreft.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø", Exact = true })).ToBeVisibleAsync();
            await Expect(ForslagTester.Kort(n.Side, "EV06 køfrist")).ToHaveCountAsync(0);
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            var tekstpost = n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "Flere <fagområder>" });
            await Expect(tekstpost).ToHaveCountAsync(1); await Expect(tekstpost).ToContainTextAsync("Vesentlig endring: EV06 køfrist");
            await Expect(tekstpost.Locator("fagområder")).ToHaveCountAsync(0);
            await SendEndring(n, maalId, "Utvidet <arbeidsoppgave>", "2030-12-05");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-03T07:00"); await n.Gaa("/admin/ko?aar=alle");
            kort = ForslagTester.Kort(n.Side, "EV06 køfrist");
            if (juster)
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true })).Not.ToBeCheckedAsync();
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            }
            else
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
                await Expect(kort.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true })).Not.ToBeCheckedAsync();
                await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
                await Expect(kort).ToHaveCountAsync(0);
            }
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            var datopost = n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "05.12.2030" });
            await Expect(datopost).ToHaveCountAsync(1); await Expect(datopost).ToContainTextAsync("Tidligere: 20.11.2030");
            if (juster) await Expect(datopost).ToContainTextAsync("kl. 13:45");
            await n.Gaa("/varsler"); await Expect(n.Side.Locator(".varsel").Filter(new() { HasText = "godkjent" })).ToHaveCountAsync(4);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EV06_generering_regenerering_avvisning_og_POL_gir_ingen_fristpost_foer_publisering(bool juster)
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fa"); await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00");
            await n.Gaa("/admin/mal/ny");
            await n.Side.GetByLabel("Funksjon / løp", new() { Exact = true }).FillAsync("ev06-generert");
            await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("EV06 generert");
            await n.Side.GetByLabel("Dag", new() { Exact = true }).FillAsync("20");
            await n.Side.GetByLabel("Måned", new() { Exact = true }).SelectOptionAsync("11");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
            await n.Gaa("/admin/generer"); await n.Side.GetByLabel("Målbudsjettår").FillAsync("2031");
            await n.Side.GetByLabel("Målbudsjettår").PressAsync("Tab");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2031", Exact = true })).ToBeVisibleAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
            var kort = ForslagTester.Kort(n.Side, "EV06 generert"); await Expect(kort).ToBeVisibleAsync();
            Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "EV06 generert");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt med satte ankre", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
            await Expect(kort).ToBeVisibleAsync();
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.LoggInn("demo-fa"); await ForslagTester.Send(n, "EV06 avvises"); await ForslagTester.Send(n, "EV06 skjult POL", "POL");
            await n.LoggInn("demo-admin"); await n.Gaa("/admin/ko?aar=alle");
            kort = ForslagTester.Kort(n.Side, "EV06 avvises"); await kort.GetByRole(AriaRole.Button, new() { Name = "Avvis", Exact = true }).ClickAsync();
            await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft avvisning", Exact = true }).ClickAsync(); await Expect(kort).ToHaveCountAsync(0);
            kort = ForslagTester.Kort(n.Side, "EV06 skjult POL"); await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            await kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") }).CheckAsync();
            await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync(); await Expect(kort).ToHaveCountAsync(0);
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/generer?aar=2031"); kort = ForslagTester.Kort(n.Side, "EV06 generert");
            if (juster)
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
                await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            }
            else
            {
                await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
                await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
                await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync(); await Expect(kort).ToHaveCountAsync(0);
            }
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fa"); await n.Gaa("/demo/epostmottaker");
            var post = n.Side.GetByRole(AriaRole.Article); await Expect(post).ToHaveCountAsync(1);
            await Expect(post).ToContainTextAsync("Ny frist: EV06 generert");
            await Expect(post).Not.ToContainTextAsync("EV06 skjult POL"); await Expect(post).Not.ToContainTextAsync("EV06 avvises");
            await n.Gaa("/varsler"); await Expect(n.Side.Locator(".varsel").Filter(new() { HasText = "Forslaget ditt er avvist." })).ToHaveCountAsync(1);
        });

    private static async Task Bekreft(Nettleser n)
    {
        await n.Gaa("/demo/epostmottaker");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
    }

    private static async Task SendEndring(Nettleser n, string maal, string notat, string? dato = null)
    {
        await n.Gaa($"/forslag/endre/{maal}");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
        if (dato is not null) await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync(dato);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "EV06 køfrist" }).Filter(new() { HasText = "Venter" })).ToHaveCountAsync(1);
    }

    [Theory]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task EV05_selvbetjent_utvalg_bevares_og_er_uavhengig_av_kalenderen(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:00");
            await n.OpprettFrist("EV05 historisk 2031", "FAG");
            await n.OpprettFrist("EV05 historisk 2032", "FAG", budsjettaar: 2032);
            await n.OpprettFrist("EV05 skjult år", "POL", budsjettaar: 2041);
            await n.LoggInn("demo-fag"); await n.Side.SetViewportSizeAsync(bredde, 844); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Alle budsjettår", Exact = true })).ToBeCheckedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2041", Exact = true })).ToHaveCountAsync(0);
            await n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true }).FillAsync("ev05@fin.dep.no");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter at dette er min egen jobbadresse og bekrefter varselvalgene.", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meld meg på", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByText("Status: Venter på bekreftelse", new() { Exact = true })).ToBeVisibleAsync();
            await n.Gaa("/demo/epostmottaker"); await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
            await n.Side.SetViewportSizeAsync(1280, 900); await n.Gaa("/kalender");
            var kalenderfilter = n.Side.GetByRole(AriaRole.Complementary, new() { Name = "Fristutvalg", Exact = true });
            await kalenderfilter.GetByRole(AriaRole.Checkbox, new() { Name = "2032", Exact = true }).CheckAsync();
            await kalenderfilter.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true }).UncheckAsync();
            await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(bredde, 844);
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true })).ToBeCheckedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2032", Exact = true })).Not.ToBeCheckedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true })).ToBeCheckedAsync();
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"ev05-lagret-{bredde}.png"), FullPage = true });
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true })).ToBeCheckedAsync();
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:30");
            await n.OpprettFrist("EV05 relevant ny", "FAG");
            await n.OpprettFrist("EV05 utelatt kategori", "FAG", kategori: "Regnskap");
            await n.OpprettFrist("EV05 utelatt år", "FAG", budsjettaar: 2032);
            await n.OpprettFrist("EV05 fremtid før utvidelse", "FAG", budsjettaar: 2040);
            await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var melding = n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" });
            await Expect(melding).ToContainTextAsync("EV05 relevant ny"); await Expect(melding).Not.ToContainTextAsync("EV05 utelatt");
            await Expect(melding).Not.ToContainTextAsync("EV05 historisk"); await Expect(melding).Not.ToContainTextAsync("EV05 fremtid før utvidelse");
            await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(bredde, 844);
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Alle budsjettår", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Alle kategorier", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre varselvalg", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varselvalgene er lagret");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00");
            await n.OpprettFrist("EV05 nytt fremtidig år", "FAG", budsjettaar: 2042, kategori: "Regnskap");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            melding = n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" }).Filter(new() { HasText = "EV05 nytt fremtidig år" });
            await Expect(melding).ToHaveCountAsync(1);
            await Expect(melding).ToContainTextAsync("EV05 nytt fremtidig år");
            await Expect(melding).Not.ToContainTextAsync("EV05 utelatt"); await Expect(melding).Not.ToContainTextAsync("EV05 fremtid før utvidelse");
        });

    [Theory]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task EV05_endret_frist_varsles_ved_utflytting_med_lagret_aarsvalg(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:00"); await n.OpprettFrist("EV05 flyttes ut", "FAG");
            await n.LoggInn("demo-fag"); await MeldPaa(n, false, true); await n.Gaa("/epostvarsling");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre varselvalg", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varselvalgene er lagret");
            await n.Gaa("/demo/epostmottaker"); await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
            await n.LoggInn("demo-admin"); await n.Gaa("/admin/frister?aar=2031");
            await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "EV05 flyttes ut" }).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
            await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2032");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Regnskap");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
            await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Vesentlig endring: EV05 flyttes ut");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Budsjettår 2031");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Budsjettår 2032");
            await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(bredde, 844);
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjettår 2031", Exact = true })).ToBeCheckedAsync();
        });
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Varselvalg_ny_og_endret_med_foer_etter_og_varige_mobilinnstillinger(bool nye, bool endringer)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-fag"); await n.Side.SetViewportSizeAsync(390, 844);
            await MeldPaa(n, nye, endringer); await n.Gaa("/demo/epostmottaker");
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:30");
            await n.OpprettFrist("EV04 ny og endret", "FAG", tid: "13:45");
            await n.Gaa("/admin/frister?aar=2031");
            await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "EV04 ny og endret" }).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
            await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-12-05");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
            await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var melding = n.Side.GetByRole(AriaRole.Article, new() { Name = "Daglig oppsummering" });
            await Expect(melding).ToHaveCountAsync(1); await Expect(melding).ToContainTextAsync("05.12.2030 kl. 13:45");
            await Expect(melding).ToContainTextAsync(nye ? "Ny frist: EV04 ny og endret" : "Vesentlig endring: EV04 ny og endret");
            if (!nye) await Expect(melding).ToContainTextAsync("Tidligere: 20.11.2030 kl. 13:45");
            await n.Gaa("/epostvarsling"); await n.Side.SetViewportSizeAsync(390, 844);
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true })).ToBeCheckedAsync(new() { Checked = nye });
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true })).ToBeCheckedAsync(new() { Checked = endringer });
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true }).UncheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true }).UncheckAsync();
            var lagre = n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre varselvalg", Exact = true });
            await lagre.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Velg minst én varseltype");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true }).UncheckAsync();
            await lagre.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varselvalgene er lagret");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "Varselvalgene må passe mobilbredden.");
            await n.OmstartAsync(); await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true })).Not.ToBeCheckedAsync();
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true })).ToBeCheckedAsync();
        });

    [Fact]
    public async Task Administrator_maa_endre_innhold_og_begrunne_manuell_vesentlighet()
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-fag"); await MeldPaa(n, false, true); await n.Gaa("/demo/epostmottaker");
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Bekreft jobbadressen", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft min jobbadresse" }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Jobbadressen er bekreftet");
            await n.LoggInn("demo-admin"); await n.Side.SetViewportSizeAsync(390, 844); await SettTid(n, "2030-01-01T06:30");
            await n.OpprettFrist("EV04 vesentlig tekst", "FAG"); await n.Gaa("/admin/frister?aar=2031");
            await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "EV04 vesentlig tekst" }).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Marker annen faktisk innholdsendring som vesentlig", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("faktisk endret innhold");
            await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Utvidet oppgave");
            await n.Side.GetByLabel("Kort endringsforklaring", new() { Exact = true }).FillAsync("Oppgaven gjelder flere <fagområder>");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
            await SettTid(n, "2030-01-01T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Forklaring: Oppgaven gjelder flere <fagområder>");
            await Expect(n.Side.GetByRole(AriaRole.Article).Locator("fagområder")).ToHaveCountAsync(0);
        });
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

    private static async Task MeldPaa(Nettleser n, bool nye = true, bool endringer = false)
    {
        await n.Gaa("/epostvarsling");
        await n.Side.GetByLabel("Egen jobbadresse", new() { Exact = true }).FillAsync("ev03@fin.dep.no");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nye frister", Exact = true }).SetCheckedAsync(nye);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true }).SetCheckedAsync(endringer);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter at dette er min egen jobbadresse og bekrefter varselvalgene.", Exact = true }).CheckAsync();
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
