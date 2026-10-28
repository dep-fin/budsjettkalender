using System.Collections.Concurrent;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Grupper;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

// Ekte Kestrel/SQLite/Blazor. Porten styrer bare fullføring og feil ved eksisterende tjenestegrenser.
internal sealed class Gruppeprove : IAsyncDisposable
{
    public readonly Gruppeapplikasjon App = new();
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private HttpClient _klient = null!;
    private readonly ConcurrentQueue<string> _feil = new();
    public IPage Side { get; private set; } = null!;
    public Port Kontroll => App.Kontroll;
    public IDbContextFactory<AppDbContext> Fabrikk => App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
    public IFristlesing Lesing => new FristTjeneste(Fabrikk, TimeProvider.System);
    public IGruppetjeneste Grupper => new Gruppetjeneste(Fabrikk);
    public const string KildeUrl = "https://www.regjeringen.no/no/ux89-rundskriv?a=1&b=2";
    public Guid SkjultId { get; private set; }
    public Guid ForslagId { get; private set; }

    public static async Task Kjor(Func<Gruppeprove, Task> scenario)
    {
        await using var p = new Gruppeprove();
        await p.Start();
        await scenario(p);
        Assert.Empty(p._feil);
    }

    private async Task Start()
    {
        _klient = App.CreateClient();
        await Seed();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        var context = await _browser.NewContextAsync(new()
        {
            BaseURL = _klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator", ["X-Test-Grupper"] = "FA,FIN-FAG,FAG,POL" }
        });
        Side = await context.NewPageAsync();
        Side.SetDefaultTimeout(10_000);
        Side.PageError += (_, melding) => _feil.Enqueue(melding);
        Side.Console += (_, melding) => { if (melding.Type == "error") _feil.Enqueue(melding.Text); };
    }

    private async Task Seed()
    {
        await Grupper.OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        await Grupper.OpprettAsync("TOM", "Tom gruppe");
        await Grupper.OpprettAsync("HEMMELIG", "Annen gruppe");
        await using var db = await Fabrikk.CreateDbContextAsync();
        var dokument = new BehandletDokument { Id = Guid.NewGuid(), Kilde = "ux89", DokumentNokkel = "ux89", Url = KildeUrl };
        db.BehandledeDokumenter.Add(dokument);
        Frist LeggTil(string tittel, string kode, int aar, Kategori kategori = Kategori.Budsjett)
        {
            var f = new Frist { Id = Guid.NewGuid(), Tittel = tittel, Dato = new(aar - 1, 11, 20), Budsjettaar = aar,
                Kategori = kategori, Notat = "Publisert merknad <trygg>", Loep = "ux89-intern-loep", Kilde = "Opplastet: UX89-hemmelig-filnavn.pdf",
                Synlighet = [new() { GruppeKode = kode }] };
            db.Frister.Add(f);
            return f;
        }
        LeggTil("UX89 historisk regnskap", "FA", 2020, Kategori.Regnskap);
        LeggTil("UX89 tidlig frist", "FA", 2031, Kategori.Gulbok).Klokkeslett = new(4, 15);
        var tentativ = LeggTil("UX89 tentativ måned", "FIN-FAG", 2032);
        tentativ.Datopresisjon = Datopresisjon.Maaned; tentativ.Datokvalifikator = Datokvalifikator.Medio;
        var kilde = LeggTil("UX89 offentlig kilde", "FAG", 2033);
        kilde.DokumentId = dokument.Id;
        LeggTil("UX89 politisk frist", "POL", 2034).Klokkeslett = new(23, 45);
        LeggTil("UX89 egen gruppe", "SKOK", 2035, Kategori.Regnskap);
        SkjultId = LeggTil("UX89 skjult publisert", "HEMMELIG", 2098).Id;
        ForslagId = Guid.NewGuid();
        db.Forslag.Add(new Forslag { Id = ForslagId, Tittel = "UX89 skjult forslag", Notat = "UX89 hemmelig forslagsnotat",
            KildeEllerInnsender = "UX89 hemmelig innsender", Budsjettaar = 2099, Dato = new(2099, 1, 1), ForeslaattSynlighet = "[\"TOM\",\"FA\"]" });
        await db.SaveChangesAsync();
    }

    public async Task Gaa(string sti)
    {
        await Side.GotoAsync(sti);
        await Expect(Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
    }

    // Forsink bare original transport inn til nettleseren, i original rekkefølge.
    // Server, appens JavaScript og native dialog/cancel kjører uten endringer.
    public async Task<Blazorleveranse> KontrollerLeveranse()
    {
        var port = new Blazorleveranse();
        await Side.RouteWebSocketAsync("**/_blazor**", ws =>
        {
            var server = ws.ConnectToServer();
            ws.OnMessage(frame => {
                if (frame.Binary is not null) port.Sendt(frame.Binary);
                if (frame.Binary is not null) server.Send(frame.Binary); else server.Send(frame.Text!);
            });
            server.OnMessage(frame => port.Ko = port.Ko.ContinueWith(async forrige =>
            {
                await forrige;
                if (frame.Binary is not null) port.Svart(frame.Binary);
                if (port.Hold)
                {
                    port.Mottatt.TrySetResult();
                    await port.Fortsett.Task;
                }
                if (frame.Binary is not null) ws.Send(frame.Binary);
                else ws.Send(frame.Text!);
            }).Unwrap());
        });
        return port;
    }

    internal sealed class Blazorleveranse : IAsyncDisposable
    {
        public bool Hold;
        public Task Ko = Task.CompletedTask;
        public TaskCompletionSource Mottatt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _svar = new();
        private (string Metode, TaskCompletionSource Svar)? _nesteKall;
        public Task NesteKvittering(string metode)
        {
            var svar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _nesteKall = (metode, svar);
            return svar.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        public async Task Kvittert(string metode, Func<Task> handling)
        {
            var svar = NesteKvittering(metode);
            await handling();
            await svar;
        }
        public void Sendt(byte[] bytes)
        {
            if (_nesteKall is not { } neste || !System.Text.Encoding.UTF8.GetString(bytes).Contains(neste.Metode, StringComparison.Ordinal)) return;
            var id = KallId(bytes, "BeginInvokeDotNetFromJS");
            if (id is null) return;
            _svar[id] = neste.Svar; _nesteKall = null;
        }
        public void Svart(byte[] bytes)
        {
            var id = KallId(bytes, "JS.EndInvokeDotNet");
            if (id is not null && _svar.TryRemove(id, out var svar)) svar.TrySetResult();
        }
        // BlazorPack: hubmetodens argumentliste begynner med en kort call-id-streng.
        // Les bare kvitterings-ID; ikke endre eller logg nyttelasten. Dette er en
        // transportbarriere, slik at testen ikke frigir lagring før Escape er behandlet.
        private static string? KallId(byte[] bytes, string hubmetode)
        {
            var markoer = System.Text.Encoding.UTF8.GetBytes(hubmetode);
            var i = bytes.AsSpan().IndexOf(markoer);
            if (i < 0) return null;
            var start = i + markoer.Length + 1;
            if (start >= bytes.Length || (bytes[start] & 0xe0) != 0xa0) return null;
            var lengde = bytes[start] & 0x1f;
            return System.Text.Encoding.UTF8.GetString(bytes, start + 1, lengde);
        }
        public Task Vent() => Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Slipp() { Hold = false; Fortsett.TrySetResult(); }
        public async ValueTask DisposeAsync() { Slipp(); await Ko; }
    }

    public ILocator Valg => Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg gruppe", Exact = true });
    public ILocator Resultat => Side.GetByRole(AriaRole.Region, new() { Name = "Gruppeinnsyn", Exact = true });
    public ILocator Rader => Resultat.Locator("[data-frist-id]");
    public async Task Velg(string kode)
    {
        await Valg.SelectOptionAsync(kode);
        if (kode.Length > 0)
        {
            await Expect(Resultat.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToContainTextAsync($"({kode})");
            await Expect(Resultat.GetByRole(AriaRole.Status).Filter(new() { HasText = "publiserte" })).ToBeVisibleAsync();
        }
    }

    public async Task Bilder(string navn)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "grupper-innsyn"); Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await Side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{navn}-{bredde}.png"), FullPage = true });
        }
        await Side.SetViewportSizeAsync(1440, 1000);
    }

    public async ValueTask DisposeAsync()
    {
        Kontroll.SlippAlle();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose(); _klient?.Dispose();
        await App.DisposeAsync();
    }

    internal sealed class Gruppeapplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Port? _kontroll;
        public Port Kontroll => _kontroll ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); builder.UseStaticWebAssets();
            var port = Kontroll;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFristlesing>();
                services.AddScoped<IFristlesing>(sp => new KontrollertLesing(new FristTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
                services.RemoveAll<IGruppetjeneste>();
                services.AddScoped<IGruppetjeneste>(sp => new KontrollerteGrupper(new Gruppetjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }

    internal sealed class Steg(bool feil)
    {
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Ferdig { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Feil => feil;
        public Task VentStart() => Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public async Task Slipp() { Fortsett.TrySetResult(); await Ferdig.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    internal sealed class Port
    {
        private readonly ConcurrentDictionary<string, Steg> _neste = new();
        private readonly ConcurrentBag<Steg> _alle = [];
        public int Opprettinger;
        public int Oppdateringer;
        public readonly ConcurrentQueue<(bool SerAlt, string[] Grupper, bool Historikk)> Lesinger = new();
        public Steg Neste(string operasjon, bool feil = false)
        {
            var steg = new Steg(feil); Assert.True(_neste.TryAdd(operasjon, steg)); _alle.Add(steg); return steg;
        }
        public async Task Vent(string operasjon)
        {
            if (!_neste.TryRemove(operasjon, out var steg)) return;
            steg.Startet.TrySetResult();
            try { await steg.Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(30)); if (steg.Feil) throw new InvalidOperationException("UX89-RÅ-DATABASEFEIL"); }
            finally { steg.Ferdig.TrySetResult(); }
        }
        public void SlippAlle() { foreach (var steg in _alle) steg.Fortsett.TrySetResult(); }
    }

    private sealed class KontrollertLesing(IFristlesing indre, Port port) : IFristlesing
    {
        public async Task<IReadOnlyList<FristDto>> HentSynligeAsync(ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default)
        {
            port.Lesinger.Enqueue((ctx.SerAlt, ctx.Grupper.ToArray(), filter.InkluderHistorikk));
            var frister = await indre.HentSynligeAsync(ctx, filter, ct);
            await port.Vent("liste:" + string.Join(',', ctx.Grupper));
            return frister;
        }
        public async Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid id, CancellationToken ct = default)
        {
            port.Lesinger.Enqueue((ctx.SerAlt, ctx.Grupper.ToArray(), true));
            var frist = await indre.HentEnAsync(ctx, id, ct);
            await port.Vent("detalj:" + string.Join(',', ctx.Grupper));
            return frist;
        }
        public Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(ISynlighetskontekst ctx, DateOnly fra, FristFilter filter, CancellationToken ct = default) => indre.HentLandingsutvalgAsync(ctx, fra, filter, ct);
        public Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(ISynlighetskontekst ctx, bool historikk, CancellationToken ct = default) => indre.HentTilgjengeligeBudsjettaarAsync(ctx, historikk, ct);
    }

    private sealed class KontrollerteGrupper(IGruppetjeneste indre, Port port) : IGruppetjeneste
    {
        public async Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default) { await port.Vent("aktive"); return await indre.HentAktiveAsync(ct); }
        public async Task<IReadOnlyList<GruppeadministrasjonDto>> HentAlleAsync(CancellationToken ct = default) { await port.Vent("grupper"); return await indre.HentAlleAsync(ct); }
        public async Task OpprettAsync(string kode, string navn, CancellationToken ct = default) { Interlocked.Increment(ref port.Opprettinger); await port.Vent("opprett"); await indre.OpprettAsync(kode, navn, ct); }
        public async Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default) { Interlocked.Increment(ref port.Oppdateringer); await port.Vent("oppdater"); await indre.OppdaterAsync(kode, navn, aktiv, ct); }
    }
}
