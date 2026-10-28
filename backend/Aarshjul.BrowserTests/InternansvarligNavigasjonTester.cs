using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class InternansvarligNavigasjonTester
{
    [Theory]
    [InlineData(false, "bruker")]
    [InlineData(false, "fritekst")]
    [InlineData(false, "fjern")]
    [InlineData(true, "bruker")]
    [InlineData(true, "fritekst")]
    [InlineData(true, "fjern")]
    public Task Bare_endret_ansvar_vernes_ved_reload_og_navigasjon_kan_forkastes_eller_lagres(bool regel, string endring) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var kalender = InternkalenderAtelierTester.Kalender(n);
        var regler = InternkalenderAtelierTester.Regler(n);
        var runde = await kalender.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var id = regel
            ? await regler.OpprettAsync(new() { Tittel = "UX16 ansvar alene", Notat = "Behold dette notatet", Rundetyper = [Rundetype.Regnskap], Ansvarlige = [new("demo-sbr", "Sivert SBR")] })
            : await kalender.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16 ansvar alene", Notat = "Behold dette notatet", Ansvarlige = [new("demo-sbr", "Sivert SBR")] });
        var sti = (regel ? "/internkalender/regler/" : "/internkalender/gjoeremaal/") + id;
        await n.Gaa(sti);
        await ForventFelt(n.Side);
        await EndreAnsvar(n.Side, regel, endring);

        // Den eneste endringen er i ansvarliglisten. Nettleserens faktiske beforeunload
        // skal avvise reload uten å miste den pågående Blazor-kretsen eller feltene.
        await Reload(n.Side, forkast: false);
        await ForventFelt(n.Side);
        await ForventAnsvar(n.Side, endring, endret: true);
        Assert.Equal("demo-sbr", Assert.Single(await HentAnsvar()).BrukerId);

        var mine = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Mine gjøremål", Exact = true });
        await mine.PressAsync("Enter");
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Ulagrede endringer", Exact = true });
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Fortsett å redigere", Exact = true })).ToBeFocusedAsync();
        if (endring == "fjern") await InternkalenderAtelierTester.Bilder(n, regel ? "ansvar-regel-behold" : "ansvar-gjoeremaal-behold");
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(mine).ToBeFocusedAsync();
        await ForventFelt(n.Side);
        await ForventAnsvar(n.Side, endring, endret: true);
        await mine.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Forkast endringene", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Mine gjøremål");

        await n.Gaa(sti);
        await ForventAnsvar(n.Side, endring, endret: false);
        await EndreAnsvar(n.Side, regel, endring);
        await Reload(n.Side, forkast: true);
        await ForventFelt(n.Side);
        await ForventAnsvar(n.Side, endring, endret: false);
        Assert.Equal("demo-sbr", Assert.Single(await HentAnsvar()).BrukerId);

        // Lagre samme ansvarsendring. Ny sidehenting skal ikke varsle om ulagrert arbeid.
        await EndreAnsvar(n.Side, regel, endring);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(regel ? "Gjøremålsregler" : "Regnskap 2036");
        var lagret = await HentAnsvar();
        if (endring == "fjern") Assert.Empty(lagret);
        else
        {
            Assert.Equal(2, lagret.Count);
            Assert.Contains(lagret, a => a.BrukerId == "demo-sbr");
            Assert.Contains(lagret, a => endring == "bruker" ? a.BrukerId == "demo-admin" : a.BrukerId is null && a.Navn == "UX16 ekstra ansvar");
        }
        await n.Gaa(sti);
        await ForventFelt(n.Side);
        await ForventAnsvar(n.Side, endring, endret: true);
        var varsler = 0;
        EventHandler<IDialog> uventet = async (_, d) => { Interlocked.Increment(ref varsler); await d.AcceptAsync(); };
        n.Side.Dialog += uventet;
        try { await n.Side.ReloadAsync(); }
        finally { n.Side.Dialog -= uventet; }
        Assert.Equal(0, varsler);
        await ForventFelt(n.Side);
        await ForventAnsvar(n.Side, endring, endret: true);
        n.BekreftIngenKretsfeil();

        async Task<List<AnsvarligDto>> HentAnsvar() => regel
            ? (await regler.HentForRedigeringAsync(id))!.Ansvarlige
            : (await kalender.HentForRedigeringAsync(id))!.Ansvarlige;
    });

    private static async Task EndreAnsvar(IPage side, bool regel, string endring)
    {
        if (endring == "fjern") await side.GetByRole(AriaRole.Button, new() { Name = "Fjern Sivert SBR", Exact = true }).ClickAsync();
        else if (endring == "bruker")
        {
            await side.GetByRole(AriaRole.Combobox, new() { Name = regel ? "Fra listen" : "Legg til fra listen", Exact = true }).SelectOptionAsync("demo-admin");
            var leggTil = side.GetByRole(AriaRole.Button, new() { Name = "Legg til ansvarlig", Exact = true });
            await Expect(leggTil).ToBeEnabledAsync();
            await leggTil.ClickAsync();
        }
        else
        {
            await side.GetByLabel(regel ? "Fritekst" : "Legg til som fritekst", new() { Exact = true }).FillAsync("UX16 ekstra ansvar");
            var leggTil = side.GetByRole(AriaRole.Button, new() { Name = "Legg til navn", Exact = true });
            await Expect(leggTil).ToBeEnabledAsync();
            await leggTil.ClickAsync();
        }
        // Dette er kvitteringen fra den interaktive handlingen, uten privat komponenttilstand.
        await ForventAnsvar(side, endring, endret: true);
    }

    private static async Task ForventAnsvar(IPage side, string endring, bool endret)
    {
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Fjern Sivert SBR", Exact = true })).ToHaveCountAsync(endring == "fjern" && endret ? 0 : 1);
        await Expect(side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Fjern Dag Admin") })).ToHaveCountAsync(endring == "bruker" && endret ? 1 : 0);
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Fjern UX16 ekstra ansvar", Exact = true })).ToHaveCountAsync(endring == "fritekst" && endret ? 1 : 0);
    }

    private static async Task ForventFelt(IPage side)
    {
        await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX16 ansvar alene");
        await Expect(side.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync("Behold dette notatet");
        await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true })).ToHaveValueAsync("Ingen");
    }

    private static async Task Reload(IPage side, bool forkast)
    {
        var ferdig = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IDialog> behandle = async (_, d) =>
        {
            try
            {
                if (forkast) await d.AcceptAsync(); else await d.DismissAsync();
                ferdig.TrySetResult(d.Type);
            }
            catch (Exception e) { ferdig.TrySetException(e); }
        };
        side.Dialog += behandle;
        try
        {
            if (forkast) await side.ReloadAsync();
            else await side.EvaluateAsync("window.location.reload()"); // Avvist reload gir ingen load-hendelse.
            Assert.Equal("beforeunload", await ferdig.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { side.Dialog -= behandle; }
    }
}
