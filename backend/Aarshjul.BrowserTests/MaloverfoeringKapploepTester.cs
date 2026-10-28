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

public sealed class MaloverfoeringKapploepTester
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Sen_preview_fra_fil_A_kan_ikke_aktivere_eller_importere_den_over_fil_B(bool vedBekreftelse) => Kjor(async (side, tjeneste, port) =>
    {
        var gammel = (await tjeneste.EksporterAsync()).Regler;
        if (vedBekreftelse)
        {
            await Fil(side).SetInputFilesAsync(JsonFil("A"));
            await Expect(Preview(side)).ToContainTextAsync("A.json");
        }
        port.Forsink("preview");
        if (vedBekreftelse) await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
        else await Fil(side).SetInputFilesAsync(JsonFil("A"));
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Fil(side).SetInputFilesAsync(JsonFil("B"));
            await Expect(Preview(side)).ToContainTextAsync("B.json");
            port.Fortsett.TrySetResult();
            await port.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Preview(side)).ToContainTextAsync("B.json");
            await Expect(Preview(side)).Not.ToContainTextAsync("UX87 A");
            Assert.Equal(gammel, (await tjeneste.EksporterAsync()).Regler);
            await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
            await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 1 regler", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Årsmalen er erstattet", Exact = true })).ToBeVisibleAsync();
            Assert.Equal("B", Assert.Single((await tjeneste.EksporterAsync()).Regler).Loep);
            Assert.Equal(1, port.Importer);
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Koede_doble_bekreftelser_importerer_bare_en_gang_og_laaser_filvalg_og_navigasjon() => Kjor(async (side, tjeneste, port, databaseFactory) =>
    {
        var gammel = (await tjeneste.EksporterAsync()).Regler;
        await Fil(side).SetInputFilesAsync(JsonFil("ett-resultat"));
        await Expect(Preview(side)).ToBeVisibleAsync();
        await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
        var dialog = side.GetByRole(AriaRole.Dialog);
        port.Forsink("import");
        try
        {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 1 regler", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Knapp(side, "Importerer …")).ToBeDisabledAsync();
            await Expect(Fil(side)).ToBeDisabledAsync();
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
            Assert.Equal(gammel, (await tjeneste.EksporterAsync()).Regler);
            // Faktisk Blazor-intern navigasjon under lagring skal ikke fjerne operasjonen.
            await side.GetByRole(AriaRole.Link, new() { Name = "Prøv malen", Exact = true }).EvaluateAsync("a => a.click()");
            await Expect(side.Locator("main")).ToContainTextAsync("Vent til importen er ferdig");
            Assert.EndsWith("/admin/mal/overfor", side.Url);
        }
        finally { port.Fortsett.TrySetResult(); }
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Årsmalen er erstattet", Exact = true })).ToBeVisibleAsync();
        var ventestatus = side.GetByRole(AriaRole.Status).Filter(new() { HasText = "Vent til importen er ferdig" });
        var etterSuksess = await ventestatus.CountAsync();
        Assert.Equal("ett-resultat", Assert.Single((await tjeneste.EksporterAsync()).Regler).Loep);
        Assert.Equal(1, port.Importer);
        await TaBilder(side, "suksess-etter-venting");

        var beholdt = (await tjeneste.EksporterAsync()).Regler;
        await Fil(side).SetInputFilesAsync(JsonFil("etter-lagringsfeil"));
        await Expect(Preview(side)).ToContainTextAsync("etter-lagringsfeil.json");
        await using var db = await databaseFactory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER avvis_ux87_ventestatus AFTER DELETE ON Gjentaksregler BEGIN SELECT RAISE(ABORT, 'Hemmelige databasedetaljer'); END;");
        await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
        port.Forsink("import");
        try
        {
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 1 regler", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Fil(side)).ToBeDisabledAsync();
            await side.GetByRole(AriaRole.Link, new() { Name = "Prøv malen", Exact = true }).EvaluateAsync("a => a.click()");
            await Expect(ventestatus).ToBeVisibleAsync();
            Assert.EndsWith("/admin/mal/overfor", side.Url);
        }
        finally { port.Fortsett.TrySetResult(); }
        await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Importen kunne ikke lagres");
        var etterLagringsfeil = await ventestatus.CountAsync();
        await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Fil og forhåndsvisning er beholdt");
        await Expect(side.Locator("main")).Not.ToContainTextAsync("Hemmelige databasedetaljer");
        await Expect(Preview(side)).ToContainTextAsync("etter-lagringsfeil.json");
        await Expect(Fil(side)).ToBeEnabledAsync();
        await Expect(Knapp(side, "Erstatt hele årsmalen")).ToBeEnabledAsync();
        Assert.Equal(beholdt, (await tjeneste.EksporterAsync()).Regler);
        Assert.Equal(2, port.Importer);
        await TaBilder(side, "lagringsfeil-etter-venting");

        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_ux87_ventestatus;");
        await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 1 regler", Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Årsmalen er erstattet", Exact = true })).ToBeVisibleAsync();
        var etterNyttForsoek = await ventestatus.CountAsync();
        await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        Assert.Equal("etter-lagringsfeil", Assert.Single((await tjeneste.EksporterAsync()).Regler).Loep);
        Assert.Equal(3, port.Importer);
        Assert.Equal(new[] { 0, 0, 0 }, new[] { etterSuksess, etterLagringsfeil, etterNyttForsoek });
        await TaBilder(side, "nytt-forsoek");
        await side.GetByRole(AriaRole.Link, new() { Name = "Se gjentaksreglene", Exact = true }).ClickAsync();
        await Expect(side.Locator("h1")).ToHaveTextAsync("Eksisterende gjentaksregler");
    });

    [Fact]
    public Task Kjent_endret_regelantall_for_commit_oppdaterer_konsekvens_og_krever_ny_bekreftelse() => Kjor(async (side, tjeneste, port) =>
    {
        await Fil(side).SetInputFilesAsync(JsonFil("endelig"));
        await Expect(Preview(side)).ToBeVisibleAsync();
        await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
        port.Forsink("preview");
        await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 1 regler", Exact = true }).ClickAsync();
        try
        {
            await port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var konkurrerende = new MalDatabase { Regler = [Regel("annen-a"), Regel("annen-b")] };
            await tjeneste.ImporterAsync(konkurrerende);
            port.Fortsett.TrySetResult();
            await Expect(side.Locator("main")).ToContainTextAsync("Antallet lagrede regler er endret");
            await Expect(side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
            Assert.Equal(konkurrerende.Regler.OrderBy(r => r.Loep), (await tjeneste.EksporterAsync()).Regler);
            Assert.Equal(0, port.Importer);
            await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
            await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alle 2 regler", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Årsmalen er erstattet", Exact = true })).ToBeVisibleAsync();
            Assert.Equal("endelig", Assert.Single((await tjeneste.EksporterAsync()).Regler).Loep);
            Assert.Equal(1, port.Importer);
        }
        finally { port.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData("avbryt")]
    [InlineData("escape")]
    [InlineData("tilbake")]
    public Task Avbryt_Escape_og_tilbake_fjerner_bekreftbar_fil_uten_mutasjon(string handling) => Kjor(async (side, tjeneste, port) =>
    {
        var gammel = (await tjeneste.EksporterAsync()).Regler;
        await Fil(side).SetInputFilesAsync(JsonFil("avbrytes"));
        await Expect(Preview(side)).ToBeVisibleAsync();
        if (handling == "tilbake")
        {
            await side.GetByRole(AriaRole.Link, new() { Name = "Eksisterende gjentaksregler", Exact = true }).ClickAsync();
            await Expect(side.Locator("h1")).ToHaveTextAsync("Eksisterende gjentaksregler");
            await side.GoBackAsync();
            await Expect(side.Locator("h1")).ToHaveTextAsync("Overfør mal");
            await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        }
        else
        {
            await Knapp(side, "Erstatt hele årsmalen").ClickAsync();
            var dialog = side.GetByRole(AriaRole.Dialog);
            await Expect(dialog).ToBeVisibleAsync();
            if (handling == "escape") await side.Keyboard.PressAsync("Escape");
            else await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
            await Expect(dialog).ToBeHiddenAsync();
        }
        await Expect(Preview(side)).ToHaveCountAsync(0);
        Assert.Equal(gammel, (await tjeneste.EksporterAsync()).Regler);
        Assert.Equal(0, port.Importer);
    });

    private static Task Kjor(Func<IPage, IMalDatautveksling, Port, Task> scenario) =>
        Kjor((side, tjeneste, port, _) => scenario(side, tjeneste, port));

    private static async Task TaBilder(IPage side, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "maloverfoering-ventestatus");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }

    private static async Task Kjor(Func<IPage, IMalDatautveksling, Port, IDbContextFactory<AppDbContext>, Task> scenario)
    {
        await using var app = new PortApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        // Den ekte tjenesten er observasjonsport; dekoratøren styrer bare asynkron fullføring.
        var databaseFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        IMalDatautveksling tjeneste = new MalDatautveksling(databaseFactory, TimeProvider.System);
        await tjeneste.ImporterAsync(new() { Regler = [Regel("gammel")] });
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
        await side.GotoAsync("/admin/mal/overfor");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await scenario(side, tjeneste, app.Port, databaseFactory);
        Assert.Empty(feil);
    }
    private static ILocator Fil(IPage side) => side.GetByLabel("Fil (JSON)", new() { Exact = true });
    private static ILocator Preview(IPage side) => side.GetByRole(AriaRole.Region, new() { Name = "Importforhåndsvisning", Exact = true });
    private static ILocator Knapp(IPage side, string navn) => side.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true });
    private static MalRegelEksport Regel(string loep) => new() { Id = Guid.NewGuid(), Loep = loep, Tittel = "UX87 " + loep, Regelparametre = "{\"maaned\":5,\"dag\":2}" };
    private static FilePayload JsonFil(string navn) => new() { Name = navn + ".json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new MalDatabase { Regler = [Regel(navn)] }, Aarshjul.Web.Api.FristEndepunkter.DatabaseJson)) };

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
                services.RemoveAll<IMalDatautveksling>();
                services.AddScoped<IMalDatautveksling>(sp => new ForsinketUtveksling(new MalDatautveksling(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
            });
        }
    }
    private sealed class Port
    {
        private string? _neste;
        public int Importer;
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
    private sealed class ForsinketUtveksling(IMalDatautveksling indre, Port port) : IMalDatautveksling
    {
        public Task<MalDatabase> EksporterAsync(CancellationToken ct = default) => indre.EksporterAsync(ct);
        public async Task<MalImportForhaandsvisning> ForhaandsvisAsync(MalDatabase database, CancellationToken ct = default)
        {
            var forsinket = await port.Vent("preview", ct);
            try { return await indre.ForhaandsvisAsync(database, ct); }
            finally { if (forsinket) port.Fullfoert.TrySetResult(); }
        }
        public async Task<ImportResultat> ImporterAsync(MalDatabase database, CancellationToken ct = default)
        {
            Interlocked.Increment(ref port.Importer);
            await port.Vent("import", ct);
            return await indre.ImporterAsync(database, ct);
        }
    }
}
