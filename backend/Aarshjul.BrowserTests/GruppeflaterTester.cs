using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GruppeflaterTester
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public Task Escape_under_asynkron_lagring_beholder_dialog_felt_og_retry_selv_foer_ventemerket_er_levert(bool oppdater, bool feil) => Gruppeprove.Kjor(async p =>
    {
        await using var transport = await p.KontrollerLeveranse();
        var foer = await p.Grupper.HentAlleAsync();
        await p.Gaa("/admin/grupper");
        var aapner = oppdater
            ? p.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" }).GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true })
            : p.Side.GetByRole(AriaRole.Button, new() { Name = "Ny gruppe", Exact = true });
        await aapner.ClickAsync();
        var dialog = p.Side.GetByRole(AriaRole.Dialog);
        var form = dialog.GetByRole(AriaRole.Form);
        var navn = form.GetByLabel("Visningsnavn", new() { Exact = true });
        if (!oppdater) await form.GetByLabel("Kode", new() { Exact = true }).FillAsync("RASK-NY");
        await navn.FillAsync("Beholdt etter rask Escape");
        if (oppdater) await form.GetByLabel("Aktiv", new() { Exact = true }).UncheckAsync();
        var operasjon = oppdater ? "oppdater" : "opprett";
        var lagring = p.Kontroll.Neste(operasjon, feil);
        transport.Hold = true;
        await form.Locator("button[type='submit']").ClickAsync();
        await lagring.VentStart();
        await transport.Vent();
        // Tjenesten venter faktisk asynkront, men den nye renderen har ikke nådd DOM.
        await Expect(dialog).ToHaveAttributeAsync("data-arbeider", "false");
        var avbrudd = transport.NesteKvittering("AvbruttFraTastaturAsync");
        await p.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeVisibleAsync();
        transport.Slipp();
        await avbrudd;
        await Expect(form.Locator("button[type='submit']")).ToBeDisabledAsync();
        await transport.Kvittert("AvbruttFraTastaturAsync", () => p.Side.Keyboard.PressAsync("Escape"));
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog).ToHaveAttributeAsync("data-arbeider", "true");
        await p.Bilder("escape-venter-" + operasjon + (feil ? "-feil" : "-suksess"));
        await transport.Kvittert("DispatchEventAsync", () => form.DispatchEventAsync("submit"));
        Assert.Equal(1, oppdater ? p.Kontroll.Oppdateringer : p.Kontroll.Opprettinger);
        await lagring.Slipp();
        if (feil)
        {
            var melding = form.GetByRole(AriaRole.Alert);
            await Expect(melding).ToContainTextAsync("Verdiene er beholdt");
            await Expect(melding).ToBeFocusedAsync();
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(navn).ToHaveValueAsync("Beholdt etter rask Escape");
            await Expect(navn).ToBeEnabledAsync();
            if (oppdater) await Expect(form.GetByLabel("Aktiv", new() { Exact = true })).Not.ToBeCheckedAsync();
            else await Expect(form.GetByLabel("Kode", new() { Exact = true })).ToHaveValueAsync("RASK-NY");
            Assert.Equal(foer, await p.Grupper.HentAlleAsync());
            await p.Bilder("escape-beholdt-feil-" + operasjon);
            await form.Locator("button[type='submit']").ClickAsync();
        }
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(p.Side.GetByRole(AriaRole.Status)).ToContainTextAsync(oppdater ? "lagret" : "opprettet");
        var etter = await p.Grupper.HentAlleAsync();
        var kode = oppdater ? "SKOK" : "RASK-NY";
        var lagret = Assert.Single(etter, g => g.Kode == kode);
        Assert.Equal("Beholdt etter rask Escape", lagret.Navn);
        Assert.Equal(!oppdater, lagret.Aktiv);
        Assert.False(lagret.ErStandard);
        Assert.Equal(foer.Where(g => g.Kode != kode), etter.Where(g => g.Kode != kode));
        Assert.Equal(feil ? 2 : 1, oppdater ? p.Kontroll.Oppdateringer : p.Kontroll.Opprettinger);
        // Samme åpner kan brukes igjen; vanlig Escape og Avbryt har native fokusretur.
        foreach (var escape in new[] { true, false })
        {
            await aapner.PressAsync("Enter");
            await Expect(dialog).ToBeVisibleAsync();
            if (escape) await p.Side.Keyboard.PressAsync("Escape");
            else await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(aapner).ToBeFocusedAsync();
        }
        Assert.Equal(etter, await p.Grupper.HentAlleAsync());
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(390)]
    public Task Gruppefelt_feil_Escape_fokus_og_lange_rader_er_tilgjengelige(int bredde) => Gruppeprove.Kjor(async p =>
    {
        var langkode = "LANG" + new string('K', 60);
        var langnavn = "Langt avdelingsnavn " + new string('N', 230);
        await p.Grupper.OpprettAsync(langkode, langnavn);
        await p.Gaa("/admin/grupper"); await p.Side.SetViewportSizeAsync(bredde, 1000);
        var ny = p.Side.GetByRole(AriaRole.Button, new() { Name = "Ny gruppe", Exact = true });
        await ny.PressAsync("Enter");
        var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Ny gruppe", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        var kode = dialog.GetByLabel("Kode", new() { Exact = true });
        var navn = dialog.GetByLabel("Visningsnavn", new() { Exact = true });
        await kode.FillAsync("SK OK"); await navn.FillAsync(" ");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(kode).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(navn).ToHaveAttributeAsync("aria-invalid", "true");
        foreach (var felt in new[] { kode, navn })
        {
            var ider = (await felt.GetAttributeAsync("aria-describedby"))!.Split(' ');
            Assert.True(ider.Length >= 2);
            foreach (var id in ider) await Expect(dialog.Locator($"[id='{id}']")).ToBeVisibleAsync();
        }
        await Expect(kode).ToHaveValueAsync("SK OK"); await Expect(navn).ToHaveValueAsync(" ");
        await Expect(kode).ToHaveAttributeAsync("maxlength", "64"); await Expect(navn).ToHaveAttributeAsync("maxlength", "256");
        await p.Side.Keyboard.PressAsync("Escape"); await Expect(dialog).Not.ToBeVisibleAsync(); await Expect(ny).ToBeFocusedAsync();
        var rad = p.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = langkode });
        await Expect(rad).ToContainTextAsync(langnavn); await Expect(rad).ToContainTextAsync("Aktiv"); await Expect(rad).ToContainTextAsync("Egendefinert");
        var rediger = rad.GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true });
        await rediger.ScrollIntoViewIfNeededAsync(); await Expect(rediger).ToBeInViewportAsync();
        Assert.True(await p.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await rediger.PressAsync("Space");
        dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Rediger gruppe", Exact = true });
        await Expect(dialog).ToBeVisibleAsync(); await Expect(dialog.GetByLabel("Kode", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(dialog).ToContainTextAsync(langkode);
        await dialog.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Skal ikke lagres");
        await p.Side.Keyboard.PressAsync("Escape"); await Expect(dialog).Not.ToBeVisibleAsync(); await Expect(rediger).ToBeFocusedAsync();
        Assert.Equal(langnavn, (await p.Grupper.HentAlleAsync()).Single(g => g.Kode == langkode).Navn);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Teknisk_lagringsfeil_beholder_felt_og_en_arbeidslaas_hindrer_dobbel_lagring(bool oppdater) => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/grupper");
        if (oppdater) await p.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" }).GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true }).ClickAsync();
        else await p.Side.GetByRole(AriaRole.Button, new() { Name = "Ny gruppe", Exact = true }).ClickAsync();
        var dialog = p.Side.GetByRole(AriaRole.Dialog);
        var form = dialog.GetByRole(AriaRole.Form);
        if (!oppdater) await form.GetByLabel("Kode", new() { Exact = true }).FillAsync(" skok-ny ");
        await form.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Beholdt navn <sikker tekst>");
        if (oppdater) await form.GetByLabel("Aktiv", new() { Exact = true }).UncheckAsync();
        var operasjon = oppdater ? "oppdater" : "opprett";
        var feil = p.Kontroll.Neste(operasjon, feil: true);
        await form.Locator("button[type='submit']").ClickAsync(); await feil.VentStart(); await feil.Slipp();
        await Expect(form.GetByRole(AriaRole.Alert)).ToContainTextAsync("Verdiene er beholdt"); await Expect(form.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(form).Not.ToContainTextAsync("UX89-RÅ-DATABASEFEIL");
        await Expect(form.GetByLabel("Visningsnavn", new() { Exact = true })).ToHaveValueAsync("Beholdt navn <sikker tekst>");
        if (oppdater) await Expect(form.GetByLabel("Aktiv", new() { Exact = true })).Not.ToBeCheckedAsync();
        else await Expect(form.GetByLabel("Kode", new() { Exact = true })).ToHaveValueAsync(" skok-ny ");
        await p.Bilder("lagringsfeil-" + operasjon);
        var lagre = p.Kontroll.Neste(operasjon);
        await form.Locator("button[type='submit']").ClickAsync(); await lagre.VentStart();
        await Expect(form.Locator("button[type='submit']")).ToBeDisabledAsync();
        await Expect(form.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
        await form.DispatchEventAsync("submit");
        Assert.Equal(2, oppdater ? p.Kontroll.Oppdateringer : p.Kontroll.Opprettinger);
        await lagre.Slipp(); await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(p.Side.GetByRole(AriaRole.Status)).ToContainTextAsync(oppdater ? "lagret" : "opprettet");
        var grupper = await p.Grupper.HentAlleAsync();
        var gruppe = Assert.Single(grupper, g => g.Kode == (oppdater ? "SKOK" : "SKOK-NY"));
        Assert.Equal("Beholdt navn <sikker tekst>", gruppe.Navn); Assert.Equal(!oppdater, gruppe.Aktiv);
        Assert.Equal(2, oppdater ? p.Kontroll.Oppdateringer : p.Kontroll.Opprettinger);
    });

    [Fact]
    public Task Listefeil_etter_vellykket_oppretting_gir_trygg_retry_uten_ny_lagring() => Gruppeprove.Kjor(async p =>
    {
        await p.Gaa("/admin/grupper");
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Ny gruppe", Exact = true }).ClickAsync();
        var dialog = p.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Kode", new() { Exact = true }).FillAsync("NY");
        await dialog.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Ny gruppe");
        var feil = p.Kontroll.Neste("grupper", feil: true);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await feil.VentStart(); await feil.Slipp();
        await Expect(dialog).Not.ToBeVisibleAsync(); await Expect(p.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste gruppene");
        await Expect(p.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("opprettet");
        Assert.Equal(1, p.Kontroll.Opprettinger); await p.Bilder("grupper-lastfeil");
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(p.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "Ny gruppe" })).ToContainTextAsync("NY");
        Assert.Equal(1, p.Kontroll.Opprettinger); Assert.Single(await p.Grupper.HentAlleAsync(), g => g.Kode == "NY");
    });
}
