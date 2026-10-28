using Aarshjul.Application.Kalender;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Kalender;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AbonnementTilstanderTester
{
    [Fact]
    public Task Lesefeil_kan_proeves_igjen_og_ventende_oppretting_gir_bare_en_lenke() => Trygt(async (app, tjeneste, side) =>
    {
        await Klar(side, "/admin/eksport");
        app.Port.FeilNeste("les");
        await DelKalender(side);
        await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke hente abonnementene");
        await Expect(side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(Gruppe(side)).ToBeDisabledAsync();
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Ingen lenker ennå", Exact = true })).ToHaveCountAsync(0);
        await AbonnementTester.TokenfrieBilder(side, "lesefeil");
        await side.GetByRole(AriaRole.Button, new() { Name = "Last på nytt", Exact = true }).ClickAsync();
        await Expect(Gruppe(side)).ToBeEnabledAsync();
        await Gruppe(side).SelectOptionAsync(new SelectOptionValue { Label = "Alt (fullt innsyn – FIN-internt)" });
        app.Port.VentNeste("opprett");
        await side.GetByRole(AriaRole.Button, new() { Name = "Opprett lenke", Exact = true }).ClickAsync();
        var dialog = side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync("Alt (fullt innsyn – FIN-internt)");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, opprett lenke", Exact = true }).EvaluateAsync("el => { el.click(); el.click(); }");
        try
        {
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Lagrer …", Exact = true })).ToBeDisabledAsync();
            await Expect(Gruppe(side)).ToBeDisabledAsync();
            await side.Keyboard.PressAsync("Escape");
            await Expect(dialog).ToBeVisibleAsync();
            Assert.True((await tjeneste.HentAlleAsync()).Count == 0, "Opprettingen ble fullført før lagring.");
            app.Port.Fortsett.TrySetResult();
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("er opprettet");
            await Expect(side.GetByRole(AriaRole.Status)).ToBeFocusedAsync();
            var lagrede = await tjeneste.HentAlleAsync();
            Assert.True(lagrede.Count == 1 && lagrede[0].ErAlt && lagrede[0].Aktiv, "Opprettingen ga ikke ett aktivt Alt-abonnement.");
            await Expect(Gruppe(side).Locator("option:checked")).ToHaveTextAsync("Alt (fullt innsyn – FIN-internt)");
        }
        finally { app.Port.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData("opprett")]
    [InlineData("av")]
    [InlineData("paa")]
    [InlineData("slett")]
    public Task Lagringsfeil_beholder_valg_og_lagret_status_uten_aa_vise_unntaksinnhold(string handling) => Trygt(async (app, tjeneste, side) =>
    {
        KalenderabonnementDto? foer = null;
        if (handling != "opprett")
        {
            foer = await tjeneste.OpprettAsync("FA", "syntetisk administrator");
            app.Port.Hemmelighet = foer.Token;
            if (handling == "paa") await tjeneste.SettAktivAsync(foer.Id, false);
        }
        await Klar(side, "/admin/abonnement");
        await Gruppe(side).SelectOptionAsync("POL");
        app.Port.FeilNeste(handling);
        await StartHandling(side, handling);
        var dialog = side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = JaTekst(handling), Exact = true }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        var feil = side.GetByRole(AriaRole.Alert);
        await Expect(feil).ToContainTextAsync("Kunne ikke bekrefte lagringen");
        await Expect(feil).ToBeFocusedAsync();
        Assert.True(!(await side.Locator("body").InnerTextAsync()).Contains("syntetisk hemmelig feil"), "Rått unntaksinnhold ble vist.");
        if (foer is not null)
            Assert.True(!(await feil.InnerTextAsync()).Contains(foer.Token), "Hemmelig informasjon ble vist i feilmeldingen.");
        await Expect(Gruppe(side)).ToHaveValueAsync("POL");
        var etterFeil = await tjeneste.HentAlleAsync();
        Assert.True(handling == "opprett" ? etterFeil.Count == 0 : etterFeil.Count == 1 && etterFeil[0].Aktiv == (handling != "paa"), "Lagringsfeilen endret abonnementet.");
        await Expect(side.GetByRole(AriaRole.Status)).ToHaveCountAsync(0);
        if (handling == "opprett") await AbonnementTester.TokenfrieBilder(side, "lagringsfeil");
        await StartHandling(side, handling);
        await side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = JaTekst(handling), Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Dialog)).Not.ToBeVisibleAsync();
        await Expect(side.GetByRole(AriaRole.Status)).ToBeFocusedAsync();
        var lagret = await tjeneste.HentAlleAsync();
        Assert.True(handling == "slett" ? lagret.Count == 0 : lagret.Count == 1 && lagret[0].Aktiv == (handling != "av"), "Nytt forsøk lagret ikke forventet status.");
        if (handling == "opprett") Assert.True(lagret[0].GruppeKode == "POL", "Nytt forsøk endret valgt publikum.");
        if (foer is not null && lagret.Count == 1) Assert.True(lagret[0].Token == foer.Token, "Nytt forsøk endret den hemmelige lenken.");
    });

    [Fact]
    public Task Forlatt_lesing_kan_ikke_fylle_en_ny_abonnementsvisning() => Trygt(async (app, tjeneste, side) =>
    {
        await Klar(side, "/admin/eksport");
        app.Port.VentNeste("les");
        await Delingsnav(side).GetByRole(AriaRole.Link, new() { Name = "Del kalender", Exact = true }).ClickAsync();
        try
        {
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("Laster abonnementer");
            await Expect(Gruppe(side)).ToBeDisabledAsync();
            await Delingsnav(side).GetByRole(AriaRole.Link, new() { Name = "Eksporter", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Del kalender (abonnement)", Exact = true })).ToHaveCountAsync(0);
            app.Port.Fortsett.TrySetResult();
            await DelKalender(side);
            await Expect(Gruppe(side)).ToBeEnabledAsync();
            await Expect(Gruppe(side)).ToHaveValueAsync("");
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Ingen lenker ennå", Exact = true })).ToBeVisibleAsync();
            Assert.True((await tjeneste.HentAlleAsync()).Count == 0, "Lesing opprettet et abonnement.");
        }
        finally { app.Port.Fortsett.TrySetResult(); }
    });

    private static ILocator Gruppe(IPage side) => side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true });
    private static ILocator Delingsnav(IPage side) => side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true });
    private static string JaTekst(string handling) => handling switch { "opprett" => "Ja, opprett lenke", "av" => "Ja, skru av", "paa" => "Ja, skru på", _ => "Ja, slett permanent" };
    private static Task StartHandling(IPage side, string handling) => side.GetByRole(AriaRole.Button,
        new() { Name = handling switch { "opprett" => "Opprett lenke", "av" => "Skru av", "paa" => "Skru på", _ => "Slett" }, Exact = true }).ClickAsync();
    private static async Task Klar(IPage side, string sti)
    {
        await side.GotoAsync(sti);
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
    }
    private static async Task DelKalender(IPage side)
    {
        await Delingsnav(side).GetByRole(AriaRole.Link, new() { Name = "Del kalender", Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Del kalender (abonnement)", Exact = true })).ToBeVisibleAsync();
    }

    private static async Task Trygt(Func<Abonnementsapp, IKalenderabonnement, IPage, Task> scenario)
    {
        try
        {
            await using var app = new Abonnementsapp();
            using var klient = app.CreateClient();
            using var scope = app.Services.CreateScope();
            // Real persisted operations through the existing Application seam.
            var tjeneste = new KalenderabonnementTjeneste(scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
            await using var context = await browser.NewContextAsync(new()
            {
                BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
            });
            var side = await context.NewPageAsync();
            side.SetDefaultTimeout(10_000);
            await scenario(app, tjeneste, side);
        }
        catch (Exception)
        {
            // Never include the original exception, DOM, URL or inner exception in TRX.
            throw new Xunit.Sdk.XunitException("Abonnementstilstanden feilet. Hemmelige lenker er skjult.");
        }
    }

    private sealed class Abonnementsapp() : TestApplikasjon(brukKestrel: true)
    {
        private Abonnementsport? _port;
        public Abonnementsport Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            builder.ConfigureLogging(logg => logg.ClearProviders());
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKalenderabonnement>();
                services.AddScoped<IKalenderabonnement>(sp => new StyrtAbonnement(
                    new KalenderabonnementTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), TimeProvider.System), port));
            });
        }
    }

    private sealed class Abonnementsport
    {
        private string? _feil, _vent;
        public string Hemmelighet { get; set; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void FeilNeste(string handling) => _feil = handling;
        public void VentNeste(string handling) => _vent = handling;
        public async Task Foer(string handling, CancellationToken ct)
        {
            if (_feil == handling && Interlocked.Exchange(ref _feil, null) == handling)
                throw new InvalidOperationException("syntetisk hemmelig feil " + Hemmelighet);
            if (_vent != handling || Interlocked.Exchange(ref _vent, null) != handling) return;
            Startet.TrySetResult();
            await Fortsett.Task.WaitAsync(ct);
        }
    }

    private sealed class StyrtAbonnement(IKalenderabonnement indre, Abonnementsport port) : IKalenderabonnement
    {
        public async Task<IReadOnlyList<KalenderabonnementDto>> HentAlleAsync(CancellationToken ct = default)
        { await port.Foer("les", ct); return await indre.HentAlleAsync(ct); }
        public async Task<KalenderabonnementDto> OpprettAsync(string? gruppeKode, string opprettetAv, CancellationToken ct = default)
        { await port.Foer("opprett", ct); return await indre.OpprettAsync(gruppeKode, opprettetAv, ct); }
        public async Task SettAktivAsync(Guid id, bool aktiv, CancellationToken ct = default)
        { await port.Foer(aktiv ? "paa" : "av", ct); await indre.SettAktivAsync(id, aktiv, ct); }
        public async Task SlettAsync(Guid id, CancellationToken ct = default)
        { await port.Foer("slett", ct); await indre.SlettAsync(id, ct); }
        public Task<Feedutvalg?> HentAktivtUtvalgAsync(string token, CancellationToken ct = default) => indre.HentAktivtUtvalgAsync(token, ct);
    }
}
