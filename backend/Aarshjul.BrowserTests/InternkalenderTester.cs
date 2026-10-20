using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class InternkalenderTester
{
    [Fact]
    public Task Internrunde_hurtiginnlegging_ansvar_fullfoer_avbryt_gjenaapne_slett_og_synk() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await n.Gaa("/internkalender");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Rundetype", Exact = true }).SelectOptionAsync("Regnskap");
        await n.Side.GetByLabel("År", new() { Exact = true }).FillAsync("2031");
        await n.Side.GetByLabel("Fyll fra generelle regler").UncheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Opprett runde" }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync("2031");
        var runde = new Uri(n.Side.Url).AbsolutePath;
        await n.Side.GetByLabel("Hurtiginnlegging", new() { Exact = false }).FillAsync("AK intern oppgave");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Legg til", Exact = true }).ClickAsync();
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK intern oppgave" });
        await Expect(rad).ToContainTextAsync("uten dato", new() { IgnoreCase = true });
        await rad.GetByRole(AriaRole.Checkbox).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(rad.GetByRole(AriaRole.Link, new() { Name = "Rediger" })).ToBeVisibleAsync();
        await rad.GetByRole(AriaRole.Link, new() { Name = "Rediger" }).ClickAsync();
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK intern redigert");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true }).SelectOptionAsync("KonkretDato");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2031-01-15");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Legg til fra listen", Exact = true }).SelectOptionAsync("demo-sbr");
        await n.Side.Locator(".skjemarad").Filter(new() { Has = n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Legg til fra listen", Exact = true }) }).GetByRole(AriaRole.Button, new() { Name = "Legg til", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK intern redigert" });
        await Expect(rad).ToContainTextAsync("Sivert SBR");
        await n.Gaa("/internkalender/mine");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("AK intern redigert");
        await n.Gaa(runde);
        await rad.GetByRole(AriaRole.Checkbox).CheckAsync();
        await Expect(rad.GetByRole(AriaRole.Button, new() { Name = "Åpne igjen" })).ToBeVisibleAsync();
        await rad.GetByRole(AriaRole.Button, new() { Name = "Åpne igjen" }).ClickAsync();
        await Expect(rad.GetByRole(AriaRole.Link, new() { Name = "Rediger" })).ToBeVisibleAsync();
        await rad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(rad).ToBeVisibleAsync();
        await rad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Ja, slett", Exact = true }).ClickAsync();
        await Expect(rad).ToHaveCountAsync(0);
        var publisert = await n.Frister();
        Assert.DoesNotContain(publisert, f => ((string?)f!["tittel"])?.StartsWith("AK intern") == true);
        await n.Gaa("/internkalender/regler/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK intern synkregel");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Regnskap", Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("AK intern synkregel");
        await n.Gaa(runde + "/synk");
        var synk = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK intern synkregel" });
        await Expect(synk).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Utfør valgte" }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK intern synkregel" })).ToHaveCountAsync(1);
        await n.Gaa(runde + "/synk");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen endringer å synkronisere");
        Assert.Equal(publisert.ToJsonString(), (await n.Frister()).ToJsonString());
        await n.Gaa("/internkalender");
        var runderad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "2031" });
        await runderad.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Ja, slett", Exact = true }).ClickAsync();
        await Expect(runderad).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });
}
