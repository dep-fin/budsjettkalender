using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GenereringTester
{
    private static async Task Regel(Nettleser n, string loep, string tittel, string type, string parametre, bool sensitiv = false)
    {
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Funksjon / løp", new() { Exact = true }).FillAsync(loep);
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        if (type == "RelativTilMilepael")
        {
            var offset = int.Parse(parametre, CultureInfo.InvariantCulture);
            await n.Side.GetByRole(AriaRole.Radio, new() { Name = "Før eller etter en milepæl", Exact = true }).CheckAsync();
            await n.Side.GetByLabel("Ankerløp", new() { Exact = true }).FillAsync("ak-fast");
            await n.Side.GetByLabel("Retning", new() { Exact = true }).SelectOptionAsync(offset < 0 ? "for" : offset > 0 ? "etter" : "samme");
            if (offset != 0)
                await n.Side.GetByLabel("Antall kalenderdager", new() { Exact = true }).FillAsync(Math.Abs((long)offset).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            using var json = JsonDocument.Parse(parametre);
            var p = json.RootElement;
            if (type == "RelativUkedag")
            {
                await n.Side.GetByRole(AriaRole.Radio, new() { Name = "Ukedag i måneden", Exact = true }).CheckAsync();
                await n.Side.GetByLabel("Forekomst", new() { Exact = true }).SelectOptionAsync(p.GetProperty("uke").ToString());
                await n.Side.GetByLabel("Ukedag", new() { Exact = true }).SelectOptionAsync(p.GetProperty("ukedag").GetString()!);
            }
            else await n.Side.GetByLabel("Dag", new() { Exact = true }).FillAsync(p.GetProperty("dag").ToString());
            await n.Side.GetByLabel("Måned", new() { Exact = true }).SelectOptionAsync(p.GetProperty("maaned").ToString());
            if (p.TryGetProperty("aar_forskyvning", out var aar))
                await n.Side.GetByLabel("Kalenderår i forhold til budsjettåret", new() { Exact = true }).FillAsync(aar.ToString());
        }
        if (sensitiv) await n.Side.GetByLabel("Valgårssensitiv", new() { Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = tittel })).ToBeVisibleAsync();
    }

    [Fact]
    public Task Malvalidering_generering_overflyt_manuelt_anker_regenerering_og_godkjenning() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Funksjon / løp", new() { Exact = true }).FillAsync("ak-ugyldig");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK ugyldig");
        await n.Side.GetByLabel("Dag", new() { Exact = true }).FillAsync("");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Dagen finnes ikke");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Tilbake til regellisten", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forkast endringene", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
        await Regel(n, "ak-fast", "AK fast", "FastDato", "{\"maaned\":11,\"dag\":20}", sensitiv: true);
        await Regel(n, "ak-uke", "AK ukedag", "RelativUkedag", "{\"maaned\":5,\"uke\":2,\"ukedag\":\"man\"}");
        await Regel(n, "ak-relativ", "AK relativ", "RelativTilMilepael", "-7");
        await Regel(n, "ak-overflyt", "AK overflyt", "FastDato", "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":2147483647}");
        await n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "AK ukedag" }).GetByRole(AriaRole.Link, new() { Name = "Rediger" }).ClickAsync();
        await Expect(n.Side.GetByLabel("Måned", new() { Exact = true })).ToHaveValueAsync("5");
        await Expect(n.Side.GetByLabel("Forekomst", new() { Exact = true })).ToHaveValueAsync("2");
        await Expect(n.Side.GetByLabel("Ukedag", new() { Exact = true })).ToHaveValueAsync("man");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
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
        await n.Side.GetByLabel("Midlertidig dato for ak-fast", new() { Exact = true }).FillAsync("2033-11-25");
        await n.Side.GetByLabel("Midlertidig dato for ak-fast", new() { Exact = true }).PressAsync("Tab");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt med satte ankre" }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
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
