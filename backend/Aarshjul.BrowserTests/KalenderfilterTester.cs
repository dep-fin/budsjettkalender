using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

public sealed class KalenderfilterTester
{
    [Fact]
    public Task Eksplisitte_aar_gir_union_og_tomt_utvalg_uten_aa_endre_leseporten() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var aar in new[] { 2026, 2027, 2028 })
            await n.OpprettFrist("UX78 løp " + aar, dato: "2030-11-15", budsjettaar: aar);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        foreach (var aar in new[] { 2026, 2028 })
            await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX78 løp " + aar, Exact = true })).ToBeVisibleAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX78 løp 2027", Exact = true })).ToHaveCountAsync(0);
        await Expect(Maanedskalender(n)).ToContainTextAsync("2 frister i måneden");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 2 frister", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg", Exact = true })).ToContainTextAsync("2026, 2028");
        await n.Side.GetByLabel("2026", new() { Exact = true }).UncheckAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("1 frist i måneden");
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Velg alle", Exact = true }).ClickAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("3 frister i måneden");
        // API-ens eksisterende null/tom=alle-kontrakt består uavhengig av kalenderens valg.
        Assert.Equal(3, (await n.Frister()).Count(f => ((string?)f!["tittel"])?.StartsWith("UX78 løp ") == true));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kategori_soek_periode_og_nullstilling_beholder_kontekst_gjennom_agenda_og_detaljer() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX78 Tittel med Årsoppgjør", dato: "2030-11-15", budsjettaar: 2026);
        await n.OpprettFrist("UX78 Annen tittel", dato: "2030-11-15", budsjettaar: 2028, kategori: "Gulbok", notat: "Notat med Årsoppgjør og <trygg tekst>.");
        await n.OpprettFrist("UX78 Regnskap", dato: "2030-11-15", budsjettaar: 2028, kategori: "Regnskap");
        await n.OpprettFrist("UX78 Årsoppgjør neste måned", dato: "2030-12-15", budsjettaar: 2026);
        await n.OpprettFrist("UX78 Mellomliggende løp i oktober", dato: "2030-10-15", budsjettaar: 2027);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var soek = n.Side.GetByRole(AriaRole.Searchbox, new() { Name = "Søk i tittel og notat", Exact = true });
        await soek.FillAsync("  årsOPPGJØR  ");
        await Expect(Maanedskalender(n)).ToContainTextAsync("2 frister i måneden");
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX78 Årsoppgjør neste måned", Exact = true })).ToHaveCountAsync(0);
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(aarvalg.Locator("input[type=checkbox]:checked")).ToHaveCountAsync(0);
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg", Exact = true })
            .GetByText("Budsjettår: 2026, 2028", new() { Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByLabel("Budsjett", new() { Exact = true }).UncheckAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("1 frist i måneden");
        await Dag(n, "15. november 2030").PressAsync("Enter");
        var agenda = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 15. november 2030", Exact = true });
        await Expect(agenda).ToContainTextAsync("1 frist denne dagen");
        await Expect(agenda.GetByRole(AriaRole.Button, new() { Name = "UX78 Tittel med Årsoppgjør", Exact = true })).ToHaveCountAsync(0);
        await agenda.GetByRole(AriaRole.Button, new() { Name = "UX78 Annen tittel", Exact = true }).ClickAsync();
        var detaljer = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX78 Annen tittel", Exact = true });
        await Expect(detaljer).ToContainTextAsync("<trygg tekst>");
        await Expect(detaljer.GetByRole(AriaRole.Heading, new() { Name = "UX78 Annen tittel", Exact = true })).ToBeFocusedAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(agenda).ToBeVisibleAsync();
        await Expect(agenda.GetByRole(AriaRole.Button, new() { Name = "UX78 Annen tittel", Exact = true })).ToBeFocusedAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(agenda).Not.ToBeVisibleAsync();
        await Expect(soek).ToHaveValueAsync("  årsOPPGJØR  ");
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
        await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Gul bok", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg", Exact = true })).ToContainTextAsync("årsOPPGJØR");
        var kategorier = n.Side.GetByRole(AriaRole.Group, new() { Name = "Kategori", Exact = true });
        await kategorier.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        await kategorier.GetByRole(AriaRole.Button, new() { Name = "Velg alle", Exact = true }).ClickAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("2 frister i måneden");
        await soek.FillAsync("finnes ikke");
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre", Exact = true }).ClickAsync();
        await Expect(soek).ToHaveValueAsync("");
        await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Gul bok", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("2 frister i måneden");
        await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("3 frister i måneden");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Aarsvalg_soek_og_markoerer_bygger_paa_hele_det_autoriserte_godkjente_utvalget_ogsaa_paa_mobil() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX78 Synlig hovedfrist", "FAG", dato: "2030-11-15", budsjettaar: 2026, notat: "Søkbart fellesord");
        await n.OpprettFrist("UX78 Synlig Gul bok", "FAG", dato: "2030-11-16", budsjettaar: 2027, kategori: "Gulbok", notat: "Søkbart fellesord");
        await n.OpprettFrist("UX78 Synlig annet løp", "FAG", dato: "2030-11-15", budsjettaar: 2028);
        await n.OpprettFrist("UX78 Historisk regnskap", "FAG", dato: "2020-01-15", budsjettaar: 2041, kategori: "Regnskap");
        await n.OpprettFrist("UX78 Skjult POL fellesord", "POL", dato: "2030-11-17", budsjettaar: 2042);
        await n.OpprettFrist("UX78 Skjult FINFAG fellesord", "FIN-FAG", dato: "2030-11-18", budsjettaar: 2043);
        var skjulteIder = (await n.Frister()).Where(f => ((string?)f!["tittel"])?.StartsWith("UX78 Skjult") == true)
            .Select(f => (string)f!["id"]!).ToArray();
        await n.LoggInn("demo-fa");
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX78 Upublisert fellesord");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-19");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2044");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX78 Upublisert" })).ToContainTextAsync("Venter");
        await n.LoggInn("demo-fag");
        await n.Side.SetViewportSizeAsync(390, 844);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        foreach (var aar in new[] { 2026, 2027, 2028, 2041 })
            await Expect(aarvalg.GetByRole(AriaRole.Checkbox, new() { Name = aar.ToString(), Exact = true })).ToBeCheckedAsync();
        foreach (var aar in new[] { 2042, 2043, 2044 })
            await Expect(aarvalg.GetByRole(AriaRole.Checkbox, new() { Name = aar.ToString(), Exact = true })).ToHaveCountAsync(0);
        var soek = n.Side.GetByRole(AriaRole.Searchbox, new() { Name = "Søk i tittel og notat", Exact = true });
        await soek.FillAsync("fellesord");
        await Expect(Maanedskalender(n)).ToContainTextAsync("2 frister i måneden");
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("1 frist i måneden");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 1 frist", Exact = true })).ToBeVisibleAsync();
        foreach (var dag in new[] { 16, 17, 18, 19 })
            await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = $"Velg {dag}. november 2030", Exact = true })).ToBeVisibleAsync();
        await soek.FillAsync("UX78 Skjult");
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        var dom = await n.Side.ContentAsync();
        foreach (var skjult in new[] { "UX78 Skjult POL fellesord", "UX78 Skjult FINFAG fellesord", "UX78 Upublisert fellesord" }.Concat(skjulteIder))
            Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
        foreach (var aar in new[] { 2026, 2027, 2028, 2041 })
            await Expect(aarvalg.GetByRole(AriaRole.Checkbox, new() { Name = aar.ToString(), Exact = true })).ToBeVisibleAsync();
        await VelgMaaned(n, "2040-02");
        await Expect(n.Side.GetByLabel("2041", new() { Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre", Exact = true }).ClickAsync();
        await VelgMaaned(n, "2020-01");
        await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX78 Historisk regnskap", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Group, new() { Name = "Kategori", Exact = true })
            .GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        // Fristlisten har sin tidligere tom/null=alle-kontrakt og eget historikkvalg.
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meny", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Meny", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2020-01");
        await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(Maanedskalender(n)).ToContainTextAsync("0 frister i måneden");
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        n.BekreftIngenKretsfeil();
    });
}

