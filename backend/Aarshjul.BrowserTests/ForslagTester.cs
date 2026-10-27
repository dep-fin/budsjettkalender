using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class ForslagTester
{
    [Fact]
    public Task Tom_maaned_avvises_med_bevarte_felter_og_kan_rettes_foer_innsending() => Nettleser.Kjor(async n =>
    {
        const string tittel = "UX81 tømt måned";
        const string notat = "Behold utfyllingen når måneden mangler.";
        await n.LoggInn("demo-fa");
        await n.Side.SetViewportSizeAsync(1440, 1000);
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
        var maaned = n.Side.GetByLabel("Måned", new() { Exact = true });
        await Expect(maaned).ToHaveValueAsync("2030-11");
        await maaned.FillAsync("");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2032");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Gulbok");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync("Medio");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
        var fa = n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") });
        await fa.CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        var feil = n.Side.GetByRole(AriaRole.Alert);
        await Expect(feil).ToHaveTextAsync("Velg en gyldig måned før du sender inn forslaget.");
        await Expect(feil).ToBeFocusedAsync();
        await Expect(maaned).ToHaveValueAsync("");
        await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(tittel);
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2032");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true })).ToHaveValueAsync("Gulbok");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true })).ToHaveValueAsync("Medio");
        await Expect(n.Side.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync(notat);
        await Expect(fa).ToBeCheckedAsync();
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "ux81-empty-month-desktop.png"), FullPage = true });
        await n.Side.SetViewportSizeAsync(390, 844);
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "ux81-empty-month-mobile.png"), FullPage = true });
        await n.Side.SetViewportSizeAsync(1440, 1000);
        var utfylling = n.Side;
        var mine = await n.Side.Context.NewPageAsync();
        await n.Gaa("/forslag/mine", mine);
        await Expect(mine.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel })).ToHaveCountAsync(0);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=alle");
        await Expect(Kort(n.Side, tittel)).ToHaveCountAsync(0);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        var admin = n.Side;
        n.Side = utfylling;
        await maaned.FillAsync("2030-12");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel });
        await Expect(rad).ToHaveCountAsync(1);
        await Expect(rad).ToContainTextAsync("01.12.2030");
        await Expect(rad).ToContainTextAsync("2032");
        await Expect(rad).ToContainTextAsync("Venter");
        n.Side = admin;
        await n.Gaa("/admin/ko?aar=alle");
        var kort = Kort(n.Side, tittel);
        await Expect(kort).ToHaveCountAsync(1);
        await Expect(kort).ToContainTextAsync(notat);
        await Expect(kort).ToContainTextAsync("Gul bok");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        Assert.Equal("2030-12-01", (string?)publisert["dato"]);
        Assert.Equal(1, (int)publisert["datopresisjon"]!);
        Assert.Equal(1, (int)publisert["datokvalifikator"]!);
        Assert.Equal(2032, (int)publisert["budsjettaar"]!);
        Assert.Equal(notat, (string?)publisert["notat"]);
        n.BekreftIngenKretsfeil();
    });

    internal static ILocator Kort(IPage side, string tittel)
        => side.Locator("article.kort").Filter(new() { Has = side.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true }) });

    internal static async Task Send(Nettleser n, string tittel, string gruppe = "FA")
    {
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2031");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({gruppe}\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel })).ToContainTextAsync("Venter");
    }

    [Fact]
    public Task Innsending_avvisning_varsling_revisjon_godkjenning_og_eierskap() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        await Send(n, "AK brukerforslag");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "AK brukerforslag");
        var bidrag = n.Side;
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=alle");
        var kort = Kort(n.Side, "AK brukerforslag");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Avvis", Exact = true }).ClickAsync();
        await kort.GetByLabel("Begrunnelse (valgfri)").FillAsync("AK legg til forklaring");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft avvisning" }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        n.Side = bidrag;
        await n.Gaa("/varsler");
        await Expect(n.Side.Locator(".varsel").Filter(new() { HasText = "AK legg til forklaring" })).ToContainTextAsync("avvist");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Marker alle som lest" }).ClickAsync();
        await Expect(n.Side.Locator(".varsel.ulest")).ToHaveCountAsync(0);
        await n.Side.ReloadAsync();
        await Expect(n.Side.Locator(".varsel.lest").Filter(new() { HasText = "AK legg til forklaring" })).ToHaveCountAsync(1);
        await n.Gaa("/forslag/mine");
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK brukerforslag" });
        var rediger = await rad.GetByRole(AriaRole.Link).GetAttributeAsync("href");
        await n.LoggInn("demo-finfag");
        await n.Gaa(rediger!);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Forslaget finnes ikke, eller kan ikke redigeres.");
        n.Side = bidrag;
        await n.Gaa(rediger!);
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK revidert forslag");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK revidert forslag" })).ToContainTextAsync("Venter");
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=alle");
        kort = Kort(n.Side, "AK revidert forslag");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "AK revidert forslag");
        n.Side = bidrag;
        await n.Gaa("/varsler");
        // Demoen har ett tidligere godkjenningsvarsel. Denne behandlingen legger til nøyaktig ett.
        await Expect(n.Side.Locator(".varsel").Filter(new() { HasText = "godkjent" })).ToHaveCountAsync(2);
        await Expect(n.Side.Locator(".varsel")).ToHaveCountAsync(3);
        await n.Gaa("/forslag/mine");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK revidert forslag" })).ToContainTextAsync("Godkjent");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Endringsforslag_bevarer_original_og_synlighet_til_godkjenning() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("AK original", "FA");
        var original = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK original")!;
        var id = (string)original["id"]!;
        await n.LoggInn("demo-fa");
        await n.Gaa($"/forslag/endre/{id}");
        await Expect(n.Side.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK endret original");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK endret original" })).ToContainTextAsync("Venter");
        Assert.Equal("AK original", (string?)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["tittel"]);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=alle");
        var nyFristMerker = n.Side.Locator("article.kort .kort-topp .merke").Filter(new() { HasText = "Ny frist" });
        await Expect(nyFristMerker.First).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("Endring");
        // De gamle kortene må være filtrert bort før vi åpner godkjenningen.
        await Expect(nyFristMerker).ToHaveCountAsync(0);
        var kort = Kort(n.Side, "AK endret original");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" })).ToBeVisibleAsync();
        await Expect(kort.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var endret = (await n.Frister()).Single(f => (string?)f!["id"] == id)!;
        Assert.Equal("AK endret original", (string?)endret["tittel"]);
        Assert.Equal(original["synlighet"]!.ToJsonString(), endret["synlighet"]!.ToJsonString());
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Koefilter_avbryt_justering_og_gammel_andre_fane_gir_en_publisering() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        await Send(n, "AK juster");
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=alle");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true }).SelectOptionAsync("Robot");
        await Expect(Kort(n.Side, "AK juster")).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true }).SelectOptionAsync("Bruker");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("NyFrist");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Budsjett");
        var kort = Kort(n.Side, "AK juster");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "AK juster");
        var gammel = await n.Side.Context.NewPageAsync();
        await n.Gaa("/admin/ko?aar=alle", gammel);
        var gammelKort = Kort(gammel, "AK juster");
        await gammelKort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK justert publisering");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø" })).ToBeVisibleAsync();
        await gammelKort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
        await Expect(gammelKort.Locator(".feil")).ToContainTextAsync("allerede behandlet");
        Assert.Single(await n.Frister(), f => ((string?)f!["tittel"])?.StartsWith("AK juster") == true);
        n.BekreftIngenKretsfeil();
    });
}
