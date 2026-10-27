using System.Collections.Concurrent;
using Aarshjul.Application.Generering;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class MalregelNavigasjonTester
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public Task Ny_rute_laster_sitt_eget_utkast_og_lagrer_bare_maalregelen(bool kopi, bool ny, bool sisteFoerst) => Kjor(async (app, side, mal, a, b) =>
    {
        var foer = await mal.HentAlleAsync();
        var eldre = app.Port.Forsink(a);
        Ventepunkt? siste = null;
        try
        {
            await Naviger(side, kopi ? $"/admin/mal/ny?kopierFra={a}" : $"/admin/mal/{a}");
            await eldre.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            siste = ny ? app.Port.ForsinkListe() : app.Port.Forsink(b);
            await Naviger(side, ny ? "/admin/mal/ny" : $"/admin/mal/{b}");
            await siste.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = ny ? "Nye gjentaksregler" : "Rediger gjentaksregel", Exact = true })).ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true })).ToHaveCountAsync(0);
            Assert.Equal(foer, await mal.HentAlleAsync());
            if (!kopi && !ny && sisteFoerst) await TaBilder(side, "laster-b");
            if (sisteFoerst)
            {
                siste.Fortsett.TrySetResult();
                await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(ny ? "" : "Original B");
            }
            eldre.Fortsett.TrySetResult();
            await eldre.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!sisteFoerst)
            {
                await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("Laster gjentaksregel");
                await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true })).ToHaveCountAsync(0);
                siste.Fortsett.TrySetResult();
            }
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(ny ? "" : "Original B");
            await Expect(side.GetByLabel("Dag", new() { Exact = true })).ToHaveValueAsync(ny ? "1" : "23");
            await Expect(side.GetByLabel("Måned", new() { Exact = true })).ToHaveValueAsync(ny ? "1" : "11");
            if (!kopi && !ny && sisteFoerst) await TaBilder(side, "b-etter-sent-a");
            var tittel = ny ? "Ny regel C" : "Redigert B";
            await side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
            await side.GetByLabel("Første budsjettår", new() { Exact = true }).FillAsync("2032");
            await Expect(side.GetByRole(AriaRole.Rowheader, new() { Name = "2032", Exact = true })).ToBeVisibleAsync();
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(tittel);
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
            Assert.Equal("Original A", (await mal.HentForRedigeringAsync(a))!.Tittel);
            var etter = await mal.HentAlleAsync();
            if (ny)
            {
                Assert.Equal(foer, etter.Where(r => r.Id == a || r.Id == b));
                Assert.Equal("Ny regel C", Assert.Single(etter, r => r.Id != a && r.Id != b).Tittel);
            }
            else
            {
                Assert.Equal(foer.Where(r => r.Id != b), etter.Where(r => r.Id != b));
                Assert.Equal(foer.Single(r => r.Id == b) with { Tittel = "Redigert B" }, etter.Single(r => r.Id == b));
            }
        }
        finally { eldre.Fortsett.TrySetResult(); siste?.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData("liste", false)]
    [InlineData("liste", true)]
    [InlineData("regel", false)]
    [InlineData("regel", true)]
    [InlineData("preview", false)]
    [InlineData("preview", true)]
    public Task Sene_svar_og_feil_fra_forlatt_rute_endrer_ikke_siste_regels_felt_eller_preview(string steg, bool feil) => Kjor(async (app, side, mal, a, b) =>
    {
        var foer = await mal.HentAlleAsync();
        var eldre = steg switch { "liste" => app.Port.ForsinkListe(), "preview" => app.Port.ForsinkPreview(a), _ => app.Port.Forsink(a) };
        try
        {
            await Naviger(side, $"/admin/mal/{a}");
            await eldre.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/mal/{b}");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await side.GetByLabel("Første budsjettår", new() { Exact = true }).FillAsync("2032");
            await Expect(side.GetByRole(AriaRole.Rowheader, new() { Name = "2032", Exact = true })).ToBeVisibleAsync();
            if (feil) eldre.Fortsett.TrySetException(new InvalidOperationException("Kontrollert feil i forlatt rute"));
            else eldre.Fortsett.TrySetResult();
            await eldre.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await Expect(side.GetByLabel("Dag", new() { Exact = true })).ToHaveValueAsync("23");
            await Expect(side.GetByLabel("Måned", new() { Exact = true })).ToHaveValueAsync("11");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            await Expect(side.GetByText("Forhåndsvisningen kunne ikke beregnes akkurat nå.", new() { Exact = false })).ToHaveCountAsync(0);
            await Expect(side.GetByRole(AriaRole.Table, new() { Name = "Forhåndsvisning for fem budsjettår", Exact = true })).ToContainTextAsync("23. november 2032");
            Assert.Equal(foer, await mal.HentAlleAsync());
        }
        finally { eldre.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Navigasjon_og_ny_innsending_venter_paa_lagring_av_det_opprinnelige_maalet() => Kjor(async (app, side, mal, a, b) =>
    {
        var foer = await mal.HentAlleAsync();
        await Naviger(side, $"/admin/mal/{a}");
        await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original A");
        await side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("Lagret A");
        var lagring = app.Port.ForsinkLagring();
        try
        {
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
            await lagring.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/mal/{b}");
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/admin/mal/{a}$"));
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Lagret A");
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Lagrer regel …", Exact = true })).ToBeDisabledAsync();
            await side.GetByRole(AriaRole.Form, new() { Name = "Gjentaksregel", Exact = true }).EvaluateAsync("el => el.requestSubmit()");
            Assert.Equal(foer, await mal.HentAlleAsync());
            lagring.Fortsett.TrySetResult();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
            var etter = await mal.HentAlleAsync();
            Assert.Equal(foer.Single(r => r.Id == a) with { Tittel = "Lagret A" }, etter.Single(r => r.Id == a));
            Assert.Equal(foer.Single(r => r.Id == b), etter.Single(r => r.Id == b));
            Assert.Equal(foer.Count, etter.Count);
        }
        finally { lagring.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Sent_preview_etter_at_editoren_er_lukket_har_ingen_sideeffekter() => Kjor(async (app, side, mal, a, b) =>
    {
        var foer = await mal.HentAlleAsync();
        var preview = app.Port.ForsinkPreview(a);
        try
        {
            await Naviger(side, $"/admin/mal/{a}");
            await preview.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await side.GetByRole(AriaRole.Link, new() { Name = "Tilbake til regellisten", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
            preview.Fortsett.TrySetException(new InvalidOperationException("Kontrollert sent preview etter lukking"));
            await preview.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/mal/{b}");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            Assert.Equal(foer, await mal.HentAlleAsync());
        }
        finally { preview.Fortsett.TrySetResult(); }
    });

    private static async Task Kjor(Func<ForsinketMalApplikasjon, IPage, IMaltjeneste, Guid, Guid, Task> scenario)
    {
        await using var app = new ForsinketMalApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var mal = new Maltjeneste(scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>());
        var a = await mal.OpprettAsync(new() { Tittel = "Original A", Loep = "original-a", Regelparametre = "{\"maaned\":3,\"dag\":20}" });
        var b = await mal.OpprettAsync(new() { Tittel = "Original B", Loep = "original-b", Regelparametre = "{\"maaned\":11,\"dag\":23}" });
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
        await side.GotoAsync("/admin/mal");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await scenario(app, side, mal, a, b);
        Assert.Empty(feil);
    }

    // En vanlig lokal lenke går gjennom Blazors navigasjonsvern og gjenbruker kretsen.
    private static Task Naviger(IPage side, string uri) => side.EvaluateAsync("uri => { const a = document.createElement('a'); a.href = uri; document.body.append(a); a.click(); a.remove(); }", uri);

    private static async Task TaBilder(IPage side, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "malregel-navigasjon");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }

    private sealed class ForsinketMalApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Malport? _port;
        public Malport Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMaltjeneste>();
                services.AddScoped<IMaltjeneste>(sp => new ForsinketMal(
                    new Maltjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }

    private sealed class Ventepunkt
    {
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fullfoert { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Malport
    {
        private readonly ConcurrentDictionary<Guid, Ventepunkt> _venter = new();
        private readonly ConcurrentQueue<Ventepunkt> _lister = new();
        private readonly ConcurrentDictionary<Guid, Ventepunkt> _preview = new();
        private readonly ConcurrentQueue<Ventepunkt> _lagringer = new();
        public Ventepunkt Forsink(Guid id) => _venter[id] = new();
        public Ventepunkt ForsinkPreview(Guid id) => _preview[id] = new();
        public Ventepunkt ForsinkLagring() { var punkt = new Ventepunkt(); _lagringer.Enqueue(punkt); return punkt; }
        public Ventepunkt ForsinkListe() { var punkt = new Ventepunkt(); _lister.Enqueue(punkt); return punkt; }
        public Task VentListe() => _lister.TryDequeue(out var punkt) ? Vent(punkt) : Task.CompletedTask;
        public Task VentLagring() => _lagringer.TryDequeue(out var punkt) ? Vent(punkt) : Task.CompletedTask;
        public Task VentPreview(Guid? id) => id is { } noekkel && _preview.TryRemove(noekkel, out var punkt) ? Vent(punkt) : Task.CompletedTask;
        public async Task Vent(Guid id)
        {
            if (!_venter.TryRemove(id, out var punkt)) return;
            await Vent(punkt);
        }
        private static async Task Vent(Ventepunkt punkt)
        {
            punkt.Startet.TrySetResult();
            // Fullfør selv om en forlatt visning kanselleres: UI må også tåle sene svar.
            try { await punkt.Fortsett.Task; }
            finally { punkt.Fullfoert.TrySetResult(); }
        }
    }

    private sealed class ForsinketMal(IMaltjeneste indre, Malport port) : IMaltjeneste
    {
        public async Task<IReadOnlyList<MalRegelDto>> HentAlleAsync(CancellationToken ct = default)
        {
            var data = await indre.HentAlleAsync(ct);
            await port.VentListe();
            return data;
        }
        public async Task<MalRegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default)
        {
            var data = await indre.HentForRedigeringAsync(id, ct);
            await port.Vent(id);
            return data;
        }
        public async Task<IReadOnlyList<MalRegelForhaandsvisning>> ForhaandsvisAsync(MalRegelInndata data, int aar, Guid? id = null, CancellationToken ct = default)
        {
            var resultat = await indre.ForhaandsvisAsync(data, aar, id, ct);
            await port.VentPreview(id);
            return resultat;
        }
        public Task<Guid> OpprettAsync(MalRegelInndata data, CancellationToken ct = default) => indre.OpprettAsync(data, ct);
        public async Task OppdaterAsync(Guid id, MalRegelInndata data, CancellationToken ct = default)
        {
            await port.VentLagring();
            await indre.OppdaterAsync(id, data, ct);
        }
        public Task SlettAsync(Guid id, CancellationToken ct = default) => indre.SlettAsync(id, ct);
    }
}
