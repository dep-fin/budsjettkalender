using System.Collections.Concurrent;
using Aarshjul.Application.Brukere;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

// Ekte Kestrel/Blazor/SQLite. Testporten kan holde eller feile en faktisk lesing, aldri finne på personer.
internal sealed class Administratorprove : IAsyncDisposable
{
    public readonly Administratorapplikasjon App = new();
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private HttpClient _klient = null!;
    private readonly ConcurrentQueue<string> _feil = new();
    public IPage Side { get; private set; } = null!;
    public IDbContextFactory<AppDbContext> Fabrikk => App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    public Port Kontroll => App.Kontroll;
    public ILocator Oversikt => Side.GetByRole(AriaRole.Region, new() { Name = "Administratorer", Exact = true });
    public ILocator Oppdater => Oversikt.GetByRole(AriaRole.Button, new() { Name = "Oppdater oversikt", Exact = true });

    public static async Task Kjor(Func<Administratorprove, Task> scenario)
    {
        await using var p = new Administratorprove();
        await p.Start();
        await scenario(p);
        Assert.Empty(p._feil);
    }

    private async Task Start()
    {
        _klient = App.CreateClient();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        Side = await NySide("Administrator", "ux92-egen");
    }

    public async Task<IPage> NySide(string? rolle, string id)
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = _klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = rolle is null ? new Dictionary<string, string>() : new() { ["X-Test-Rolle"] = rolle, ["X-Test-Bruker"] = id, ["X-Test-Grupper"] = "FA" } });
        var side = await context.NewPageAsync();
        side.SetDefaultTimeout(10_000);
        side.PageError += (_, e) => _feil.Enqueue(e);
        // 401/403 ved bevisst nektet dokumentlesing er forventet. Kretsfeil er det aldri.
        return side;
    }

    public async Task Gaa()
    {
        await Side.GotoAsync("/admin/innstillinger?bruker=ux92-annen");
        await Expect(Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await Expect(Oversikt).ToHaveAttributeAsync("aria-busy", "false");
    }

    public async Task Bilder(string navn)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "administratoroversikt"); Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            await Side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{navn}-{bredde}.png"), FullPage = true });
        }
        await Side.SetViewportSizeAsync(1440, 1000);
    }

    public async ValueTask DisposeAsync()
    {
        Kontroll.SlippAlle();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose(); _klient?.Dispose(); await App.DisposeAsync();
    }

    internal sealed class Administratorapplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Port? _kontroll;
        public Port Kontroll => _kontroll ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); builder.UseStaticWebAssets();
            var port = Kontroll;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdministratoroversikt>();
                services.AddScoped<IAdministratoroversikt>(sp => new KontrollertOversikt(
                    new AdministratoroversiktTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }

    internal sealed class Steg(bool feil)
    {
        public bool Feil => feil;
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task VentStart() => Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Slipp() => Fortsett.TrySetResult();
    }

    internal sealed class Port
    {
        private Steg? _neste;
        private readonly List<Steg> _alle = [];
        public Steg Neste(bool feil = false)
        {
            var steg = new Steg(feil);
            Assert.Null(Interlocked.CompareExchange(ref _neste, steg, null));
            _alle.Add(steg); return steg;
        }
        public async Task Vent(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _neste, null) is not { } steg) return;
            steg.Startet.TrySetResult();
            await steg.Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            if (steg.Feil) throw new InvalidOperationException("UX92 intern databasefeil skal ikke sendes til klienten");
        }
        public void SlippAlle() { foreach (var steg in _alle) steg.Slipp(); }
    }

    private sealed class KontrollertOversikt(IAdministratoroversikt indre, Port port) : IAdministratoroversikt
    {
        public async Task<IReadOnlyList<AdministratortilgangDto>> HentAsync(CancellationToken ct = default)
        {
            var resultat = await indre.HentAsync(ct);
            await port.Vent(ct);
            return resultat;
        }
    }
}
