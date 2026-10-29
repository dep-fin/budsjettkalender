using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
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

// Eksisterende nettlesergrense: ekte Kestrel/Blazor og relasjonell SQLite.
public sealed class FristdetaljTester
{
    [Theory]
    [InlineData("kalender", "POL")]
    [InlineData("kalender", "grupper")]
    [InlineData("kalender", "sletting")]
    [InlineData("kalender", "utlogget")]
    [InlineData("budsjettaar", "POL")]
    [InlineData("budsjettaar", "grupper")]
    [InlineData("budsjettaar", "sletting")]
    [InlineData("budsjettaar", "utlogget")]
    public Task Detaljaapning_gjenleser_fersk_tilgang_og_viser_ingen_gamle_detaljer(string flate, string endring) => Prove.Kjor(flate, async p =>
    {
        // Listen er allerede lest av FA. Notat og kildelenke har aldri vært i DOM.
        await Expect(p.Knapp).ToBeVisibleAsync();
        await p.IngenNyeDetaljer();
        if (endring == "utlogget") p.App.Innlogging.Innlogget = false;
        else if (endring == "grupper") p.App.Innlogging.Gruppe = "FAG";
        else
        {
            await using var db = await p.Fabrikk.CreateDbContextAsync();
            var frist = await db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == p.Id);
            if (endring == "sletting") db.Frister.Remove(frist);
            else { frist.Synlighet.Clear(); frist.Synlighet.Add(new() { GruppeKode = "POL" }); }
            await db.SaveChangesAsync();
        }
        await p.Knapp.PressAsync("Enter");
        var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frist utilgjengelig", Exact = true });
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog).ToContainTextAsync("Fristen er ikke lenger tilgjengelig.");
        await Expect(dialog).Not.ToContainTextAsync(Prove.Tittel);
        await p.IngenNyeDetaljer();
        await p.Side.Keyboard.PressAsync("Escape");
        await Expect(p.Knapp).ToBeFocusedAsync();
        await Expect(p.Side.Locator("dialog.fristdialog")).Not.ToContainTextAsync(Prove.Tittel);
        await p.BevarKontekst();
    });

    [Theory]
    [InlineData("kalender", "Lukk")]
    [InlineData("kalender", "Escape")]
    [InlineData("budsjettaar", "Lukk")]
    [InlineData("budsjettaar", "Escape")]
    public Task Lukking_toemmer_hele_dialogens_DOM_og_neste_aapning_henter_nytt_innhold(string flate, string lukking) => Prove.Kjor(flate, async p =>
    {
        await p.Knapp.PressAsync("Enter");
        var dialog = p.Side.GetByRole(AriaRole.Dialog, new() { Name = Prove.Tittel, Exact = true });
        await Expect(dialog).ToContainTextAsync(Prove.Notat);
        await Expect(dialog.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") })).ToHaveAttributeAsync("href", Prove.Kilde);
        if (lukking == "Escape") await p.Side.Keyboard.PressAsync("Escape");
        else await dialog.GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
        await Expect(p.Knapp).ToBeFocusedAsync();
        await Expect(p.Side.Locator("dialog.fristdialog")).Not.ToContainTextAsync(Prove.Tittel);
        await p.IngenNyeDetaljer();
        await p.BevarKontekst();
        await using (var db = await p.Fabrikk.CreateDbContextAsync())
        {
            var frist = await db.Frister.SingleAsync(f => f.Id == p.Id);
            frist.Notat = "DETALJ138 ferskt notat";
            await db.SaveChangesAsync();
        }
        await p.Knapp.PressAsync("Space");
        await Expect(dialog).ToContainTextAsync("DETALJ138 ferskt notat");
        Assert.DoesNotContain(Prove.Notat, await p.Side.ContentAsync());
    });

    [Theory]
    [InlineData("kalender", false)]
    [InlineData("kalender", true)]
    [InlineData("budsjettaar", false)]
    [InlineData("budsjettaar", true)]
    public Task Forsinket_detaljsvar_overtar_verken_lukket_eller_nyere_dialog(string flate, bool aapneNy) => Prove.Kjor(flate, async p =>
    {
        var gammel = p.App.Kontroll.HoldNeste();
        // Kvittering for den opprinnelige UI-hendelsen gir en deterministisk sluttbarriere.
        var ferdig = p.Leveranse.NesteKvittering("DispatchEventAsync");
        await p.Knapp.PressAsync("Enter");
        await gammel.Hentet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Henter fristdetaljer", Exact = true })).ToBeVisibleAsync();
        await p.Leveranse.Kvittert("LukketAsync", () => p.Side.Keyboard.PressAsync("Escape"));
        await Expect(p.Knapp).ToBeFocusedAsync();
        if (aapneNy)
        {
            await using var db = await p.Fabrikk.CreateDbContextAsync();
            var frist = await db.Frister.SingleAsync(f => f.Id == p.Id);
            frist.Notat = "DETALJ138 nyere dialog";
            await db.SaveChangesAsync();
            await p.Knapp.PressAsync("Enter");
            await Expect(p.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("DETALJ138 nyere dialog");
        }
        gammel.Fortsett.TrySetResult();
        await ferdig;
        if (aapneNy)
        {
            await Expect(p.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("DETALJ138 nyere dialog");
            Assert.DoesNotContain(Prove.Notat, await p.Side.ContentAsync());
        }
        else
        {
            await Expect(p.Side.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            await p.IngenNyeDetaljer();
        }
        await p.BevarKontekst();
    });

    private sealed class Prove : IAsyncDisposable
    {
        public const string Tittel = "DETALJ138 opprinnelig frist";
        public const string Notat = "DETALJ138 tidligere hemmelig notat";
        public const string Kilde = "https://www.regjeringen.no/no/detalj138-kildemarkor";
        public readonly Detaljapplikasjon App = new();
        private IPlaywright _playwright = null!;
        private IBrowser _browser = null!;
        private HttpClient _klient = null!;
        private readonly List<string> _feil = [];
        private string _flate = "";
        public readonly Gruppeprove.Blazorleveranse Leveranse = new();
        public IPage Side { get; private set; } = null!;
        public Guid Id { get; } = Guid.NewGuid();
        public IDbContextFactory<AppDbContext> Fabrikk => App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        public ILocator Knapp => Side.GetByRole(AriaRole.Button, new() { Name = Tittel, Exact = true });

        public static async Task Kjor(string flate, Func<Prove, Task> scenario)
        {
            await using var p = new Prove();
            await p.Start(flate);
            await scenario(p);
            Assert.Empty(p._feil);
        }

        private async Task Start(string flate)
        {
            _flate = flate;
            _klient = App.CreateClient();
            await using (var db = await Fabrikk.CreateDbContextAsync())
            {
                var dokument = new BehandletDokument { Id = Guid.NewGuid(), Kilde = "syntetisk", DokumentNokkel = "detalj138", Url = Kilde };
                db.BehandledeDokumenter.Add(dokument);
                db.Frister.Add(new Frist { Id = Id, Tittel = Tittel, Notat = Notat, DokumentId = dokument.Id,
                    Dato = new(2030, 11, 20), Budsjettaar = 2031, Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = "FA" }] });
                await db.SaveChangesAsync();
            }
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
            var kontekst = await _browser.NewContextAsync(new()
            {
                BaseURL = _klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Leser", ["X-Test-Grupper"] = "FA" }
            });
            Side = await kontekst.NewPageAsync();
            Side.SetDefaultTimeout(10_000);
            Side.PageError += (_, feil) => _feil.Add(feil);
            await Side.RouteWebSocketAsync("**/_blazor**", ws =>
            {
                var server = ws.ConnectToServer();
                ws.OnMessage(frame =>
                {
                    if (frame.Binary is not null) { Leveranse.Sendt(frame.Binary); server.Send(frame.Binary); }
                    else server.Send(frame.Text!);
                });
                server.OnMessage(frame =>
                {
                    if (frame.Binary is not null) { ws.Send(frame.Binary); Leveranse.Svart(frame.Binary); }
                    else ws.Send(frame.Text!);
                });
            });
            await Side.GotoAsync("/" + flate);
            await Expect(Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
            if (flate == "kalender")
            {
                await Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync("2030-11");
                await Side.GetByRole(AriaRole.Searchbox).FillAsync("DETALJ138");
                await Side.GetByRole(AriaRole.Navigation, new() { Name = "Kalendervisning", Exact = true })
                    .GetByRole(AriaRole.Button, new() { Name = "Liste", Exact = true }).ClickAsync();
                await Expect(Side.GetByRole(AriaRole.Region, new() { Name = "Kalenderliste", Exact = true }))
                    .ToContainTextAsync("Side 1 av 1 · 1 frist");
            }
            else
            {
                await Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync("2031");
                await Side.GetByRole(AriaRole.Navigation, new() { Name = "Budsjettårvisning", Exact = true })
                    .GetByRole(AriaRole.Button, new() { Name = "Fristliste", Exact = true }).ClickAsync();
                await Expect(Side.GetByRole(AriaRole.Region, new() { Name = "Fristliste for budsjettår 2031", Exact = true }))
                    .ToContainTextAsync(Tittel);
            }
            await Expect(Knapp).ToBeVisibleAsync();
        }

        public async Task IngenNyeDetaljer()
        {
            var dom = await Side.ContentAsync(); // Inkluderer lukkede dialoger og skjulte elementer.
            Assert.DoesNotContain(Notat, dom);
            Assert.DoesNotContain("detalj138-kildemarkor", dom);
        }

        public async Task BevarKontekst()
        {
            if (_flate == "kalender")
            {
                await Expect(Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
                await Expect(Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("DETALJ138");
                await Expect(Side.GetByRole(AriaRole.Region, new() { Name = "Kalenderliste", Exact = true })).ToContainTextAsync("Side 1 av 1 · 1 frist");
            }
            else await Expect(Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true })).ToHaveValueAsync("2031");
        }

        public async ValueTask DisposeAsync()
        {
            App.Kontroll.SlippAlle();
            if (_browser is not null) await _browser.DisposeAsync();
            _playwright?.Dispose(); _klient?.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class Detaljapplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private KontrollertInnlogging? _innlogging;
        private Detaljkontroll? _kontroll;
        public KontrollertInnlogging Innlogging => _innlogging ??= new();
        public Detaljkontroll Kontroll => _kontroll ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); builder.UseStaticWebAssets();
            var innlogging = Innlogging;
            var kontroll = Kontroll;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AuthenticationStateProvider>();
                services.AddSingleton<AuthenticationStateProvider>(innlogging);
                services.RemoveAll<IFristlesing>();
                services.AddScoped<IFristlesing>(sp => new KontrollertLesing(new FristTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), kontroll));
            });
        }
    }

    private sealed class Detaljkontroll
    {
        private Steg? _neste;
        private readonly List<Steg> _alle = [];
        public Steg HoldNeste() { var steg = new Steg(); _alle.Add(steg); _neste = steg; return steg; }
        public Steg? TaNeste() => Interlocked.Exchange(ref _neste, null);
        public void SlippAlle() { foreach (var steg in _alle) steg.Fortsett.TrySetResult(); }
    }

    private sealed class Steg
    {
        public TaskCompletionSource Hentet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // Bare fullføringstidspunktet kontrolleres. Data/synlighet hentes av den ekte leseporten.
    private sealed class KontrollertLesing(IFristlesing indre, Detaljkontroll kontroll) : IFristlesing
    {
        public async Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid id, CancellationToken ct = default)
        {
            var frist = await indre.HentEnAsync(ctx, id, ct);
            if (kontroll.TaNeste() is { } steg)
            {
                steg.Hentet.TrySetResult();
                await steg.Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            return frist;
        }
        public Task<IReadOnlyList<FristDto>> HentSynligeAsync(ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default) => indre.HentSynligeAsync(ctx, filter, ct);
        public Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(ISynlighetskontekst ctx, DateOnly fra, FristFilter filter, CancellationToken ct = default) => indre.HentLandingsutvalgAsync(ctx, fra, filter, ct);
        public Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(ISynlighetskontekst ctx, bool inkluderHistorikk, CancellationToken ct = default) => indre.HentTilgjengeligeBudsjettaarAsync(ctx, inkluderHistorikk, ct);
    }

    private sealed class KontrollertInnlogging : ServerAuthenticationStateProvider
    {
        public string Gruppe = "FA";
        public bool Innlogget = true;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, "detalj138"), new(Brukerclaims.Rolle, "Leser"), new(Brukerclaims.Gruppe, Gruppe)], Innlogget ? "Test" : null))));
    }
}
