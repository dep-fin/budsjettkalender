using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KalenderkontekstTester
{
    [Fact]
    public async Task Hver_side_henter_fersk_autentisering_foer_ekte_leseport_og_skjuler_tidligere_gruppes_IDer()
    {
        await using var app = new Kalenderapplikasjon();
        using var klient = app.CreateClient();
        var fabrikk = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await fabrikk.CreateDbContextAsync())
        {
            foreach (var (gruppe, antall) in new[] { ("FA", 53), ("FAG", 7) })
                for (var i = 0; i < antall; i++)
                    db.Frister.Add(new Frist
                    {
                        Id = Guid.NewGuid(), Tittel = $"KONTEKST138 {gruppe} {i:00}", Dato = new(2030, 11, 20),
                        Budsjettaar = 2031, Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = gruppe }]
                    });
            await db.SaveChangesAsync();
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var kontekst = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Leser", ["X-Test-Grupper"] = "FA" }
        });
        var side = await kontekst.NewPageAsync();
        var feil = new List<string>();
        side.PageError += (_, melding) => feil.Add(melding);
        await side.GotoAsync("/kalender");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await side.GetByRole(AriaRole.Searchbox).FillAsync("KONTEKST138");
        await side.GetByRole(AriaRole.Navigation, new() { Name = "Kalendervisning", Exact = true }).GetByRole(AriaRole.Button, new() { Name = "Liste", Exact = true }).ClickAsync();
        var liste = side.GetByRole(AriaRole.Region, new() { Name = "Kalenderliste", Exact = true });
        var status = liste.GetByRole(AriaRole.Navigation, new() { Name = "Sider med frister", Exact = true }).GetByRole(AriaRole.Status);
        await Expect(status).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await liste.GetByRole(AriaRole.Button, new() { Name = "Neste side", Exact = true }).ClickAsync();
        await Expect(status).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        var gamleIder = await liste.Locator(".kalenderliste [data-frist-id]").EvaluateAllAsync<string[]>("r => r.map(e => e.dataset.fristId)");
        // Ingen auth-varsling eller ny krets: neste brukerhandling må selv hente fersk kontekst.
        app.Innlogging.Gruppe = "FAG";
        await liste.GetByRole(AriaRole.Button, new() { Name = "Neste side", Exact = true }).ClickAsync();
        await Expect(status).ToHaveTextAsync("Side 1 av 1 · 7 frister");
        await Expect(liste).ToContainTextAsync("Utvalget er oppdatert");
        await Expect(liste.Locator(".kalenderliste [data-frist-id]")).ToHaveCountAsync(7);
        var dom = await side.ContentAsync();
        Assert.DoesNotContain("KONTEKST138 FA ", dom);
        foreach (var id in gamleIder) Assert.DoesNotContain(id, dom, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(feil);
    }

    private sealed class Kalenderapplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private KontrollertInnlogging? _innlogging;
        public KontrollertInnlogging Innlogging => _innlogging ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var innlogging = Innlogging;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AuthenticationStateProvider>();
                services.AddSingleton<AuthenticationStateProvider>(innlogging);
            });
        }
    }

    // Bare identitetsgrensen kontrolleres; IFristlesing/SQL/Blazor er appens egne.
    private sealed class KontrollertInnlogging : ServerAuthenticationStateProvider
    {
        public string Gruppe = "FA";
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, "kontekst138"), new(Brukerclaims.Rolle, "Leser"), new(Brukerclaims.Gruppe, Gruppe)], "Test"))));
    }
}
