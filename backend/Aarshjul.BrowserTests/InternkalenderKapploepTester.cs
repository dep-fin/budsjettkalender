using System.Collections.Concurrent;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Internkalender;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class InternkalenderKapploepTester
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Sen_lesing_eller_feil_fra_annen_oppgave_eller_regel_kan_ikke_overta_ny_editor(bool regel, bool feil) => Kjor(async (p, side, k, r) =>
    {
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var a = regel ? await r.OpprettAsync(Regel("UX16 original A")) : await k.HurtiglaggAsync(runde, "UX16 original A");
        var b = regel ? await r.OpprettAsync(Regel("UX16 original B")) : await k.HurtiglaggAsync(runde, "UX16 original B");
        var sti = regel ? "/internkalender/regler/" : "/internkalender/gjoeremaal/";
        p.LastId = a; p.LastRegel = regel;
        await Naviger(side, sti + a);
        await p.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await Naviger(side, sti + b);
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX16 original B");
            if (feil) p.Fortsett.TrySetException(new InvalidOperationException("UX16PRIVATE_OLD_READ_ERROR"));
            else p.Fortsett.TrySetResult();
            await p.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await side.GetByLabel("Notat", new() { Exact = true }).FillAsync("UX16 bare B");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX16 original B");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(regel ? "Gjøremålsregler" : "Regnskap 2036");
            if (regel)
            {
                Assert.Null((await r.HentForRedigeringAsync(a))!.Notat);
                Assert.Equal("UX16 bare B", (await r.HentForRedigeringAsync(b))!.Notat);
            }
            else
            {
                Assert.Null((await k.HentForRedigeringAsync(a))!.Notat);
                Assert.Equal("UX16 bare B", (await k.HentForRedigeringAsync(b))!.Notat);
            }
        }
        finally { p.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Koede_doble_lagringer_laas_felter_og_intern_navigasjon_til_riktig_lagret_identitet(bool regel) => Kjor(async (p, side, k, r) =>
    {
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var id = regel ? await r.OpprettAsync(Regel("UX16 før lagring")) : await k.HurtiglaggAsync(runde, "UX16 før lagring");
        await Naviger(side, (regel ? "/internkalender/regler/" : "/internkalender/gjoeremaal/") + id);
        var tittel = side.GetByLabel("Tittel", new() { Exact = true });
        await Expect(tittel).ToHaveValueAsync("UX16 før lagring");
        await tittel.FillAsync("UX16 etter én lagring");
        p.Lagre = true;
        try
        {
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await p.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(tittel).ToBeDisabledAsync();
            await Naviger(side, "/internkalender/mine");
            Assert.Contains(id.ToString(), side.Url);
            Assert.Equal("UX16 før lagring", regel ? (await r.HentForRedigeringAsync(id))!.Tittel : (await k.HentForRedigeringAsync(id))!.Tittel);
            p.Fortsett.TrySetResult();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(regel ? "Gjøremålsregler" : "Regnskap 2036");
            Assert.Equal("UX16 etter én lagring", regel ? (await r.HentForRedigeringAsync(id))!.Tittel : (await k.HentForRedigeringAsync(id))!.Tittel);
            Assert.Equal(1, p.Lagringer);
        }
        finally { p.Fortsett.TrySetResult(); }
    });

    private static async Task Kjor(Func<Port, IPage, IInternkalender, IGjoeremaalRegler, Task> scenario)
    {
        await using var app = new PortApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var fabrikk = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var k = new InternkalenderTjeneste(fabrikk, TimeProvider.System);
        var r = new GjoeremaalRegelTjeneste(fabrikk);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        side.SetDefaultTimeout(10_000);
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, e) => feil.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") feil.Enqueue(e.Text); };
        await side.GotoAsync("/internkalender");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        try { await scenario(app.Port, side, k, r); Assert.Empty(feil); }
        finally { app.Port.Fortsett.TrySetResult(); }
    }

    // Opprett bare en vanlig intern lenke for å utløse samme Blazor-navigasjon som rutelenker.
    private static Task Naviger(IPage side, string uri) => side.EvaluateAsync("uri => { const a = document.createElement('a'); a.href = uri; document.body.append(a); a.click(); a.remove(); }", uri);
    private static RegelInndata Regel(string tittel) => new() { Tittel = tittel, Rundetyper = [Rundetype.Regnskap] };
    private sealed class PortApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Port? _port;
        public Port Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder); builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInternkalender>(); services.RemoveAll<IGjoeremaalRegler>();
                services.AddScoped<IInternkalender>(sp => new ForsinketKalender(new InternkalenderTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
                services.AddScoped<IGjoeremaalRegler>(sp => new ForsinkedeRegler(new GjoeremaalRegelTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }
    private sealed class Port
    {
        public Guid? LastId;
        public bool LastRegel, Lagre;
        public int Lagringer;
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fullfoert { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task EtterLesing(Guid id, bool regel, CancellationToken ct)
        {
            if (id != LastId || regel != LastRegel) return;
            LastId = null; Startet.TrySetResult();
            try { await Fortsett.Task.WaitAsync(ct); }
            finally { Fullfoert.TrySetResult(); }
        }
        public async Task FoerLagring(CancellationToken ct)
        {
            if (!Lagre) return;
            Interlocked.Increment(ref Lagringer); Startet.TrySetResult();
            await Fortsett.Task.WaitAsync(ct);
        }
    }
    private sealed class ForsinkedeRegler(IGjoeremaalRegler indre, Port port) : IGjoeremaalRegler
    {
        public Task<IReadOnlyList<RegelDto>> HentAlleAsync(CancellationToken ct = default) => indre.HentAlleAsync(ct);
        public async Task<RegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default) { var resultat = await indre.HentForRedigeringAsync(id, ct); await port.EtterLesing(id, true, ct); return resultat; }
        public Task<Guid> OpprettAsync(RegelInndata data, CancellationToken ct = default) => indre.OpprettAsync(data, ct);
        public async Task OppdaterAsync(Guid id, RegelInndata data, CancellationToken ct = default) { await port.FoerLagring(ct); await indre.OppdaterAsync(id, data, ct); }
        public Task SlettAsync(Guid id, CancellationToken ct = default) => indre.SlettAsync(id, ct);
    }
    private sealed class ForsinketKalender(IInternkalender indre, Port port) : IInternkalender
    {
        public Task<IReadOnlyList<RundeDto>> HentRunderAsync(CancellationToken ct = default) => indre.HentRunderAsync(ct);
        public Task<RundeDto?> HentRundeAsync(Guid id, CancellationToken ct = default) => indre.HentRundeAsync(id, ct);
        public Task<Guid> OpprettRundeAsync(Rundetype type, int? aar, string? opprettetAv, CancellationToken ct = default) => indre.OpprettRundeAsync(type, aar, opprettetAv, ct);
        public Task<Guid> HentEllerOpprettOvrigAsync(string? opprettetAv, CancellationToken ct = default) => indre.HentEllerOpprettOvrigAsync(opprettetAv, ct);
        public Task<Guid> GenererRundeAsync(Rundetype type, int aar, string? opprettetAv, CancellationToken ct = default) => indre.GenererRundeAsync(type, aar, opprettetAv, ct);
        public Task SlettRundeAsync(Guid id, CancellationToken ct = default) => indre.SlettRundeAsync(id, ct);
        public Task<IReadOnlyList<GjoeremaalDto>> HentGjoeremaalAsync(Guid id, string? ansvarligBrukerId, CancellationToken ct = default) => indre.HentGjoeremaalAsync(id, ansvarligBrukerId, ct);
        public Task<Guid> HurtiglaggAsync(Guid id, string tittel, CancellationToken ct = default) => indre.HurtiglaggAsync(id, tittel, ct);
        public async Task<GjoeremaalInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default) { var resultat = await indre.HentForRedigeringAsync(id, ct); await port.EtterLesing(id, false, ct); return resultat; }
        public Task<Guid?> HentRundeIdForGjoeremaalAsync(Guid id, CancellationToken ct = default) => indre.HentRundeIdForGjoeremaalAsync(id, ct);
        public Task<Guid> OpprettGjoeremaalAsync(Guid id, GjoeremaalInndata data, CancellationToken ct = default) => indre.OpprettGjoeremaalAsync(id, data, ct);
        public async Task OppdaterGjoeremaalAsync(Guid id, GjoeremaalInndata data, CancellationToken ct = default) { await port.FoerLagring(ct); await indre.OppdaterGjoeremaalAsync(id, data, ct); }
        public Task SlettGjoeremaalAsync(Guid id, CancellationToken ct = default) => indre.SlettGjoeremaalAsync(id, ct);
        public Task FullfoerAsync(Guid id, string brukerId, string navn, CancellationToken ct = default) => indre.FullfoerAsync(id, brukerId, navn, ct);
        public Task GjenaapneAsync(Guid id, CancellationToken ct = default) => indre.GjenaapneAsync(id, ct);
        public Task<IReadOnlyList<MittGjoeremaalDto>> HentMineAsync(string id, CancellationToken ct = default) => indre.HentMineAsync(id, ct);
        public Task<IReadOnlyList<AnsvarligDto>> HentMuligeAnsvarligeAsync(CancellationToken ct = default) => indre.HentMuligeAnsvarligeAsync(ct);
        public Task<IReadOnlyList<SynkForslagDto>> ForberedSynkAsync(Guid id, CancellationToken ct = default) => indre.ForberedSynkAsync(id, ct);
        public Task SynkroniserAsync(Guid id, IReadOnlyList<SynkForslagDto> forslag, CancellationToken ct = default) => indre.SynkroniserAsync(id, forslag, ct);
    }
}
