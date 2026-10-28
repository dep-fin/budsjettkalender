using System.Collections.Concurrent;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class EksportutvalgKapploepTester
{
    [Fact]
    public Task Sen_FA_preview_kan_ikke_erstatte_eksplisitt_POL_etter_feltbytte() => Kjor(async (side, port, _) =>
    {
        await Gruppe(side).SelectOptionAsync("FA");
        port.Forsink();
        await Knapp(side, "Kontroller utvalg").ClickAsync();
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Gruppe(side).SelectOptionAsync("POL");
            await Expect(Preview(side)).ToHaveCountAsync(0);
            await Knapp(side, "Kontroller utvalg").ClickAsync();
            await Expect(Preview(side).Locator("tbody tr")).ToHaveCountAsync(1);
            await Expect(Preview(side)).ToContainTextAsync("FA og POL");
            port.Fortsett.TrySetResult();
            await port.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Preview(side).Locator("tbody tr")).ToHaveCountAsync(1);
            await Expect(Preview(side)).Not.ToContainTextAsync("Kun FA");
            await Expect(Gruppe(side)).ToHaveValueAsync("POL");
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Avbryt_ventende_kontroll_bevarer_felt_returnerer_fokus_og_utelater_sen_preview() => Kjor(async (side, port, mappe) =>
    {
        await Gruppe(side).SelectOptionAsync("FA");
        await side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2027-01-01");
        await side.GetByLabel("Til dato", new() { Exact = true }).FillAsync("2027-12-31");
        port.Forsink();
        await Knapp(side, "Kontroller utvalg").ClickAsync();
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Knapp(side, "Kontrollerer …")).ToBeDisabledAsync();
            await TaBilder(side, mappe, "eksport-venter");
            await Knapp(side, "Avbryt").FocusAsync();
            await Knapp(side, "Avbryt").PressAsync("Enter");
            await Expect(Knapp(side, "Kontroller utvalg")).ToBeFocusedAsync();
            await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("avbrutt");
            await Expect(Gruppe(side)).ToHaveValueAsync("FA");
            await Expect(side.GetByLabel("Fra dato", new() { Exact = true })).ToHaveValueAsync("2027-01-01");
            await Expect(side.GetByLabel("Til dato", new() { Exact = true })).ToHaveValueAsync("2027-12-31");
            await TaBilder(side, mappe, "eksport-avbryt-fokus");
            port.Fortsett.TrySetResult();
            await port.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Preview(side)).ToHaveCountAsync(0);
            await Knapp(side, "Kontroller utvalg").ClickAsync();
            await Expect(Preview(side).Locator("tbody tr")).ToHaveCountAsync(2);
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Teknisk_utvalgsfeil_skjuler_gammelt_publikum_bevarer_felt_og_tillater_nytt_forsoek() => Kjor(async (side, port, mappe) =>
    {
        await Gruppe(side).SelectOptionAsync("FA");
        await Knapp(side, "Kontroller utvalg").ClickAsync();
        await Expect(Preview(side)).ToContainTextAsync("Kun FA");
        await Gruppe(side).SelectOptionAsync("FAG");
        port.FeilVedNeste = true;
        await Knapp(side, "Kontroller utvalg").ClickAsync();
        await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Valgene er beholdt");
        await Expect(side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(Preview(side)).ToHaveCountAsync(0);
        await Expect(side.Locator("main")).Not.ToContainTextAsync("TEKNISK-DETALJ");
        await Expect(side.Locator("main")).Not.ToContainTextAsync("Kun FA");
        await Expect(Gruppe(side)).ToHaveValueAsync("FAG");
        await TaBilder(side, mappe, "eksport-teknisk-feil");
        await Knapp(side, "Kontroller utvalg").ClickAsync();
        await Expect(Preview(side)).ToContainTextAsync("Kun FAG");
        await Expect(Preview(side).Locator("tbody tr")).ToHaveCountAsync(1);
        await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    });

    private static async Task Kjor(Func<IPage, Port, string, Task> scenario)
    {
        await using var app = new PortApplikasjon();
        using var klient = app.CreateClient();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, e) => feil.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") feil.Enqueue(e.Text); };
        await side.GotoAsync("/admin/eksport");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await Expect(Gruppe(side)).ToBeEnabledAsync();
        var mappe = Path.Combine(Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") ?? Path.GetTempPath(), "90-utvalg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mappe);
        await scenario(side, app.Port, mappe);
        Assert.Empty(feil);
    }
    private static ILocator Gruppe(IPage side) => side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true });
    private static ILocator Preview(IPage side) => side.GetByRole(AriaRole.Region, new() { Name = "Eksportutvalg", Exact = true });
    private static ILocator Knapp(IPage side, string navn) => side.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true });
    private static async Task TaBilder(IPage side, string mappe, string navn)
    {
        foreach (var (bredde, hoyde) in new[] { (1440, 1000), (390, 844) })
        {
            await side.SetViewportSizeAsync(bredde, hoyde);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.EvaluateAsync("window.scrollTo(0, 0)");
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"ux90-{navn}-{bredde}.png"), FullPage = true });
        }
    }
    private sealed class PortApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Port? _port;
        public Port Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFristlesing>();
                services.AddScoped<IFristlesing>(sp => new ForsinketLesing(new FristTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
            });
        }
    }
    private sealed class Port
    {
        public bool FeilVedNeste;
        private bool _forsink;
        public TaskCompletionSource Startet { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fullfoert { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Forsink() { _forsink = true; }
        public bool TaForsinkelse() { var verdi = _forsink; _forsink = false; return verdi; }
    }
    private sealed class ForsinketLesing(IFristlesing indre, Port port) : IFristlesing
    {
        public async Task<IReadOnlyList<FristDto>> HentSynligeAsync(ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default)
        {
            if (port.FeilVedNeste) { port.FeilVedNeste = false; throw new IOException("TEKNISK-DETALJ"); }
            var resultat = await indre.HentSynligeAsync(ctx, filter, ct);
            if (!port.TaForsinkelse()) return resultat;
            port.Startet.TrySetResult();
            // En allerede avsendt lesing kan fullføre selv etter at brukeren har endret utvalget.
            await port.Fortsett.Task;
            port.Fullfoert.TrySetResult();
            return resultat;
        }
        public Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid fristId, CancellationToken ct = default) => indre.HentEnAsync(ctx, fristId, ct);
        public Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(ISynlighetskontekst ctx, DateOnly fraOgMed, FristFilter filter, CancellationToken ct = default) => indre.HentLandingsutvalgAsync(ctx, fraOgMed, filter, ct);
        public Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(ISynlighetskontekst ctx, bool inkluderHistorikk, CancellationToken ct = default) => indre.HentTilgjengeligeBudsjettaarAsync(ctx, inkluderHistorikk, ct);
    }
}
