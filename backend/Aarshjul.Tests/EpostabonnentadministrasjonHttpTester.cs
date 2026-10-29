using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Aarshjul.Application.Epost;
using Aarshjul.Application.Brukere;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class EpostabonnentadministrasjonHttpTester
{
    [Fact]
    public async Task Lagringsfeil_gir_no_store_JSON_uten_innhold_eller_falsk_stans()
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        var fabrikk = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var abonnement = app.Services.GetRequiredService<IEpostabonnement>();
        var foer = await abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        await using (var db = fabrikk.CreateDbContext()) await db.Database.ExecuteSqlRawAsync("DROP TABLE Epostadministratorstanser");
        using var lesing = await klient.GetAsync("/api/admin/epostabonnenter");
        using var stans = await klient.PostAsJsonAsync("/api/admin/epostabonnenter/stans", new { brukerId = "a", foer.Versjon, bekreftet = true });
        foreach (var svar in new[] { lesing, stans })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
            Assert.Equal(new[] { "feil" }, (await svar.Content.ReadFromJsonAsync<JsonObject>())!.Select(x => x.Key));
            var tekst = await svar.Content.ReadAsStringAsync(); Assert.DoesNotContain("egen@fin.dep.no", tekst); Assert.DoesNotContain("Epostadministratorstanser", tekst);
            Assert.False(svar.Headers.Contains("Access-Control-Allow-Origin"));
        }
        Assert.Equal(foer, await abonnement.HentAsync("a"));
    }
    [Theory]
    [InlineData(null, "", 401)]
    [InlineData("Leser", "", 403)]
    [InlineData("Bidragsyter", "FA", 403)]
    [InlineData("Leser", "FA", 403)]
    public async Task Hele_avvisningssvaret_beskytter_andres_data_og_har_no_store(string? rolle, string grupper, int kode)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        var fabrikk = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await new EpostabonnementTjeneste(fabrikk, TimeProvider.System, app.Services.GetRequiredService<KontrollertBekreftelsespost>())
            .MeldPaaAsync("skjult-identitet", "skjult@fin.dep.no", true);
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        foreach (var uri in new[] { "/api/admin/epostabonnenter", "/api/admin/epostabonnenter/stans", "/admin/innstillinger/epostvarsling" })
        {
            using var svar = uri.EndsWith("/stans", StringComparison.Ordinal)
                ? await klient.PostAsJsonAsync(uri, new { brukerId = "skjult-identitet", versjon = Guid.NewGuid(), bekreftet = true })
                : await klient.GetAsync(uri);
            Assert.Equal(kode, (int)svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
            var tekst = await svar.Content.ReadAsStringAsync();
            Assert.DoesNotContain("skjult-identitet", tekst); Assert.DoesNotContain("skjult@fin.dep.no", tekst);
            Assert.DoesNotContain("bekreftelsesHash", tekst, StringComparison.OrdinalIgnoreCase);
            Assert.False(svar.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task HTTP_binder_aktoeren_til_innloggingen_og_krever_gjeldende_versjon_og_bekreftelse()
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator"); klient.DefaultRequestHeaders.Add("X-Test-Bruker", "ekte-admin");
        var fabrikk = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await new EpostabonnementTjeneste(fabrikk, TimeProvider.System, app.Services.GetRequiredService<KontrollertBekreftelsespost>())
            .MeldPaaAsync("a", "egen@fin.dep.no", true);
        var a = Assert.Single((await klient.GetFromJsonAsync<EpostabonnentDto[]>("/api/admin/epostabonnenter"))!);
        foreach (var bekreftet in new[] { false, true })
        {
            using var svar = await klient.PostAsJsonAsync("/api/admin/epostabonnenter/stans", new { brukerId = "a", a.Versjon, bekreftet, aktoerId = "spoofet", adresse = "annen@jd.dep.no" });
            Assert.Equal(bekreftet ? HttpStatusCode.OK : HttpStatusCode.BadRequest, svar.StatusCode);
            Assert.True(svar.Headers.CacheControl?.NoStore);
        }
        using var gammel = await klient.PostAsJsonAsync("/api/admin/epostabonnenter/stans", new { brukerId = "a", a.Versjon, bekreftet = true });
        Assert.Equal(HttpStatusCode.Conflict, gammel.StatusCode); Assert.True(gammel.Headers.CacheControl?.NoStore);
        using var db = fabrikk.CreateDbContext(); var spor = Assert.Single(await db.Epostadministratorstanser.ToListAsync());
        Assert.Equal("ekte-admin", spor.AktoerId); Assert.Empty(await db.Brukere.ToListAsync());
        var tekst = await klient.GetStringAsync("/api/admin/epostabonnenter");
        Assert.DoesNotContain("Hash", tekst, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("token", tekst, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spoofet", tekst); Assert.DoesNotContain("annen@jd.dep.no", tekst);
    }

    [Theory]
    [InlineData("{}", "application/json", 400)]
    [InlineData("null", "application/json", 400)]
    [InlineData("{", "application/json", 400)]
    [InlineData("{\"brukerId\":\"ukjent\",\"bekreftet\":true}", "application/json", 400)]
    [InlineData("{\"brukerId\":12}", "application/json", 400)]
    [InlineData("brukerId=a&bekreftet=true", "application/x-www-form-urlencoded", 415)]
    public async Task Ugyldig_stans_returnerer_trygg_JSON_feil_uten_CORS_eller_cache(string kropp, string type, int kode)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        using var svar = await klient.PostAsync("/api/admin/epostabonnenter/stans", new StringContent(kropp, System.Text.Encoding.UTF8, type));
        Assert.Equal(kode, (int)svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
        Assert.Equal(new[] { "feil" }, (await svar.Content.ReadFromJsonAsync<JsonObject>())!.Select(x => x.Key));
        Assert.False(svar.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData(null)]
    public async Task Kretsen_autoriserer_lesing_og_stans_paa_nytt_etter_nedgradering(string? rolle)
    {
        using var app = new TestApplikasjon(); using var scope = app.Services.CreateScope();
        var tjeneste = scope.ServiceProvider.GetRequiredService<IEpostabonnentadministrasjon>();
        var tilstand = new Kretsidentitet(Principal("Administrator"));
        var kommando = new EpostabonnentadministrasjonKommando(tilstand, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), tjeneste);
        Assert.Empty(await kommando.HentAsync());
        tilstand.Bruker = Principal(rolle);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.StansAsync("a", Guid.NewGuid(), true));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Kretsens_stans_bruker_stabil_claimprioritet(int foerste)
    {
        using var app = new TestApplikasjon(); using var scope = app.Services.CreateScope();
        var fabrikk = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var typer = new[] { "http://schemas.microsoft.com/identity/claims/objectidentifier", "oid", ClaimTypes.NameIdentifier, "sub" };
        var claims = typer.Select((type, i) => new Claim(type, "id-" + i)).Skip(foerste).Append(new Claim(Brukerclaims.Rolle, "Administrator"));
        var tilstand = new Kretsidentitet(new(new ClaimsIdentity(claims, "Test")));
        var kommando = new EpostabonnentadministrasjonKommando(tilstand, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), scope.ServiceProvider.GetRequiredService<IEpostabonnentadministrasjon>());
        await scope.ServiceProvider.GetRequiredService<IEpostabonnement>().MeldPaaAsync("a", "egen@fin.dep.no", true);
        var a = Assert.Single(await kommando.HentAsync()); await kommando.StansAsync("a", a.Versjon, true);
        Assert.Equal("id-" + foerste, Assert.Single(await kommando.HentAsync()).SistStansetAv);
        await using var db = fabrikk.CreateDbContext(); Assert.Empty(await db.Brukere.ToListAsync());
    }

    [Fact]
    public async Task Administratorclaim_uten_autentisering_eller_stabil_identitet_avvises_i_kretsen()
    {
        using var app = new TestApplikasjon(); using var scope = app.Services.CreateScope();
        foreach (var identity in new[] { new ClaimsIdentity([new(Brukerclaims.Rolle, "Administrator")]), new ClaimsIdentity([new(Brukerclaims.Rolle, "Administrator")], "Test") })
        {
            var kommando = new EpostabonnentadministrasjonKommando(new Kretsidentitet(new(identity)), scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), scope.ServiceProvider.GetRequiredService<IEpostabonnentadministrasjon>());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.StansAsync("a", Guid.NewGuid(), true));
        }
    }

    private static ClaimsPrincipal Principal(string? rolle) => rolle is null ? new(new ClaimsIdentity())
        : new(new ClaimsIdentity([new(Brukerclaims.Rolle, rolle), new(ClaimTypes.NameIdentifier, "admin"), new("gruppe", "FA")], "Test"));
    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
    [Fact]
    public async Task Administrator_faar_tom_abonnentoversikt_uten_automatisk_paamelding()
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        using var svar = await klient.GetAsync("/api/admin/epostabonnenter");
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.True(svar.Headers.CacheControl?.NoStore);
        Assert.Empty((await svar.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonArray>())!);
    }
}
