using System.Collections.Concurrent;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Opplasting;
using Aarshjul.Infrastructure.Opplasting;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DokumentKapploepTester
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paagaaende_PDF_innlesing_laaser_aar_fil_og_navigasjon_og_dobbelhandling_lager_ett_forslag(bool feilFoerst)
    {
        // Bare fullføringen styres ved den eksisterende opplastingsporten. PDF-leser,
        // heuristisk uttrekk, lagring og kølesing er de faktiske adapterne også her.
        await using var app = new PortApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var ko = scope.ServiceProvider.GetRequiredService<IGodkjenningsko>();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var kretsfeil = new ConcurrentQueue<string>();
        side.PageError += (_, e) => kretsfeil.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") kretsfeil.Enqueue(e.Text); };
        await side.GotoAsync("/admin/last-opp?aar=2031");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await side.GetByLabel("Dokument (PDF)", new() { Exact = true }).SetInputFilesAsync(new FilePayload
        {
            Name = "UX13 operasjon.pdf", MimeType = "application/pdf",
            Buffer = DokumentTester.Pdf("UX13 operasjon rammefordeling 20. november 2030")
        });
        app.Port.Feil = feilFoerst;
        var knapp = side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true });
        await knapp.EvaluateAsync("button => { button.click(); button.click(); button.click(); }");
        try
        {
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByLabel("Dokument (PDF)", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Budsjettår", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true })).ToBeDisabledAsync();
            await side.GetByRole(AriaRole.Link, new() { Name = "Forslag", Exact = true }).EvaluateAsync("a => a.click()");
            await Expect(side.Locator("main")).ToContainTextAsync("Vent til innlesingen er ferdig");
            Assert.EndsWith("/admin/last-opp?aar=2031", side.Url);
            Assert.Empty(await ko.HentKoAsync(new() { Kilde = "Opplastet: UX13 operasjon.pdf" }));
            Assert.Equal(1, app.Port.Antall);
            await TaBilder(side, $"venter-{feilFoerst}");
        }
        finally { app.Port.Fortsett.TrySetResult(); }
        if (feilFoerst)
        {
            await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Klarte ikke lese dokumentet");
            await Expect(side.GetByRole(AriaRole.Alert)).Not.ToContainTextAsync("UX13-private-feildetaljer");
            Assert.Empty(await ko.HentKoAsync(new() { Kilde = "Opplastet: UX13 operasjon.pdf" }));
            await Expect(knapp).ToBeEnabledAsync();
            await TaBilder(side, "kontrollert-feil");
            await knapp.ClickAsync();
        }
        await Expect(side.Locator("main")).ToContainTextAsync("laget 1 forslag");
        await Expect(side.GetByRole(AriaRole.Status).Filter(new() { HasText = "Vent til innlesingen er ferdig" })).ToHaveCountAsync(0);
        var lagret = Assert.Single(await ko.HentKoAsync(new() { Kilde = "Opplastet: UX13 operasjon.pdf" }));
        Assert.Equal(2031, lagret.Budsjettaar);
        Assert.Equal(new DateOnly(2030, 11, 20), lagret.Dato);
        Assert.Equal(feilFoerst ? 2 : 1, app.Port.Antall);
        await TaBilder(side, $"ferdig-{feilFoerst}");
        await side.GetByRole(AriaRole.Link, new() { Name = "Kontroller forslagene", Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true })).ToHaveValueAsync("2031");
        Assert.Empty(kretsfeil);
    }

    private static async Task TaBilder(IPage side, string navn)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "dokument-pending");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{navn}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
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
                services.RemoveAll<IDokumentopplasting>();
                services.AddScoped<IDokumentopplasting>(sp => new ForsinketOpplasting(ActivatorUtilities.CreateInstance<OpplastingsTjeneste>(sp), port));
            });
        }
    }
    private sealed class Port
    {
        public int Antall;
        public bool Feil;
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class ForsinketOpplasting(IDokumentopplasting indre, Port port) : IDokumentopplasting
    {
        public async Task<OpplastingsResultat> LesOgLagForslagAsync(OpplastetDokument dokument, int budsjettaar, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref port.Antall) == 1)
            {
                port.Startet.TrySetResult();
                await port.Fortsett.Task.WaitAsync(ct);
                if (port.Feil) throw new InvalidOperationException("UX13-private-feildetaljer");
            }
            return await indre.LesOgLagForslagAsync(dokument, budsjettaar, ct);
        }
    }
}
