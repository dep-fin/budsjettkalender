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

// Samme offentlige nettlesergrense. Kun fullføring/feil og autentisering styres;
// ekte IFristlesing filtrerer alle data i SQLite før svarene kan holdes tilbake.
public sealed class KalendersoekKapploepTester
{
    [Fact]
    public Task Aarsvalg_beholder_tastaturfokus_gjennom_forsinket_oppdatering() => Prove.Kjor(async p =>
    {
        var steg = p.App.Kontroll.Neste("aar");
        var aarsvalg = p.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        var aar = p.Side.GetByLabel("2031", new() { Exact = true });
        await aar.FocusAsync();
        await aar.PressAsync("Space");
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(aar).ToHaveCountAsync(0);
        await Expect(aarsvalg).ToBeFocusedAsync();
        await Expect(p.Treff.Locator(".listefrist")).ToHaveCountAsync(0);
        await Expect(p.Status).ToHaveCountAsync(0);
        steg.Fortsett.TrySetResult();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Expect(aar).Not.ToBeCheckedAsync();
        await Expect(aar).ToBeFocusedAsync();
        await aar.PressAsync("Space");
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        await Expect(aar).ToBeFocusedAsync();
    });

    [Theory]
    [InlineData("aar")]
    [InlineData("liste")]
    public Task Feil_etter_aarsvalg_beholder_fokus_i_aarsgruppen_uten_gamle_valg_eller_treff(string operasjon) => Prove.Kjor(async p =>
    {
        var steg = p.App.Kontroll.Neste(operasjon, feil: true);
        var aarsvalg = p.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await p.Side.GetByLabel("2031", new() { Exact = true }).PressAsync("Space");
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(aarsvalg).ToBeFocusedAsync();
        steg.Fortsett.TrySetResult();
        await Expect(p.Treff.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå");
        await Expect(aarsvalg.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(0);
        await Expect(aarsvalg).ToBeFocusedAsync();
        await Expect(p.Treff.Locator(".listefrist")).ToHaveCountAsync(0);
        await Expect(p.Status).ToHaveCountAsync(0);
        Assert.DoesNotContain("RACE139-RÅ-DATABASEFEIL", await p.Side.ContentAsync());
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Expect(p.Side.GetByLabel("2031", new() { Exact = true })).Not.ToBeCheckedAsync();
    });

    [Theory]
    [InlineData("gruppe")]
    [InlineData("utlogget")]
    [InlineData("sletting")]
    public Task Bortfalt_fokusert_aar_beholder_fokus_i_aarsgruppen_uten_uautoriserte_valg(string endring) => Prove.Kjor(async p =>
    {
        if (endring == "gruppe") p.App.Innlogging.Gruppe = "FAG";
        else if (endring == "utlogget") p.App.Innlogging.Innlogget = false;
        else
        {
            await using var db = await p.Fabrikk.CreateDbContextAsync();
            db.Frister.Remove(await db.Frister.SingleAsync(f => f.Id == p.Id));
            await db.SaveChangesAsync();
        }
        var steg = p.App.Kontroll.Neste("aar");
        var aarsvalg = p.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        var aar = p.Side.GetByLabel("2031", new() { Exact = true });
        await aar.PressAsync("Space");
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(aar).ToHaveCountAsync(0);
        await Expect(aarsvalg).ToBeFocusedAsync();
        await Expect(p.Status).ToHaveCountAsync(0);
        await Expect(p.Treff.Locator(".listefrist")).ToHaveCountAsync(0);
        steg.Fortsett.TrySetResult();
        await Expect(p.Status).ToHaveTextAsync(endring == "sletting" ? "Side 1 av 1 · 1 frist" : "Side 0 av 0 · 0 frister");
        await Expect(aar).ToHaveCountAsync(0);
        await Expect(aarsvalg).ToBeFocusedAsync();
        if (endring != "sletting") await Expect(p.Side.GetByLabel("2032", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.DoesNotContain("RACE139 opprinnelig", await p.Side.ContentAsync());
        Assert.DoesNotContain("RACE139 privat notat", await p.Side.ContentAsync());
        await p.Side.Keyboard.PressAsync("Tab");
        await Expect(endring == "utlogget"
            ? aarsvalg.GetByRole(AriaRole.Button, new() { Name = "Velg alle", Exact = true })
            : p.Side.GetByLabel("2028", new() { Exact = true })).ToBeFocusedAsync();
    });

    [Fact]
    public Task Aarsvalg_stjeler_ikke_fokus_nar_brukeren_gaar_videre_under_ventingen() => Prove.Kjor(async p =>
    {
        var steg = p.App.Kontroll.Neste("aar");
        var aarsvalg = p.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await p.Side.GetByLabel("2031", new() { Exact = true }).PressAsync("Space");
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(aarsvalg).ToBeFocusedAsync();
        await Expect(aarsvalg.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(0);
        await p.Side.Keyboard.PressAsync("Tab");
        var neste = aarsvalg.GetByRole(AriaRole.Button, new() { Name = "Velg alle", Exact = true });
        await Expect(neste).ToBeFocusedAsync();
        steg.Fortsett.TrySetResult();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Expect(neste).ToBeFocusedAsync();
    });

    [Theory]
    [InlineData("aar", false)]
    [InlineData("aar", true)]
    [InlineData("soek", false)]
    [InlineData("retur", false)]
    public Task Eldre_svar_kan_ikke_overta_nyere_soek_filter_eller_kalenderretur(string endring, bool feil) => Prove.Kjor(async p =>
    {
        var gammel = p.App.Kontroll.Neste("liste", feil);
        var ferdig = p.Leveranse.NesteKvittering("DispatchEventAsync");
        await p.Soek.FillAsync(" RACE139 ");
        await gammel.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (endring == "aar")
        {
            await p.Side.GetByLabel("2031", new() { Exact = true }).UncheckAsync();
            await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
            await Expect(p.Treff).ToContainTextAsync("RACE139 neste år");
        }
        else if (endring == "soek")
        {
            await p.Soek.FillAsync("neste år");
            await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        }
        else
        {
            await p.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til kalenderen", Exact = true }).ClickAsync();
            await Expect(p.Side.GetByRole(AriaRole.Region, new() { Name = "Månedskalender", Exact = true })).ToContainTextAsync("0 frister i måneden");
        }
        gammel.Fortsett.TrySetResult();
        await ferdig;
        if (endring == "retur") await Expect(p.Treff).ToHaveCountAsync(0);
        else
        {
            await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
            await Expect(p.Treff.Locator(".kalenderliste")).Not.ToContainTextAsync("RACE139 opprinnelig");
        }
        await Expect(p.Side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    });

    [Fact]
    public Task Lastefeil_skjuler_gamle_treff_og_kan_proeves_igjen_uten_raa_feil() => Prove.Kjor(async p =>
    {
        var steg = p.App.Kontroll.Neste("liste", feil: true);
        await p.Soek.FillAsync(" RACE139 ");
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Expect(p.Treff).ToContainTextAsync("Laster frister");
        await Expect(p.Treff.Locator(".listefrist")).ToHaveCountAsync(0);
        await Expect(p.Status).ToHaveCountAsync(0);
        steg.Fortsett.TrySetResult();
        await Expect(p.Treff.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste frister akkurat nå");
        Assert.DoesNotContain("RACE139-RÅ-DATABASEFEIL", await p.Side.ContentAsync());
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 2 frister");
    });

    [Theory]
    [InlineData("POL", false)]
    [InlineData("sletting", false)]
    [InlineData("gruppe", false)]
    [InlineData("utlogget", false)]
    [InlineData("POL", true)]
    [InlineData("sletting", true)]
    public Task Bortfalt_tilgang_eller_sletting_gir_noeytral_melding_og_ferske_treff_ogsaa_ved_kalenderhandling(string endring, bool kalender) => Prove.Kjor(async p =>
    {
        if (endring == "gruppe") p.App.Innlogging.Gruppe = "FAG";
        else if (endring == "utlogget") p.App.Innlogging.Innlogget = false;
        else
        {
            await using var db = await p.Fabrikk.CreateDbContextAsync();
            var f = await db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == p.Id);
            if (endring == "sletting") db.Frister.Remove(f);
            else { f.Synlighet.Clear(); f.Synlighet.Add(new() { GruppeKode = "POL" }); }
            await db.SaveChangesAsync();
        }
        var navn = kalender ? "Vis i kalenderen: RACE139 opprinnelig" : "RACE139 opprinnelig";
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true }).ClickAsync();
        await Expect(p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frist utilgjengelig", Exact = true }))
            .ToContainTextAsync("Fristen er ikke lenger tilgjengelig.");
        await Expect(p.Status).ToHaveTextAsync(endring is "gruppe" or "utlogget" ? "Side 0 av 0 · 0 frister" : "Side 1 av 1 · 1 frist");
        var dom = await p.Side.ContentAsync();
        foreach (var gammel in new[] { "RACE139 opprinnelig", "RACE139 privat notat", "race139-kilde" }) Assert.DoesNotContain(gammel, dom);
        await p.Side.Keyboard.PressAsync("Escape");
        await Expect(p.Side.GetByRole(AriaRole.Heading, new() { Name = "Søketreff", Exact = true })).ToBeFocusedAsync();
        await Expect(p.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
    });

    [Fact]
    public Task Vis_i_kalenderen_bruker_fersk_maaned_og_beholder_siden_ved_returtur() => Prove.Kjor(async p =>
    {
        await using (var db = await p.Fabrikk.CreateDbContextAsync())
        {
            var f = await db.Frister.SingleAsync(f => f.Id == p.Id);
            f.Dato = new(2035, 4, 19); f.Datopresisjon = Datopresisjon.Maaned; f.Datokvalifikator = Datokvalifikator.Ultimo;
            await db.SaveChangesAsync();
        }
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen: RACE139 opprinnelig", Exact = true }).ClickAsync();
        await Expect(p.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2035-04");
        await Expect(p.Side.GetByRole(AriaRole.Region, new() { Name = "Tentative frister i april 2035", Exact = true })).ToContainTextAsync("RACE139 opprinnelig");
        await p.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til søketreff", Exact = true }).ClickAsync();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 2 frister");
    });

    [Fact]
    public Task Forsinket_kalenderhandling_kan_ikke_forlate_et_nyere_soek() => Prove.Kjor(async p =>
    {
        var gammel = p.App.Kontroll.Neste("detalj");
        var ferdig = p.Leveranse.NesteKvittering("DispatchEventAsync");
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen: RACE139 opprinnelig", Exact = true }).ClickAsync();
        await gammel.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await p.Soek.FillAsync("neste år");
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        gammel.Fortsett.TrySetResult();
        await ferdig;
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Expect(p.Treff).ToContainTextAsync("RACE139 neste år");
    });

    [Fact]
    public Task Forsinkede_aarsvalg_kan_ikke_gjeninnfoere_tidligere_tilgang() => Prove.Kjor(async p =>
    {
        var gammel = p.App.Kontroll.Neste("aar");
        var ferdig = p.Leveranse.NesteKvittering("DispatchEventAsync");
        await p.Soek.FillAsync(" RACE139 ");
        await gammel.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        p.App.Innlogging.Gruppe = "FAG";
        await p.Soek.FillAsync("RACE139");
        await Expect(p.Status).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        await Expect(p.Side.GetByLabel("2031", new() { Exact = true })).ToHaveCountAsync(0);
        gammel.Fortsett.TrySetResult();
        await ferdig;
        await Expect(p.Side.GetByLabel("2031", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(p.Status).ToHaveTextAsync("Side 0 av 0 · 0 frister");
    });

    [Theory]
    [InlineData("aar")]
    [InlineData("kategori")]
    public Task Frist_som_har_flyttet_ut_av_valgt_utvalg_utvider_ikke_filtrene_ved_kalenderhandling(string endring) => Prove.Kjor(async p =>
    {
        await using (var db = await p.Fabrikk.CreateDbContextAsync())
        {
            var f = await db.Frister.SingleAsync(f => f.Id == p.Id);
            if (endring == "aar") f.Budsjettaar = 2042;
            else f.Kategori = Kategori.Regnskap;
            await db.SaveChangesAsync();
        }
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen: RACE139 opprinnelig", Exact = true }).ClickAsync();
        await Expect(p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frist utilgjengelig", Exact = true })).ToBeVisibleAsync();
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Expect(p.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
        if (endring == "aar") await Expect(p.Side.GetByLabel("2042", new() { Exact = true })).Not.ToBeCheckedAsync();
        Assert.DoesNotContain("RACE139 privat notat", await p.Side.ContentAsync());
    });

    [Fact]
    public Task Lese_feil_ved_kalenderhandling_gir_en_noeytral_melding_uten_gamle_detaljer() => Prove.Kjor(async p =>
    {
        var steg = p.App.Kontroll.Neste("detalj", feil: true);
        await p.Treff.GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen: RACE139 opprinnelig", Exact = true }).ClickAsync();
        await steg.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        steg.Fortsett.TrySetResult();
        await Expect(p.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frist utilgjengelig", Exact = true })).ToContainTextAsync("Fristen er ikke lenger tilgjengelig.");
        await Expect(p.Status).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        var dom = await p.Side.ContentAsync();
        Assert.DoesNotContain("RACE139 privat notat", dom);
        Assert.DoesNotContain("RACE139-RÅ-DATABASEFEIL", dom);
    });

    private sealed class Prove : IAsyncDisposable
    {
        public readonly Applikasjon App = new();
        private IPlaywright _playwright = null!;
        private IBrowser _browser = null!;
        private HttpClient _klient = null!;
        private readonly List<string> _feil = [];
        public readonly Gruppeprove.Blazorleveranse Leveranse = new();
        public IPage Side { get; private set; } = null!;
        public Guid Id { get; } = Guid.NewGuid();
        public IDbContextFactory<AppDbContext> Fabrikk => App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        public ILocator Treff => Side.GetByRole(AriaRole.Region, new() { Name = "Søketreff", Exact = true });
        public ILocator Status => Treff.GetByRole(AriaRole.Navigation, new() { Name = "Sider med frister", Exact = true }).GetByRole(AriaRole.Status);
        public ILocator Soek => Side.GetByRole(AriaRole.Searchbox);

        public static async Task Kjor(Func<Prove, Task> scenario)
        {
            await using var p = new Prove();
            await p.Start();
            await scenario(p);
            Assert.Empty(p._feil);
        }

        private async Task Start()
        {
            _klient = App.CreateClient();
            await using (var db = await Fabrikk.CreateDbContextAsync())
            {
                db.Frister.Add(new Frist { Id = Id, Tittel = "RACE139 opprinnelig", Notat = "RACE139 privat notat", ManuellKildeUrl = "https://www.regjeringen.no/no/race139-kilde",
                    Dato = new(2020, 1, 1), Budsjettaar = 2031, Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = "FA" }] });
                db.Frister.Add(new Frist { Id = Guid.NewGuid(), Tittel = "RACE139 neste år", Dato = new(2032, 1, 1), Budsjettaar = 2032,
                    Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = "FA" }] });
                await db.SaveChangesAsync();
            }
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
            var ctx = await _browser.NewContextAsync(new() { BaseURL = _klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Leser", ["X-Test-Grupper"] = "FA" } });
            Side = await ctx.NewPageAsync();
            Side.SetDefaultTimeout(10_000);
            Side.PageError += (_, feil) => _feil.Add(feil);
            await Side.RouteWebSocketAsync("**/_blazor**", ws =>
            {
                var server = ws.ConnectToServer();
                ws.OnMessage(frame => { if (frame.Binary is not null) { Leveranse.Sendt(frame.Binary); server.Send(frame.Binary); } else server.Send(frame.Text!); });
                server.OnMessage(frame => { if (frame.Binary is not null) { ws.Send(frame.Binary); Leveranse.Svart(frame.Binary); } else ws.Send(frame.Text!); });
            });
            await Side.GotoAsync("/kalender");
            await Expect(Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
            await Side.GetByLabel("Vis måned", new() { Exact = true }).FillAsync("2030-11");
            await Soek.FillAsync("RACE139");
            await Soek.PressAsync("Enter");
            await Expect(Status).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        }

        public async ValueTask DisposeAsync()
        {
            App.Kontroll.SlippAlle();
            if (_browser is not null) await _browser.DisposeAsync();
            _playwright?.Dispose(); _klient?.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class Applikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Innlogging? _innlogging;
        private Kontroll? _kontroll;
        public Innlogging Innlogging => _innlogging ??= new();
        public Kontroll Kontroll => _kontroll ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); builder.UseStaticWebAssets();
            var innlogging = Innlogging; var kontroll = Kontroll;
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<AuthenticationStateProvider>(); s.AddSingleton<AuthenticationStateProvider>(innlogging);
                s.RemoveAll<IFristlesing>();
                s.AddScoped<IFristlesing>(sp => new Lesing(new FristTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), kontroll));
            });
        }
    }

    private sealed class Steg(bool feil)
    {
        public bool Feil => feil;
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Kontroll
    {
        private (string Operasjon, Steg Steg)? _neste;
        private readonly List<Steg> _alle = [];
        public Steg Neste(string operasjon, bool feil = false) { var s = new Steg(feil); _alle.Add(s); _neste = (operasjon, s); return s; }
        public async Task Vent(string operasjon)
        {
            if (_neste is not { } n || n.Operasjon != operasjon) return;
            _neste = null; n.Steg.Startet.TrySetResult();
            await n.Steg.Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (n.Steg.Feil) throw new InvalidOperationException("RACE139-RÅ-DATABASEFEIL");
        }
        public void SlippAlle() { foreach (var s in _alle) s.Fortsett.TrySetResult(); }
    }

    private sealed class Lesing(IFristlesing indre, Kontroll kontroll) : IFristlesing
    {
        public async Task<IReadOnlyList<FristDto>> HentSynligeAsync(ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default)
        { var data = await indre.HentSynligeAsync(ctx, filter, ct); await kontroll.Vent("liste"); return data; }
        public async Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid id, CancellationToken ct = default)
        { var data = await indre.HentEnAsync(ctx, id, ct); await kontroll.Vent("detalj"); return data; }
        public Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(ISynlighetskontekst ctx, DateOnly fra, FristFilter filter, CancellationToken ct = default) => indre.HentLandingsutvalgAsync(ctx, fra, filter, ct);
        public async Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(ISynlighetskontekst ctx, bool historikk, CancellationToken ct = default)
        { var data = await indre.HentTilgjengeligeBudsjettaarAsync(ctx, historikk, ct); await kontroll.Vent("aar"); return data; }
    }

    private sealed class Innlogging : ServerAuthenticationStateProvider
    {
        public string Gruppe = "FA";
        public bool Innlogget = true;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, "race139"), new(Brukerclaims.Rolle, "Leser"), new(Brukerclaims.Gruppe, Gruppe)], Innlogget ? "Test" : null))));
    }
}
