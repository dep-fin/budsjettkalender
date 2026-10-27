using Microsoft.Playwright;
using System.Text;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class ArbeidsaarTester
{
    [Fact]
    public Task Registeret_avgrenses_etter_budsjettaar_selv_med_samme_kalenderdato() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX82 arbeidsår 2031", budsjettaar: 2031);
        await n.OpprettFrist("UX82 arbeidsår 2032", budsjettaar: 2032);
        await n.Gaa("/kalender");
        await KalenderTester.VelgMaaned(n, "2030-11");
        var leseaar = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await leseaar.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(leseaar.Locator("input:checked")).ToHaveCountAsync(0);
        await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("UX82 arbeidsår");
        await Expect(KalenderperspektivTester.Utvalg(n)).ToContainTextAsync("Søk: «UX82 arbeidsår»");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Administrasjon", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Level = 1, Exact = true })).ToBeVisibleAsync();
        await Aarsvalg(n).SelectOptionAsync("2031");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 arbeidsår 2031" })).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 arbeidsår 2032" })).ToHaveCountAsync(0);
        await Expect(Aarsvalg(n)).ToHaveValueAsync("2031");
        var nav = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true });
        await Expect(nav.GetByRole(AriaRole.Link)).ToHaveTextAsync(new[] { "Frister", "Forslag", "Dokumenter", "Generer fra årsmal" });
        foreach (var (fane, sti, overskrift, felt) in new[]
        {
            ("Forslag", "/admin/ko", "Godkjenningskø", ""), ("Dokumenter", "/admin/last-opp", "Last opp dokument", "Budsjettår"),
            ("Generer fra årsmal", "/admin/generer", "Generér budsjettår", "Målbudsjettår"), ("Frister", "/admin/frister", "Frister", "")
        })
        {
            await nav.GetByRole(AriaRole.Link, new() { Name = fane, Exact = true }).ClickAsync();
            await Expect(n.Side).ToHaveURLAsync(n.Adresse + sti + "?aar=2031");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = overskrift, Level = 1, Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2031", Exact = true })).ToBeVisibleAsync();
            if (felt != "") await Expect(n.Side.GetByLabel(felt, new() { Exact = true })).ToHaveValueAsync("2031");
        }
        await Aarsvalg(n).SelectOptionAsync("2032");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 arbeidsår 2032" })).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 arbeidsår 2031" })).ToHaveCountAsync(0);
        await Aarsvalg(n).SelectOptionAsync("alle");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 arbeidsår" })).ToHaveCountAsync(2);
        await n.Side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true }).FillAsync("2045");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true }).PressAsync("Enter");
        await Expect(Aarsvalg(n)).ToHaveValueAsync("2045");
        await Expect(n.Side.GetByText("Ingen frister registrert for budsjettår 2045.", new() { Exact = true })).ToBeVisibleAsync();
        await n.Side.SetViewportSizeAsync(390, 844);
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await n.Side.SetViewportSizeAsync(1440, 1000);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("UX82 arbeidsår");
        await Expect(n.Side.GetByLabel("2031", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("2032", new() { Exact = true })).Not.ToBeCheckedAsync();
        // En vanlig direkte inngang er fortsatt gyldig og velger neste Oslo-år.
        await n.Gaa("/admin/frister");
        var idag = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        await Expect(Aarsvalg(n)).ToHaveValueAsync((idag.Year + 1).ToString());
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ny_og_redigert_frist_har_en_identitet_i_register_kalender_tidslinje_og_fristliste() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frister?aar=2031");
        var foer = (await n.Frister()).Count;
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Ny frist", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Tittel må fylles ut");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX82 samme frist");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Opprettet fra arbeidsår 2031");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).EvaluateAsync("el => { el.click(); el.click(); }");
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Fristen er lagret i budsjettår 2031");
        var opprettet = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "UX82 samme frist")!;
        Assert.Equal(foer + 1, (await n.Frister()).Count);
        await KalenderforslagTester.KontrollerVisninger(n, opprettet, "20. november 2030", "Budsjett", "Opprettet fra arbeidsår 2031");

        var id = (string)opprettet["id"]!;
        await n.Gaa($"/admin/frist/{id}?aar=2032");
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        await Expect(n.Side.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-20");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(Aarsvalg(n)).ToHaveValueAsync("2032");
        await n.Gaa($"/admin/frist/{id}?aar=2031");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX82 redigert samme frist");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2032");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Flyttet til arbeidsår 2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Aarsvalg(n)).ToHaveValueAsync("2031");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 redigert samme frist" })).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne budsjettår 2032", Exact = true }).ClickAsync();
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX82 redigert samme frist" });
        await Expect(rad).ToHaveCountAsync(1);
        await Expect(rad.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveAttributeAsync("href", $"/admin/frist/{id}?aar=2032");
        var endret = Assert.Single(await n.Frister(), f => (string?)f!["id"] == id)!;
        Assert.Equal(foer + 1, (await n.Frister()).Count);
        Assert.Equal(opprettet["synlighet"]!.ToJsonString(), endret["synlighet"]!.ToJsonString());
        await KalenderforslagTester.KontrollerVisninger(n, endret, "20. november 2030", "Budsjett", "Flyttet til arbeidsår 2032");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Dokument_og_generering_bytter_arbeidsaar_uten_gamle_resultater_og_genererte_er_atskilt_fra_koeen() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/last-opp?aar=2031");
        await n.Side.GetByLabel("Dokument (PDF)").SetInputFilesAsync(new FilePayload
        {
            Name = "ux82-avvist.txt", MimeType = "text/plain", Buffer = Encoding.UTF8.GetBytes("syntetisk fil")
        });
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kun PDF");
        await Aarsvalg(n).SelectOptionAsync("2027");
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2027");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true })).ToBeDisabledAsync();
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "godkjenningskøen", Exact = true })).ToHaveAttributeAsync("href", "/admin/ko?aar=2027");
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Generer fra årsmal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToHaveValueAsync("2027");
        await n.Side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true }).FillAsync("2043");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToHaveValueAsync("2043");
        var publisert = (await n.Frister()).Count;
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByText("Genererte", new() { Exact = false })).ToContainTextAsync("forslag for budsjettår 2043");
        var kort = n.Side.Locator("article");
        await Expect(kort.First).ToBeVisibleAsync();
        var antallGenerert = await kort.CountAsync();
        await kort.First.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2043");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(n.Side).ToHaveURLAsync(n.Adresse + "/admin/generer?aar=2043");
        await Expect(kort).ToHaveCountAsync(antallGenerert);
        await kort.First.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2043");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX82 justert generert frist");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Generér budsjettår", Exact = true })).ToBeVisibleAsync();
        await Expect(kort).ToHaveCountAsync(antallGenerert - 1);
        Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "UX82 justert generert frist");
        await n.Side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true }).FillAsync("2045");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToHaveValueAsync("2045");
        await Expect(kort).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("forslag for budsjettår 2043", new() { Exact = false })).ToHaveCountAsync(0);
        await n.Gaa("/admin/ko?aar=2043");
        await Expect(n.Side.Locator("article")).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("Ingen forslag til behandling.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(publisert + 1, (await n.Frister()).Count);
        await n.Gaa("/admin/ko?aar=alle");
        foreach (var intern in new[] { "Sammenstille satsingsforslag fra avdelingene", "Kvalitetssikre tabeller til Gul bok", "Rydde i seksjonens fellesområde" })
            await Expect(n.Side.Locator("main")).Not.ToContainTextAsync(intern);
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Aarsvalg(Nettleser n) => n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true });
}
