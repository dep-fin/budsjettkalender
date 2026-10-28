using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class FristoverfoeringKapploepTester
{
    [Fact]
    public Task Sen_preview_fra_fil_A_kan_ikke_aktivere_import_over_nytt_filvalg_B() => Kjor(async (side, tjeneste, port, _) =>
    {
        var foer = Normaliser(await tjeneste.EksporterAsync());
        await Fil(side).SetInputFilesAsync(JsonFil("A"));
        port.Forsink("preview");
        await Knapp(side, "Forhåndsvis frister").ClickAsync();
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Fil(side).SetInputFilesAsync(JsonFil("B"));
            port.Fortsett.TrySetResult();
            await port.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Preview(side)).ToHaveCountAsync(0);
            await Expect(Knapp(side, "Importer frister (erstatt alt)")).ToHaveCountAsync(0);
            Assert.Equal(foer, Normaliser(await tjeneste.EksporterAsync()));
            await Knapp(side, "Forhåndsvis frister").ClickAsync();
            await Expect(Preview(side)).ToContainTextAsync("UX90 B");
            await Expect(Preview(side)).Not.ToContainTextAsync("UX90 A");
            await Bekreft(side);
            await Expect(side.Locator("main")).ToContainTextAsync("Importerte 1");
            Assert.Equal("UX90 B", Assert.Single((await tjeneste.EksporterAsync()).Frister).Tittel);
            Assert.Equal(1, port.Importer);
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Koede_bekreftelser_importerer_en_gang_og_venting_laaser_filvalg_avbryt_og_navigasjon() => Kjor(async (side, tjeneste, port, _) =>
    {
        var foer = Normaliser(await tjeneste.EksporterAsync());
        await Forhaandsvis(side, "bekreftet");
        await Knapp(side, "Importer frister (erstatt alt)").ClickAsync();
        port.Forsink("import");
        var dialog = side.GetByRole(AriaRole.Dialog);
        try
        {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).EvaluateAsync("b => { b.click(); b.click(); }");
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Fil(side)).ToBeDisabledAsync();
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
            await Expect(Knapp(side, "Importerer …")).ToBeDisabledAsync();
            Assert.Equal(foer, Normaliser(await tjeneste.EksporterAsync()));
            await side.GetByRole(AriaRole.Link, new() { Name = "Overfør mal", Exact = true }).EvaluateAsync("a => a.click()");
            await Expect(side.Locator("main")).ToContainTextAsync("Vent til importen er ferdig");
            Assert.EndsWith("/admin/data", side.Url);
        }
        finally { port.Fortsett.TrySetResult(); }
        await Expect(side.Locator("main")).ToContainTextAsync("Importerte 1");
        Assert.Equal(1, port.Importer);
        Assert.Equal("UX90 bekreftet", Assert.Single((await tjeneste.EksporterAsync()).Frister).Tittel);
        await Expect(side.GetByRole(AriaRole.Status).Filter(new() { HasText = "Vent til importen er ferdig" })).ToHaveCountAsync(0);
    });

    [Fact]
    public Task Endret_lagret_antall_foer_commit_oppdaterer_konsekvens_og_krever_ny_bekreftelse() => Kjor(async (side, tjeneste, port, _) =>
    {
        await Forhaandsvis(side, "endelig");
        await Knapp(side, "Importer frister (erstatt alt)").ClickAsync();
        port.Forsink("preview");
        await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).ClickAsync();
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await tjeneste.ImporterAsync(new() { Frister = [Rad("andre-a"), Rad("andre-b")] });
            port.Fortsett.TrySetResult();
            await Expect(side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
            await Expect(Preview(side)).ToContainTextAsync("2");
            await Expect(side.Locator("main")).ToContainTextAsync("endret");
            Assert.Equal(0, port.Importer);
            Assert.Equal(2, (await tjeneste.EksporterAsync()).Frister.Count);
            await Bekreft(side);
            await Expect(side.Locator("main")).ToContainTextAsync("Importerte 1");
            Assert.Equal(1, port.Importer);
            Assert.Equal("UX90 endelig", Assert.Single((await tjeneste.EksporterAsync()).Frister).Tittel);
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Endret_gruppekunnskap_etter_preview_krever_ny_konsekvens_foer_import() => Kjor(async (side, tjeneste, port, factory) =>
    {
        var rad = Rad("gruppeendring") with { SynligFor = ["FA", "NYGRUPPE"] };
        await Fil(side).SetInputFilesAsync(new FilePayload { Name = "gruppe.json", MimeType = "application/json", Buffer = JsonSerializer.SerializeToUtf8Bytes(new FristDatabase { Frister = [rad] }, WebJson) });
        await Knapp(side, "Forhåndsvis frister").ClickAsync();
        await Expect(Preview(side)).ToContainTextAsync("NYGRUPPE");
        await Knapp(side, "Importer frister (erstatt alt)").ClickAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = "NYGRUPPE", Navn = "Ny gruppe" });
            await db.SaveChangesAsync();
        }
        await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Expect(side.Locator("main")).ToContainTextAsync("endret");
        Assert.Equal(0, port.Importer);
        await Bekreft(side);
        await Expect(side.Locator("main")).ToContainTextAsync("Importerte 1");
        Assert.Equal(new[] { "FA", "NYGRUPPE" }, Assert.Single((await tjeneste.EksporterAsync()).Frister).SynligFor);
    });

    [Fact]
    public Task Teknisk_previewfeil_bevarer_valgt_fil_og_trygt_nytt_forsoek_fungerer() => Kjor(async (side, tjeneste, port, _) =>
    {
        var foer = Normaliser(await tjeneste.EksporterAsync());
        await Fil(side).SetInputFilesAsync(JsonFil("beholdes"));
        port.FeilVedNestePreview = true;
        await Knapp(side, "Forhåndsvis frister").ClickAsync();
        await Expect(side.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(side.Locator("main")).Not.ToContainTextAsync("TEKNISK-DETALJ");
        await Expect(side.Locator("main")).ToContainTextAsync("beholdes.json");
        await Expect(Preview(side)).ToHaveCountAsync(0);
        Assert.Equal(foer, Normaliser(await tjeneste.EksporterAsync()));
        await Knapp(side, "Forhåndsvis frister").ClickAsync();
        await Expect(Preview(side)).ToContainTextAsync("UX90 beholdes");
        Assert.Equal(foer, Normaliser(await tjeneste.EksporterAsync()));
    });

    private static async Task Kjor(Func<IPage, IFristDatautveksling, Port, IDbContextFactory<AppDbContext>, Task> scenario)
    {
        await using var app = new PortApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        IFristDatautveksling tjeneste = new FristDatautveksling(factory, TimeProvider.System);
        await tjeneste.ImporterAsync(new() { Frister = [Rad("gammel")] });
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var fejl = new ConcurrentQueue<string>();
        side.PageError += (_, e) => fejl.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") fejl.Enqueue(e.Text); };
        await side.GotoAsync("/admin/data");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await scenario(side, tjeneste, app.Port, factory);
        Assert.Empty(fejl);
    }
    private static readonly JsonSerializerOptions WebJson = Aarshjul.Web.Api.FristEndepunkter.DatabaseJson;
    private static ILocator Fil(IPage side) => side.GetByLabel("Fil (JSON)", new() { Exact = true });
    private static ILocator Preview(IPage side) => side.GetByRole(AriaRole.Region, new() { Name = "Importforhåndsvisning", Exact = true });
    private static ILocator Knapp(IPage side, string navn) => side.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true });
    private static async Task Forhaandsvis(IPage side, string navn)
    {
        await Fil(side).SetInputFilesAsync(JsonFil(navn));
        await Knapp(side, "Forhåndsvis frister").ClickAsync();
        await Expect(Preview(side)).ToContainTextAsync("UX90 " + navn);
    }
    private static async Task Bekreft(IPage side)
    {
        await Knapp(side, "Importer frister (erstatt alt)").ClickAsync();
        await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).ClickAsync();
    }
    private static FristEksport Rad(string navn) => new() { Id = Guid.NewGuid(), Tittel = "UX90 " + navn, Dato = new(2030, 11, 20), Budsjettaar = 2031, Status = FristStatus.Godkjent, SynligFor = ["FA"] };
    private static FilePayload JsonFil(string navn) => new() { Name = navn + ".json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new FristDatabase { Frister = [Rad(navn)] }, WebJson)) };
    private static string Normaliser(FristDatabase fil) => JsonSerializer.Serialize(fil.Frister.OrderBy(f => f.Id));
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
                services.RemoveAll<IFristDatautveksling>();
                services.AddScoped<IFristDatautveksling>(sp => new ForsinketUtveksling(new FristDatautveksling(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
            });
        }
    }
    private sealed class Port
    {
        private string? _neste;
        public int Importer;
        public bool FeilVedNestePreview;
        public TaskCompletionSource Startet { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fullfoert { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Forsink(string operasjon)
        {
            Startet = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Fullfoert = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _neste = operasjon;
        }
        public async Task<bool> Vent(string operasjon, CancellationToken ct)
        {
            if (Interlocked.CompareExchange(ref _neste, null, operasjon) != operasjon) return false;
            Startet.TrySetResult();
            await Fortsett.Task.WaitAsync(ct);
            return true;
        }
    }
    private sealed class ForsinketUtveksling(IFristDatautveksling indre, Port port) : IFristDatautveksling
    {
        public Task<FristDatabase> EksporterAsync(CancellationToken ct = default) => indre.EksporterAsync(ct);
        public async Task<FristImportForhaandsvisning> ForhaandsvisAsync(FristDatabase database, CancellationToken ct = default)
        {
            var forsinket = await port.Vent("preview", ct);
            try
            {
                if (port.FeilVedNestePreview) { port.FeilVedNestePreview = false; throw new IOException("TEKNISK-DETALJ"); }
                return await indre.ForhaandsvisAsync(database, ct);
            }
            finally { if (forsinket) port.Fullfoert.TrySetResult(); }
        }
        public async Task<ImportResultat> ImporterAsync(FristDatabase database, CancellationToken ct = default)
        {
            Interlocked.Increment(ref port.Importer);
            await port.Vent("import", ct);
            return await indre.ImporterAsync(database, ct);
        }
    }
}
