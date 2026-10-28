using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Grupper;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Godkjenningsko;
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

public sealed class FristNavigasjonTester
{
    [Fact]
    public Task Sent_bevis_fra_A_kan_ikke_erstatte_B_eller_publiseres_paa_feil_maal() => Kjor(async (app, side, klient, ko, a, b) =>
    {
        var foer = await ko.HentKoAsync();
        var vent = app.Port.Forsink("bevis", 2031);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/juster/{b}?aar=2032");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await Expect(side.GetByRole(AriaRole.Table)).ToContainTextAsync("BEVIS Original B");
            vent.Fortsett.TrySetResult();
            await vent.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // En ny Blazor-hendelse og render bekrefter at det sene svaret er behandlet.
            await side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true })).ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Table)).ToContainTextAsync("BEVIS Original B");
            await Expect(side.GetByRole(AriaRole.Table)).Not.ToContainTextAsync("BEVIS Original A");
            await TaBilder(side, "b-etter-sent-a");
            await side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("Justert B");
            await side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync("Ultimo");
            await side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/ko\\?aar=2032&lagretAar=2032$"));
            var etter = await ko.HentKoAsync();
            Assert.Equal(JsonSerializer.Serialize(foer.Single(f => f.Id == a)), JsonSerializer.Serialize(Assert.Single(etter)));
            var publisert = Assert.Single((await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!, f => f.Budsjettaar == 2032);
            Assert.NotEqual(a, publisert.Id);
            Assert.NotEqual(b, publisert.Id);
            Assert.Equal("Justert B", publisert.Tittel);
            Assert.Equal(new DateOnly(2030, 11, 23), publisert.Dato);
            Assert.Equal(Datopresisjon.Maaned, publisert.Datopresisjon);
            Assert.Equal(Datokvalifikator.Ultimo, publisert.Datokvalifikator);
            Assert.Equal(new DateOnly(2030, 11, 30), publisert.Sorteringsdag);
            Assert.Equal(Kategori.Regnskap, publisert.Kategori);
            Assert.Equal("loep-b", publisert.Loep);
            Assert.Equal("notat-b", publisert.Notat);
            Assert.Equal("Opplastet: Original B.pdf", publisert.Kilde);
            Assert.Equal(foer.Single(f => f.Id == b).DokumentId, publisert.DokumentId);
            Assert.Equal("https://regjeringen.no/original-b", publisert.KildeUrl);
            Assert.Equal(new[] { "FA" }, publisert.Synlighet);
            Assert.Null(publisert.Klokkeslett);
            Assert.False(publisert.VenterEndring);
            Assert.Equal(FristStatus.Godkjent, publisert.Status);
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData("grupper", false)]
    [InlineData("grupper", true)]
    [InlineData("felt", false)]
    [InlineData("felt", true)]
    [InlineData("bevis", false)]
    [InlineData("bevis", true)]
    public Task Sene_felt_bevis_eller_feil_fra_A_endrer_ikke_ferdig_lastet_B(string steg, bool feil) => Kjor(async (app, side, klient, ko, a, b) =>
    {
        var foer = JsonSerializer.Serialize(await ko.HentKoAsync());
        var vent = app.Port.Forsink(steg, steg == "felt" ? a : steg == "grupper" ? 0 : 2031);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/juster/{b}?aar=2032");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            if (feil) vent.Fortsett.TrySetException(new InvalidOperationException("Kontrollert privat feil fra gammel rute"));
            else vent.Fortsett.TrySetResult();
            await vent.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true })).ToBeVisibleAsync();
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await Expect(side.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-23");
            await Expect(side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2032");
            await Expect(side.GetByRole(AriaRole.Table)).ToContainTextAsync("BEVIS Original B");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            Assert.Equal(foer, JsonSerializer.Serialize(await ko.HentKoAsync()));
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Gammel_fullfoering_aapner_ikke_lagring_mens_B_fortsatt_laster(bool feil) => Kjor(async (app, side, klient, ko, a, b) =>
    {
        var gammel = app.Port.Forsink("bevis", 2031);
        var ny = app.Port.Forsink("bevis", 2032);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await gammel.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/juster/{b}?aar=2032");
            await ny.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (feil) gammel.Fortsett.TrySetException(new InvalidOperationException("Foreldet lastefeil"));
            else gammel.Fortsett.TrySetResult();
            await gammel.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true })).ToHaveCountAsync(0);
            await Expect(side.Locator("main")).ToContainTextAsync("Laster …");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            if (!feil) await TaBilder(side, "b-venter-etter-sent-a");
            ny.Fortsett.TrySetResult();
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await Expect(side.GetByRole(AriaRole.Table)).ToContainTextAsync("BEVIS Original B");
            Assert.Equal(2, (await ko.HentKoAsync()).Count);
        }
        finally { gammel.Fortsett.TrySetResult(); ny.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Justering_til_ny_eller_redigering_bevarer_egen_identitet_og_uten_gamle_bevis(bool ny) => Kjor(async (app, side, klient, ko, a, b) =>
    {
        using var scope = app.Services.CreateScope();
        var skriving = scope.ServiceProvider.GetRequiredService<IFristskriving>();
        var id = await skriving.OpprettAsync(new() { Tittel = "Eksisterende C", Budsjettaar = 2033, Dato = new(2032, 12, 7), Synlighetskoder = ["FA"] });
        var foer = (await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!;
        var koFoer = JsonSerializer.Serialize(await ko.HentKoAsync());
        var vent = app.Port.Forsink("bevis", 2031);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, ny ? "/admin/frist/ny?aar=2033" : $"/admin/frist/{id}?aar=2033");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(ny ? "" : "Eksisterende C");
            vent.Fortsett.TrySetResult();
            await vent.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true })).ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
            await Expect(side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2033");
            await side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(ny ? "Ny D" : "Redigert C");
            await side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2032-12-07");
            await side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
            var etter = (await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!;
            Assert.Equal(koFoer, JsonSerializer.Serialize(await ko.HentKoAsync()));
            Assert.Equal(JsonSerializer.Serialize(foer!.Where(f => ny || f.Id != id)), JsonSerializer.Serialize(etter.Where(f => ny ? f.Tittel != "Ny D" : f.Id != id)));
            var lagret = Assert.Single(etter, f => f.Tittel == (ny ? "Ny D" : "Redigert C"));
            if (ny) Assert.DoesNotContain(lagret.Id, foer.Select(f => f.Id));
            else Assert.Equal(id, lagret.Id);
            Assert.Equal(new DateOnly(2032, 12, 7), lagret.Dato);
            Assert.Equal(2033, lagret.Budsjettaar);
            Assert.Equal(Datopresisjon.Maaned, lagret.Datopresisjon);
            Assert.Equal("manuell", lagret.Kilde);
            Assert.Null(lagret.DokumentId);
            Assert.Null(lagret.KildeUrl);
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Lastefeil_beholder_skjemaet_lukket_og_nytt_forsoek_henter_riktig_bevis() => Kjor(async (app, side, klient, ko, a, b) =>
    {
        var vent = app.Port.Forsink("bevis", 2031);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vent.Fortsett.TrySetException(new InvalidOperationException("PRIVAT-TEKNISK-DETALJ"));
            await Expect(side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke laste feltene og kildegrunnlaget");
            await Expect(side.Locator("main")).Not.ToContainTextAsync("PRIVAT-TEKNISK-DETALJ");
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true })).ToHaveCountAsync(0);
            await TaBilder(side, "lastefeil");
            await side.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original A");
            await Expect(side.GetByRole(AriaRole.Table)).ToContainTextAsync("BEVIS Original A");
            Assert.Equal(2, (await ko.HentKoAsync()).Count);
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Redigering_til_redigering_avviser_gamle_felt_og_lagrer_bare_det_nye_maalet(bool feil) => Kjor(async (app, side, klient, ko, a, b) =>
    {
        using var scope = app.Services.CreateScope();
        var skriving = scope.ServiceProvider.GetRequiredService<IFristskriving>();
        var c = await skriving.OpprettAsync(new() { Tittel = "Frist C", Dato = new(2032, 12, 7), Budsjettaar = 2033, Synlighetskoder = ["FA"] });
        var d = await skriving.OpprettAsync(new() { Tittel = "Frist D", Dato = new(2033, 1, 8), Budsjettaar = 2034, Synlighetskoder = ["FA"] });
        var foer = (await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!;
        var vent = app.Port.Forsink("frist", c);
        try
        {
            await Naviger(side, $"/admin/frist/{c}?aar=2033");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/{d}?aar=2034");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Frist D");
            if (feil) vent.Fortsett.TrySetException(new InvalidOperationException("Foreldet fristfeil"));
            else vent.Fortsett.TrySetResult();
            await vent.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("Endret D");
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
            var etter = (await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!;
            Assert.Equal(JsonSerializer.Serialize(foer.Where(f => f.Id != d)), JsonSerializer.Serialize(etter.Where(f => f.Id != d)));
            Assert.Equal(JsonSerializer.Serialize(foer.Single(f => f.Id == d) with { Tittel = "Endret D" }), JsonSerializer.Serialize(etter.Single(f => f.Id == d)));
            Assert.Equal(2, (await ko.HentKoAsync()).Count);
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Avbryt_og_sen_feil_kan_ikke_endres_eller_publisere_en_forlatt_justering() => Kjor(async (app, side, klient, ko, a, b) =>
    {
        var foer = JsonSerializer.Serialize(await ko.HentKoAsync());
        var vent = app.Port.Forsink("bevis", 2031);
        try
        {
            await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/juster/{b}?aar=2032&fra=generering");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original B");
            await side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/generer\\?aar=2032$"));
            vent.Fortsett.TrySetException(new InvalidOperationException("Gammel feil etter avbryt"));
            await vent.Fullfoert.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, "/admin/frist/ny?aar=2033");
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("");
            await Expect(side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
            await Expect(side.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
            Assert.Equal(foer, JsonSerializer.Serialize(await ko.HentKoAsync()));
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    [Fact]
    public Task Lagring_laas_er_knyttet_til_det_lastede_forslaget_og_hindrer_ny_rute_og_dobbelinnsending() => Kjor(async (app, side, klient, ko, a, b) =>
    {
        await Naviger(side, $"/admin/frist/juster/{a}?aar=2031");
        await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("Original A");
        await side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        var vent = app.Port.Forsink("lagre", a);
        try
        {
            await side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await vent.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Naviger(side, $"/admin/frist/juster/{b}?aar=2032");
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/admin/frist/juster/{a}\\?aar=2031$"));
            await Expect(side.GetByLabel("Tittel", new() { Exact = true })).ToBeDisabledAsync();
            await side.Locator("form.skjema").EvaluateAsync("el => el.requestSubmit()");
            Assert.Equal(2, (await ko.HentKoAsync()).Count);
            vent.Fortsett.TrySetResult();
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/ko\\?aar=2031&lagretAar=2031$"));
            Assert.Equal(b, Assert.Single(await ko.HentKoAsync()).Id);
            var data = (await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!;
            Assert.Single(data, f => f.Tittel == "Original A");
            Assert.DoesNotContain(data, f => f.Tittel == "Original B");
        }
        finally { vent.Fortsett.TrySetResult(); }
    });

    private static async Task Kjor(Func<ForsinketFristApplikasjon, IPage, HttpClient, IGodkjenningsko, Guid, Guid, Task> scenario)
    {
        await using var app = new ForsinketFristApplikasjon();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        using var scope = app.Services.CreateScope();
        var ko = scope.ServiceProvider.GetRequiredService<IGodkjenningsko>();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
        {
            foreach (var (id, navn, aar, dag, kategori) in new[] { (a, "A", 2031, 20, Kategori.Budsjett), (b, "B", 2032, 23, Kategori.Regnskap) })
            {
                var dokument = new BehandletDokument { Id = Guid.NewGuid(), Kilde = "opplasting", DokumentNokkel = id.ToString(), Url = $"https://regjeringen.no/original-{navn.ToLowerInvariant()}" };
                db.BehandledeDokumenter.Add(dokument);
                db.Forslag.Add(new Forslag
                {
                    Id = id, Tittel = "Original " + navn, Budsjettaar = aar, Dato = new(2030, 11, dag), Kategori = kategori,
                    Opphav = Opphav.Robot, KildeEllerInnsender = $"Opplastet: Original {navn}.pdf", DokumentId = dokument.Id,
                    Loep = "loep-" + navn.ToLowerInvariant(), Notat = "notat-" + navn.ToLowerInvariant(),
                    UttrekksBevis = [new() { Id = Guid.NewGuid(), Felt = "dato", TolketVerdi = $"2030-11-{dag}", Kildeutdrag = "BEVIS Original " + navn, Konfidens = 0.65 }]
                });
            }
            await db.SaveChangesAsync();
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, melding) => feil.Enqueue(melding);
        side.Console += (_, melding) => { if (melding.Type == "error") feil.Enqueue(melding.Text); };
        await side.GotoAsync("/admin/frist/ny?aar=2031");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await scenario(app, side, klient, ko, a, b);
        Assert.Empty(feil);
    }

    private static Task Naviger(IPage side, string uri) => side.EvaluateAsync("uri => { const a = document.createElement('a'); a.href = uri; document.body.append(a); a.click(); a.remove(); }", uri);

    private static async Task TaBilder(IPage side, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "frist-navigasjon");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }

    private sealed class ForsinketFristApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Fristport? _port;
        public Fristport Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGodkjenningsko>();
                services.AddScoped<IGodkjenningsko>(sp => new ForsinketKo(new GodkjenningskoTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
                services.RemoveAll<IGruppetjeneste>();
                services.AddScoped<IGruppetjeneste>(sp => new ForsinketGrupper(new Gruppetjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
                services.RemoveAll<IFristskriving>();
                services.AddScoped<IFristskriving>(sp => new ForsinketSkriving(new FristskrivingTjeneste(sp.GetRequiredService<IDbContextFactory<AppDbContext>>()), port));
            });
        }
    }

    private sealed class Ventepunkt
    {
        public TaskCompletionSource Startet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fullfoert { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Fristport
    {
        private readonly ConcurrentDictionary<(string, string), Ventepunkt> _venter = new();
        public Ventepunkt Forsink(string steg, object id) => _venter[(steg, id.ToString()!)] = new();
        public async Task Vent(string steg, object id)
        {
            if (!_venter.TryRemove((steg, id.ToString()!), out var punkt)) return;
            punkt.Startet.TrySetResult();
            // Fullfør også for forlatte ruter: bare kansellering er ikke nok som vern.
            try { await punkt.Fortsett.Task; }
            finally { punkt.Fullfoert.TrySetResult(); }
        }
    }

    private sealed class ForsinketKo(IGodkjenningsko indre, Fristport port) : IGodkjenningsko
    {
        public async Task<IReadOnlyList<Koelement>> HentKoAsync(Kofilter? filter = null, CancellationToken ct = default)
        {
            var data = await indre.HentKoAsync(filter, ct);
            await port.Vent("bevis", filter?.Budsjettaar ?? 0);
            return data;
        }
        public async Task<ForslagForJustering?> HentForslagForJusteringAsync(Guid id, CancellationToken ct = default)
        {
            var data = await indre.HentForslagForJusteringAsync(id, ct);
            await port.Vent("felt", id);
            return data;
        }
        public Task<Guid> GodkjennAsync(GodkjennInndata data, CancellationToken ct = default) => indre.GodkjennAsync(data, ct);
        public async Task<Guid> JusterOgGodkjennAsync(Guid id, FristInndata data, CancellationToken ct = default)
        {
            await port.Vent("lagre", id);
            return await indre.JusterOgGodkjennAsync(id, data, ct);
        }
        public Task AvvisAsync(Guid id, string? grunn = null, CancellationToken ct = default) => indre.AvvisAsync(id, grunn, ct);
    }

    private sealed class ForsinketGrupper(IGruppetjeneste indre, Fristport port) : IGruppetjeneste
    {
        public async Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default)
        {
            var data = await indre.HentAktiveAsync(ct);
            await port.Vent("grupper", 0);
            return data;
        }
        public Task<IReadOnlyList<GruppeadministrasjonDto>> HentAlleAsync(CancellationToken ct = default) => indre.HentAlleAsync(ct);
        public Task OpprettAsync(string kode, string navn, CancellationToken ct = default) => indre.OpprettAsync(kode, navn, ct);
        public Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default) => indre.OppdaterAsync(kode, navn, aktiv, ct);
    }

    private sealed class ForsinketSkriving(IFristskriving indre, Fristport port) : IFristskriving
    {
        public async Task<FristInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default)
        {
            var data = await indre.HentForRedigeringAsync(id, ct);
            await port.Vent("frist", id);
            return data;
        }
        public Task<Guid> OpprettAsync(FristInndata data, CancellationToken ct = default) => indre.OpprettAsync(data, ct);
        public Task OppdaterAsync(Guid id, FristInndata data, CancellationToken ct = default) => indre.OppdaterAsync(id, data, ct);
    }
}
