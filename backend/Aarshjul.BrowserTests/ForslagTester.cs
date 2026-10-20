using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class ForslagTester
{
    internal static ILocator Kort(IPage side, string tittel)
        => side.Locator("article.kort").Filter(new() { Has = side.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true }) });

    internal static async Task Send(Nettleser n, string tittel)
    {
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2031");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
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
        await n.Gaa("/admin/ko");
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
        await n.Gaa("/admin/ko");
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
        await n.Gaa("/admin/ko");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("Endring");
        var kort = Kort(n.Side, "AK endret original");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
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
        await n.Gaa("/admin/ko");
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
        await n.Gaa("/admin/ko", gammel);
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
