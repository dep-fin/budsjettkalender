using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KalenderTester
{
    [Fact]
    public Task Maaned_og_minikalender_viser_mandag_foerst_og_ISO_uke_over_aarsskiftet() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fag");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2021-01");
        var kalender = Maanedskalender(n);
        Assert.Equal(new[] { "Uke", "Man", "Tir", "Ons", "Tor", "Fre", "Lør", "Søn" },
            await kalender.GetByRole(AriaRole.Columnheader).AllTextContentsAsync());
        Assert.Equal(new[] { "Man", "Tir", "Ons", "Tor", "Fre", "Lør", "Søn" },
            await Minikalender(n).GetByRole(AriaRole.Columnheader).AllTextContentsAsync());
        await Expect(kalender.GetByRole(AriaRole.Rowheader, new() { Name = "Uke 53, 2020", Exact = true })).ToBeVisibleAsync();
        await Expect(kalender.GetByRole(AriaRole.Rowheader, new() { Name = "Uke 1, 2021", Exact = true })).ToBeVisibleAsync();
        await Expect(kalender.GetByRole(AriaRole.Button, new() { Name = "Åpne dagsagenda for 1. januar 2021", Exact = true })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Maanedslengde_seksukersmaaned_aarsskifte_og_datogrenser_kan_navigeres() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fag");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2028-02");
        await Expect(Dag(n, "29. februar 2028")).ToBeVisibleAsync();
        await VelgMaaned(n, "2027-02");
        await Expect(Dag(n, "28. februar 2027")).ToBeVisibleAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("29\\. februar") })).ToHaveCountAsync(0);
        await VelgMaaned(n, "2026-08");
        Assert.Equal(new[] { "Uke 31, 2026", "Uke 32, 2026", "Uke 33, 2026", "Uke 34, 2026", "Uke 35, 2026", "Uke 36, 2026" },
            await Maanedskalender(n).GetByRole(AriaRole.Rowheader).EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('aria-label'))"));
        await VelgMaaned(n, "2030-12");
        await Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "Neste måned", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2031-01");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 1. januar 2031", Exact = true })).ToBeVisibleAsync();
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Forrige måned", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-12");
        await VelgMaaned(n, "0001-01");
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "Forrige måned", Exact = true })).ToBeDisabledAsync();
        await Dag(n, "1. januar 0001").ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 1. januar 0001", Exact = true })).ToBeVisibleAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await VelgMaaned(n, "9999-12");
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "Neste måned", Exact = true })).ToBeDisabledAsync();
        await Dag(n, "31. desember 9999").ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 31. desember 9999", Exact = true })).ToBeVisibleAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Minikalender_I_dag_og_tomme_dager_beholder_filter_ogsaa_ved_avbrutt_dialoglasting() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX77 historisk kalenderdag", dato: "2020-01-01", budsjettaar: 2031);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2020-01");
        await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX77 historisk kalenderdag", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).ToHaveCountAsync(0);
        // Brukeren kan forlate Kalender mens dialogens første modulimport venter på nettet.
        var ventendeImport = new TaskCompletionSource<IRoute>(TaskCreationOptions.RunContinuationsAsynchronously);
        await n.Side.RouteAsync("**/kalenderdialog*.js", r =>
        {
            ventendeImport.TrySetResult(r);
            return Task.CompletedTask;
        });
        await Dag(n, "15. januar 2020").ClickAsync();
        var import = await ventendeImport.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("dialog[aria-labelledby='fristdialog-tittel']")).ToHaveCountAsync(0);
        await import.ContinueAsync();
        await n.Side.UnrouteAsync("**/kalenderdialog*.js");
        await n.Side.EvaluateAsync("() => import('./kalenderdialog.js')");
        await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("UX77 historisk kalenderdag");
        await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).UncheckAsync();
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await VelgMaaned(n, "2020-01");
        n.BekreftIngenKretsfeil();
        var mini = Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. januar 2020", Exact = true });
        await mini.ClickAsync();
        await Expect(mini).ToHaveAttributeAsync("aria-pressed", "true");
        await Dag(n, "15. januar 2020").PressAsync("Enter");
        var tom = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 15. januar 2020", Exact = true });
        await Expect(tom).ToContainTextAsync("Ingen frister");
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(Dag(n, "15. januar 2020")).ToBeFocusedAsync();
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX77 historisk kalenderdag");
        await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("UX77 historisk kalenderdag");
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
            .GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("2031", new() { Exact = true })).ToBeCheckedAsync();
        var idag = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        await Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "I dag", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync(idag.ToString("yyyy-MM"));
        await Expect(n.Side.GetByLabel("2031", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { NameRegex = new("^Velg " + idag.ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("nb-NO"))) })).ToBeVisibleAsync();
        await VelgMaaned(n, "2040-02");
        await Expect(Maanedskalender(n)).ToContainTextAsync("Ingen frister");
        await Dag(n, "1. februar 2040").ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 1. februar 2040", Exact = true })).ToContainTextAsync("Ingen frister");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Travel_dag_gir_alle_frister_heldag_samtidige_og_tidlige_sene_tidspunkt_med_fokusretur() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string fullTittel = "UX77 En fullstendig og lang fristtittel som må kunne leses uten avkorting i fristdetaljene";
        await n.OpprettFrist(fullTittel, dato: "2030-11-15", notat: "Hele notatet om fristen skal være tilgjengelig.", kategori: "Gulbok");
        foreach (var (tittel, tid) in new[] { ("UX77 svært tidlig", "00:05"), ("UX77 samtidig A", "13:45"), ("UX77 samtidig B", "13:45"), ("UX77 svært sent", "23:55") })
            await n.OpprettFrist(tittel, tid: tid, dato: "2030-11-15");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
        var direkte = Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = fullTittel, Exact = true });
        await direkte.FocusAsync();
        await direkte.PressAsync("Enter");
        var detaljer = n.Side.GetByRole(AriaRole.Dialog, new() { Name = fullTittel, Exact = true });
        await Expect(detaljer).ToBeVisibleAsync();
        foreach (var tekst in new[] { fullTittel, "15. november 2030", "2031", "Gul bok", "Hele notatet om fristen skal være tilgjengelig.", "Heldag" })
            await Expect(detaljer).ToContainTextAsync(tekst);
        await Expect(detaljer.GetByRole(AriaRole.Heading, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
        await n.Side.Keyboard.PressAsync("Tab");
        Assert.True(await detaljer.EvaluateAsync<bool>("el => el.contains(document.activeElement)"));
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(direkte).ToBeFocusedAsync();
        var alle = Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "Vis alle 5 frister for 15. november 2030", Exact = true });
        await alle.FocusAsync();
        await alle.PressAsync("Space");
        var agenda = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 15. november 2030", Exact = true });
        foreach (var tekst in new[] { "Heldag", "00:05", "13:45", "23:55" }) await Expect(agenda).ToContainTextAsync(tekst);
        foreach (var tittel in new[] { fullTittel, "UX77 svært tidlig", "UX77 samtidig A", "UX77 samtidig B", "UX77 svært sent" })
        {
            var hendelse = agenda.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await hendelse.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
            if (tittel == fullTittel) await n.Side.Keyboard.PressAsync("Escape");
            else await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "‹ Tilbake til dagsagenda", Exact = true }).ClickAsync();
            await Expect(agenda).ToBeVisibleAsync();
            await Expect(hendelse).ToBeFocusedAsync();
        }
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(alle).ToBeFocusedAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
        await Expect(n.Side.GetByLabel("2031", new() { Exact = true })).ToBeCheckedAsync();
        await n.Side.SetViewportSizeAsync(390, 844);
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 5 frister", Exact = true }).ClickAsync();
        await Dag(n, "15. november 2030").ClickAsync();
        await Expect(agenda.GetByRole(AriaRole.Button, new() { Name = "UX77 svært sent", Exact = true })).ToBeVisibleAsync();
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "ux77-travel-dag-mobil.png"), FullPage = true });
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tentative_primo_medio_ultimo_er_maanedsfrister_uten_dagsmarkoer_eller_oppdiktet_dato() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var kvalifikator in new[] { "Primo", "Medio", "Ultimo" })
            await n.OpprettFrist("UX77 tentativ " + kvalifikator, tentativ: true, kvalifikator: kvalifikator, dato: "2030-11-15");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var tentative = n.Side.GetByRole(AriaRole.Region, new() { Name = "Tentative frister i november 2030", Exact = true });
        foreach (var kvalifikator in new[] { "Primo", "Medio", "Ultimo" })
        {
            var tittel = "UX77 tentativ " + kvalifikator;
            await Expect(tentative.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
            await Expect(Maanedskalender(n).GetByRole(AriaRole.Cell).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToHaveCountAsync(0);
            await tentative.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).ClickAsync();
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
            await Expect(dialog).ToContainTextAsync(kvalifikator.ToLowerInvariant() + " november 2030");
            await Expect(dialog).Not.ToContainTextAsync("15. november");
            await Expect(dialog).Not.ToContainTextAsync("Heldag");
            await n.Side.Keyboard.PressAsync("Escape");
        }
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030", Exact = true })).ToBeVisibleAsync();
        await Dag(n, "15. november 2030").ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 15. november 2030", Exact = true })).ToContainTextAsync("Ingen frister");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Markoerer_og_tellinger_bruker_bare_autoriserte_godkjente_frister_i_aktivt_utvalg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX77 synlig Budsjett", dato: "2030-11-15");
        await n.OpprettFrist("UX77 synlig Gul bok", dato: "2030-11-15", kategori: "Gulbok", budsjettaar: 2032);
        await n.OpprettFrist("UX77 skjult FINFAG", "FIN-FAG", dato: "2030-11-16");
        await n.OpprettFrist("UX77 skjult POL", "POL", dato: "2030-11-17");
        var skjulte = (await n.Frister()).Where(f => ((string?)f!["tittel"])?.StartsWith("UX77 skjult ") == true)
            .Select(f => (string)f!["id"]!).ToArray();
        await n.LoggInn("demo-fa");
        await n.Gaa("/forslag/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX77 upublisert");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-18");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2031");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX77 upublisert" })).ToContainTextAsync("Venter");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 2 frister", Exact = true })).ToBeVisibleAsync();
        foreach (var dato in new[] { "16. november 2030", "17. november 2030", "18. november 2030" })
        {
            await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg " + dato, Exact = true })).ToBeVisibleAsync();
            await Dag(n, dato).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister " + dato, Exact = true })).ToContainTextAsync("Ingen frister");
            await n.Side.Keyboard.PressAsync("Escape");
        }
        var dom = await n.Side.ContentAsync();
        foreach (var skjult in new[] { "UX77 skjult FINFAG", "UX77 skjult POL", "UX77 upublisert" }.Concat(skjulte))
            Assert.DoesNotContain(skjult, dom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "UX77 upublisert");
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 1 frist", Exact = true })).ToBeVisibleAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX77 synlig Gul bok", Exact = true })).ToHaveCountAsync(0);
        await n.Side.GetByLabel("2032", new() { Exact = true }).CheckAsync();
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).CheckAsync();
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 15. november 2030, 1 frist", Exact = true })).ToBeVisibleAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX77 synlig Gul bok", Exact = true })).ToBeVisibleAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "UX77 synlig Budsjett", Exact = true })).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Fristdetaljer_bevarer_leser_bidrag_og_administrators_eksisterende_handlinger() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string tittel = "UX77 felles rolletest";
        await n.OpprettFrist(tittel);
        var id = (string)(await n.Frister()).Single(f => (string?)f!["tittel"] == tittel)!["id"]!;
        await n.Gaa("/admin/frist/" + id);
        foreach (var gruppe in new[] { "FIN-FAG", "FAG" })
            await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({gruppe}\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await VelgMaaned(n, "2030-11");
            await AapneFrist(n, tittel);
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
            var bidrag = persona is "demo-fa" or "demo-finfag";
            await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Foreslå endring", Exact = true })).ToHaveCountAsync(bidrag ? 1 : 0);
            await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
            if (bidrag || persona == "demo-admin")
            {
                await dialog.GetByRole(AriaRole.Link, new() { Name = bidrag ? "Foreslå endring" : "Rediger", Exact = true }).ClickAsync();
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = bidrag ? "Foreslå endring på en publisert frist" : "Rediger frist", Exact = true })).ToBeVisibleAsync();
                await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(tittel);
                await Expect(n.Side.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-20");
            }
            else
            {
                foreach (var sti in new[] { "/admin/frist/" + id, "/forslag/endre/" + id })
                {
                    await n.Side.GotoAsync(sti);
                    await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være", Exact = true })).ToBeVisibleAsync();
                }
            }
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Opprett_rediger_og_valider_dag_tid_tentativ_og_aktivt_POL_valg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frist/ny");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("Tittel");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK uten synlighet");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-20");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("synlig");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.OpprettFrist("AK heldag");
        await n.OpprettFrist("AK klokkeslett", tid: "13:45");
        await n.OpprettFrist("AK tentativ", "POL", tentativ: true);
        var frister = await n.Frister();
        var heldag = frister.Single(f => (string?)f!["tittel"] == "AK heldag")!;
        var timed = frister.Single(f => (string?)f!["tittel"] == "AK klokkeslett")!;
        Assert.Null(heldag["klokkeslett"]);
        Assert.Equal("13:45:00", (string?)timed["klokkeslett"]);
        Assert.Equal(new[] { "POL" }, frister.Single(f => (string?)f!["tittel"] == "AK tentativ")!["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.Gaa($"/admin/frist/{heldag["id"]}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK heldag redigert");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK heldag redigert" })).ToBeVisibleAsync();
        await n.LoggInn("demo-pol");
        Assert.Single(await n.Frister(), f => ((string?)f!["tittel"])?.StartsWith("AK ") == true);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("AK tentativ");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("medio");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kalender_aarshjul_filter_historikk_og_personasett_stemmer_med_serverrespons() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var gruppe in new[] { "FA", "FIN-FAG", "POL" }) await n.OpprettFrist($"AK bare {gruppe}", gruppe);
        foreach (var (persona, tillatt) in new[] { ("demo-fa", "FA"), ("demo-finfag", "FIN-FAG"), ("demo-pol", "POL"), ("demo-fag", "ingen") })
        {
            await n.LoggInn(persona);
            var data = await n.Frister();
            var egne = data.Where(f => ((string?)f!["tittel"])?.StartsWith("AK bare ") == true).Select(f => (string)f!["tittel"]!).ToArray();
            Assert.Equal(tillatt == "ingen" ? [] : new[] { $"AK bare {tillatt}" }, egne);
            await n.Gaa("/kalender");
            await VelgMaaned(n, "2030-11");
            await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
            var maanedsdata = data.Where(f => ((string)f!["sorteringsdag"]!).StartsWith("2030-11", StringComparison.Ordinal)).ToArray();
            foreach (var f in maanedsdata) await Expect(n.Side.Locator("main")).ToContainTextAsync((string)f!["tittel"]!);
            foreach (var f in data.Except(maanedsdata)) await Expect(n.Side.Locator("main")).Not.ToContainTextAsync((string)f!["tittel"]!);
            // Intern navigasjon beholder kretsens filtervalg.
            await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny", Exact = true })
                .GetByRole(AriaRole.Link, new() { Name = "Budsjettår", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
            await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
            foreach (var f in data) await Expect(n.Side.Locator("main")).ToContainTextAsync((string)f!["tittel"]!);
            // Det gamle bokmerket er fortsatt den samme virksomme fristlisten.
            await n.Gaa("/aarshjul");
            await n.Side.GetByLabel("Vis historikk", new() { Exact = true }).CheckAsync();
            await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).ToBeCheckedAsync();
            foreach (var f in data) await Expect(n.Side.Locator("main")).ToContainTextAsync((string)f!["tittel"]!);
            await n.Side.GetByLabel("Budsjett", new() { Exact = true }).UncheckAsync();
            await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
            if (data.Any(f => (int)f!["kategori"]! == 2))
                await Expect(n.Side.Locator(".hjul-tittel").First).ToBeVisibleAsync();
            else await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen frister i utvalget ditt.");
            await n.Side.ReloadAsync();
            await Expect(n.Side.GetByLabel("Vis historikk", new() { Exact = true })).Not.ToBeCheckedAsync();
            await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).ToBeCheckedAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await n.Side.GetByLabel("2031", new() { Exact = true }).CheckAsync();
        await Dag(n, "20. november 2030").ClickAsync();
        var agenda = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister 20. november 2030", Exact = true });
        foreach (var gruppe in new[] { "FA", "FIN-FAG", "POL" })
            await Expect(agenda.GetByRole(AriaRole.Button, new() { Name = "AK bare " + gruppe, Exact = true })).ToBeVisibleAsync();
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "kalender.png"), FullPage = true });
        n.BekreftIngenKretsfeil();
    });
    internal static ILocator Maanedskalender(Nettleser n)
        => n.Side.GetByRole(AriaRole.Region, new() { Name = "Månedskalender", Exact = true });

    internal static ILocator Minikalender(Nettleser n)
        => n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Minikalender", Exact = true });

    internal static ILocator Dag(Nettleser n, string dato)
        => Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = "Åpne dagsagenda for " + dato, Exact = true });

    internal static async Task AapneFrist(Nettleser n, string tittel, string dato = "20. november 2030")
    {
        await Dag(n, dato).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frister " + dato, Exact = true })
            .GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true })).ToBeVisibleAsync();
    }

    internal static async Task VelgMaaned(Nettleser n, string maaned)
    {
        await Expect(n.Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await n.Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync(maaned);
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync(maaned);
        await Expect(Maanedskalender(n)).ToBeVisibleAsync();
        var dato = DateTime.ParseExact(maaned + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Table, new() { Name = dato.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("nb-NO")), Exact = true })).ToBeVisibleAsync();
    }
}
