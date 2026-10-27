using System.Collections.Concurrent;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Grupper;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Grupper;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GenereringInitialiseringTester
{
    [Fact]
    public async Task Generering_venter_paa_grupper_og_bruker_URL_aaret_etter_initialisering()
    {
        // Samme HTTP-testvert og SQLite-tjenester som ordinære tester, her over ekte
        // Kestrel. Bare gruppeoppslagets fullføring styres; ingen produksjonsforsinkelse.
        await using var app = new ForsinketGruppeApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMaltjeneste>().OpprettAsync(new()
        {
            Loep = "ux82-initialisering", Tittel = "UX82 riktig genereringsår",
            Regelparametre = "{\"maaned\":11,\"dag\":20}"
        });
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL")
        });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, melding) => feil.Enqueue(melding);
        side.Console += (_, melding) => { if (melding.Type == "error") feil.Enqueue(melding.Text); };
        await side.GotoAsync("/admin/frister?aar=2031");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        app.Port.ForsinkNeste();
        await side.GetByRole(AriaRole.Link, new() { Name = "Generer fra årsmal", Exact = true }).ClickAsync();
        try
        {
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var generer = side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true });
            var maalaar = side.GetByLabel("Målbudsjettår", new() { Exact = true });
            await Expect(generer).ToBeDisabledAsync();
            await Expect(maalaar).ToHaveValueAsync("2031");
            await Expect(maalaar).ToBeDisabledAsync();
            Assert.False(app.Port.Fortsett.Task.IsCompleted);
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<IGenereringstjeneste>().HentAarMedForslagAsync());
            await TaBilder(side, "venter");
            app.Port.Fortsett.TrySetResult();
            await Expect(generer).ToBeEnabledAsync();
            await Expect(maalaar).ToBeEnabledAsync();
            await generer.ClickAsync();
            await Expect(side.GetByText("Genererte 1 forslag for budsjettår 2031.", new() { Exact = false })).ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "UX82 riktig genereringsår", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(new[] { 2031 }, await scope.ServiceProvider.GetRequiredService<IGenereringstjeneste>().HentAarMedForslagAsync());
            await TaBilder(side, "generert");
            Assert.Empty(feil);
        }
        finally { app.Port.Fortsett.TrySetResult(); }
    }

    private static async Task TaBilder(IPage side, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "generering-initialisering");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }

    private sealed class ForsinketGruppeApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Gruppeport? _port;
        public Gruppeport Port => _port ??= new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGruppetjeneste>();
                services.AddScoped<IGruppetjeneste>(sp => new ForsinketGrupper(
                    new Gruppetjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }

    private sealed class Gruppeport
    {
        private int _forsink;
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void ForsinkNeste() => Interlocked.Exchange(ref _forsink, 1);
        public async Task Vent(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _forsink, 0) == 0) return;
            Startet.TrySetResult();
            await Fortsett.Task.WaitAsync(ct);
        }
    }

    private sealed class ForsinketGrupper(IGruppetjeneste indre, Gruppeport port) : IGruppetjeneste
    {
        public async Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default)
        {
            await port.Vent(ct);
            return await indre.HentAktiveAsync(ct);
        }
        public Task<IReadOnlyList<GruppeadministrasjonDto>> HentAlleAsync(CancellationToken ct = default) => indre.HentAlleAsync(ct);
        public Task OpprettAsync(string kode, string navn, CancellationToken ct = default) => indre.OpprettAsync(kode, navn, ct);
        public Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default) => indre.OppdaterAsync(kode, navn, aktiv, ct);
    }
}
