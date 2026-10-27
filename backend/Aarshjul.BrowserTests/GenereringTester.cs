using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GenereringTester
{
    private static async Task Regel(Nettleser n, string loep, string tittel, string type, string parametre, bool sensitiv = false)
    {
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Løp (funksjonsnavn)").FillAsync(loep);
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Regeltype", Exact = true }).SelectOptionAsync(type);
        if (type == "RelativTilMilepael")
        {
            await n.Side.GetByLabel("Milepæl (eksisterende regel)").SelectOptionAsync("ak-fast");
            await n.Side.GetByLabel("Antall dager", new() { Exact = false }).FillAsync(parametre);
        }
        else
        {
            var felt = n.Side.GetByLabel("Regelparametre (JSON)");
            // Regeltypevalget må ha vist nye standardparametre før vi erstatter dem.
            await Expect(felt).ToHaveValueAsync(type == "RelativUkedag"
                ? "{\"maaned\":3,\"uke\":2,\"ukedag\":\"man\",\"aar_forskyvning\":0}"
                : "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":0}");
            await felt.FillAsync(parametre);
        }
        if (sensitiv) await n.Side.GetByLabel("Valgårssensitiv", new() { Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel })).ToBeVisibleAsync();
    }

    [Fact]
    public Task Malvalidering_generering_overflyt_manuelt_anker_regenerering_og_godkjenning() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Løp (funksjonsnavn)").FillAsync("ak-ugyldig");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK ugyldig");
        await n.Side.GetByLabel("Regelparametre (JSON)").FillAsync("{}");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("manglende/ugyldige parameterfelter");
        await Regel(n, "ak-fast", "AK fast", "FastDato", "{\"maaned\":11,\"dag\":20}", sensitiv: true);
        await Regel(n, "ak-uke", "AK ukedag", "RelativUkedag", "{\"maaned\":5,\"uke\":2,\"ukedag\":\"man\"}");
        await Regel(n, "ak-relativ", "AK relativ", "RelativTilMilepael", "-7");
        await Regel(n, "ak-overflyt", "AK overflyt", "FastDato", "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":2147483647}");
        await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK ukedag" }).GetByRole(AriaRole.Link, new() { Name = "Rediger" }).ClickAsync();
        await Expect(n.Side.GetByLabel("Regelparametre (JSON)")).ToHaveValueAsync("{\"maaned\":5,\"uke\":2,\"ukedag\":\"man\"}");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        var publisertFoer = (await n.Frister()).Count;
        await n.Gaa("/admin/generer");
        await n.Side.GetByLabel("Målbudsjettår").FillAsync("2033");
        await n.Side.GetByLabel("Målbudsjettår").PressAsync("Tab");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2033", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("AK overflyt");
        await Expect(ForslagTester.Kort(n.Side, "AK ukedag")).ToContainTextAsync("09.05.2033");
        var antall = await n.Side.Locator("article.kort").CountAsync();
        Assert.Equal(publisertFoer, (await n.Frister()).Count);
        await n.Side.GetByLabel("AK fast (ak-fast)", new() { Exact = true }).FillAsync("2033-11-25");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt med satte ankre" }).ClickAsync();
        await Expect(ForslagTester.Kort(n.Side, "AK fast")).ToContainTextAsync("25.11.2033");
        await Expect(ForslagTester.Kort(n.Side, "AK relativ")).ToContainTextAsync("18.11.2033");
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(antall);
        var kort = ForslagTester.Kort(n.Side, "AK fast");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "AK fast");
        n.BekreftIngenKretsfeil();
    });
}
