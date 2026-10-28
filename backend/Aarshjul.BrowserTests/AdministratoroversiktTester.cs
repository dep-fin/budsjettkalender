using System.Net;
using System.Text.Json;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AdministratoroversiktTester
{
    [Fact]
    public Task Manuell_og_Entra_administrator_ser_appkjente_personer_kilde_og_egen_identitet() => Nettleser.Kjor(async n =>
    {
        // SBR er seedet som bidragsyter. Bare vedkommendes egen innlogging gir Entra-admin.
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/innstillinger");
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Administratorer", Exact = true })
            .GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Administratorer", Exact = true }))
            .Not.ToContainTextAsync("Sivert SBR");
        await n.LoggInn("demo-sbr");
        await n.Gaa("/admin/innstillinger");
        foreach (var persona in new[] { "demo-admin", "demo-sbr" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/admin/innstillinger");
            var oversikt = n.Side.GetByRole(AriaRole.Region, new() { Name = "Administratorer", Exact = true });
            await Expect(oversikt.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
            var manuell = oversikt.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Dag Admin (administrator, FA)" });
            var entra = oversikt.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Sivert SBR (seksjon SBR – blir admin automatisk)" });
            await Expect(manuell).ToContainTextAsync("Manuelt tildelt");
            await Expect(entra).ToContainTextAsync("Forvaltes i Entra");
            await Expect((persona == "demo-admin" ? manuell : entra).GetByText("Deg", new() { Exact = true })).ToBeVisibleAsync();
            await Expect((persona == "demo-admin" ? entra : manuell).GetByText("Deg", new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(oversikt.GetByRole(AriaRole.Button, new() { NameRegex = new("Utnevn|Fjern|Lagre") })).ToHaveCountAsync(0);
            await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne faseinndeling", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne dataforvaltning", Exact = true })).ToBeVisibleAsync();
            if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is { } rot)
            {
                var mappe = Path.Combine(rot, "administratoroversikt"); Directory.CreateDirectory(mappe);
                foreach (var bredde in new[] { 1440, 390 })
                {
                    await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
                    Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                    await n.Side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{persona}-{bredde}.png"), FullPage = true });
                }
            }
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Samme_navn_gir_bare_Deg_paa_stabil_identitet_og_hele_navn_er_trygt_kodet() => Administratorprove.Kjor(async p =>
    {
        await Seed(p);
        var foer = await Lagret(p);
        await p.Gaa();
        var rader = p.Oversikt.GetByRole(AriaRole.Listitem);
        await Expect(rader).ToHaveCountAsync(3);
        await Expect(rader.Nth(0)).ToContainTextAsync("Samme navn");
        await Expect(rader.Nth(0)).ToContainTextAsync("Manuelt tildelt");
        await Expect(rader.Nth(0).GetByText("Deg", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(rader.Nth(1)).ToContainTextAsync("Samme navn");
        await Expect(rader.Nth(1)).ToContainTextAsync("Forvaltes i Entra");
        await Expect(rader.Nth(1).GetByText("Deg", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(rader.Nth(2)).ToContainTextAsync(LangtNavn);
        await Expect(p.Oversikt.Locator("img, script")).ToHaveCountAsync(0);
        await Expect(p.Oversikt.GetByRole(AriaRole.Button)).ToHaveCountAsync(1);
        await Expect(p.Side.Locator("body")).Not.ToContainTextAsync("UX92 skjult lesernavn");
        await Expect(p.Side.Locator("body")).Not.ToContainTextAsync("UX92 skjult bidragsnavn");
        foreach (var id in new[] { "ux92-egen", "ux92-annen", "ux92-lang" }) Assert.DoesNotContain(id, await p.Side.ContentAsync());
        await p.Bilder("fylt");
        await p.Oppdater.FocusAsync();
        await p.Oppdater.PressAsync("Enter");
        await Expect(p.Oversikt.GetByRole(AriaRole.Status)).ToHaveTextAsync("3 administratorer");
        await Expect(p.Oppdater).ToBeFocusedAsync();
        Assert.True(await p.Oppdater.EvaluateAsync<bool>("e => {const s = getComputedStyle(e); return s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= 2;}"));
        await p.Bilder("tastaturfokus");
        await p.Side.SetViewportSizeAsync(390, 844);
        await p.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne faseinndeling", Exact = true }).PressAsync("Enter");
        await Expect(p.Side.GetByRole(AriaRole.Heading, new() { Name = "Faseinndeling", Exact = true })).ToBeVisibleAsync();
        await p.Gaa();
        await p.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne dataforvaltning", Exact = true }).PressAsync("Enter");
        await Expect(p.Side.GetByRole(AriaRole.Heading, new() { Name = "Eksporter / importer data", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(foer, await Lagret(p));
    });

    [Fact]
    public Task Lasting_feil_og_tastaturstyrt_nytt_forsoek_bevarer_lesende_oversikt_uten_raa_feil() => Administratorprove.Kjor(async p =>
    {
        await Seed(p); await p.Gaa(); var foer = await Lagret(p);
        var steg = p.Kontroll.Neste(feil: true);
        await p.Oppdater.ClickAsync(); await steg.VentStart();
        await Expect(p.Oversikt).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(p.Oversikt.GetByRole(AriaRole.Status)).ToHaveTextAsync("Laster administratorer …");
        await Expect(p.Oversikt.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0);
        await Expect(p.Oppdater).ToBeDisabledAsync(); await p.Bilder("laster");
        steg.Slipp();
        await Expect(p.Oversikt.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Kunne ikke laste administratorene akkurat nå. Velg Oppdater oversikt for å prøve igjen.");
        Assert.DoesNotContain("UX92 intern databasefeil", await p.Side.ContentAsync());
        await Expect(p.Oversikt.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0);
        await p.Bilder("feil");
        await p.Oppdater.FocusAsync(); await p.Oppdater.PressAsync("Enter");
        await Expect(p.Oversikt.GetByRole(AriaRole.Status)).ToHaveTextAsync("3 administratorer");
        await Expect(p.Oversikt.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await Expect(p.Oppdater).ToBeFocusedAsync(); await p.Bilder("nytt-forsoek");
        Assert.Equal(foer, await Lagret(p));
    });

    [Fact]
    public Task Tom_appkjent_liste_forklarer_tilstanden_og_oppdatering_finner_ny_lagret_administrator() => Administratorprove.Kjor(async p =>
    {
        // Testautentisering gir adminclaim; den reelle appdatabasen har ennå ingen personer.
        await p.Gaa();
        await Expect(p.Oversikt.GetByRole(AriaRole.Status)).ToHaveTextAsync("Ingen administratorer er registrert i appen. Oppdater oversikten for å prøve igjen.");
        await Expect(p.Oversikt.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(0); await p.Bilder("tomt");
        await Seed(p); await p.Oppdater.PressAsync("Enter");
        await Expect(p.Oversikt.GetByRole(AriaRole.Status)).ToHaveTextAsync("3 administratorer");
    });

    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    public Task Direkte_UI_og_http_lesing_avvises_uten_administratorpersoner(string? rolle) => Administratorprove.Kjor(async p =>
    {
        await Seed(p);
        var side = await p.NySide(rolle, "uautorisert");
        var svar = await side.GotoAsync("/admin/innstillinger");
        Assert.Equal(rolle is null ? (int)HttpStatusCode.Unauthorized : (int)HttpStatusCode.Forbidden, svar!.Status);
        var html = await svar.TextAsync();
        foreach (var markoer in new[] { "Samme navn", LangtNavn, "ux92-egen", "ux92-annen", "Manuelt tildelt", "Forvaltes i Entra" })
            Assert.DoesNotContain(markoer, html, StringComparison.OrdinalIgnoreCase);
        await Expect(side.GetByRole(AriaRole.Region, new() { Name = "Administratorer", Exact = true })).ToHaveCountAsync(0);
    });

    private const string LangtNavn = "Åse Anne Kristine <img src=x onerror=alert(1)> & Ødegård med et langt etternavn";
    private static async Task Seed(Administratorprove p)
    {
        await using var db = await p.Fabrikk.CreateDbContextAsync();
        db.Brukere.AddRange(
            new Bruker { Id = "ux92-egen", Navn = "Samme navn", Funksjonsrolle = Funksjonsrolle.Administrator, AdminViaEntra = true, ErFin = true },
            new Bruker { Id = "ux92-annen", Navn = "Samme navn", Funksjonsrolle = Funksjonsrolle.Administrator, ErFin = true },
            new Bruker { Id = "ux92-lang", Navn = LangtNavn, Funksjonsrolle = Funksjonsrolle.Administrator, AdminViaEntra = true, ErFin = true },
            new Bruker { Id = "leser", Navn = "UX92 skjult lesernavn" },
            new Bruker { Id = "bidrag", Navn = "UX92 skjult bidragsnavn", Funksjonsrolle = Funksjonsrolle.Bidragsyter });
        await db.SaveChangesAsync();
    }

    private static async Task<string> Lagret(Administratorprove p)
    {
        await using var db = await p.Fabrikk.CreateDbContextAsync();
        return JsonSerializer.Serialize(await db.Brukere.AsNoTracking().OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Navn, b.Funksjonsrolle, b.ErFin, b.AdminViaEntra,
                Grupper = b.Grupper.OrderBy(g => g.GruppeKode).Select(g => new { g.GruppeKode, g.Kilde }).ToArray() }).ToArrayAsync());
    }
}
