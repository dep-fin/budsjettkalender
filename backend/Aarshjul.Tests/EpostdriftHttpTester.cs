using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Aarshjul.Application.Epost;
using Aarshjul.Application.Brukere;
using Aarshjul.Infrastructure;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class EpostdriftHttpTester
{
    [Theory]
    [InlineData(null, "", 401)] [InlineData("Leser", "", 403)]
    [InlineData("Leser", "FA", 403)] [InlineData("Bidragsyter", "FA", 403)]
    public async Task EV11_driftsinnganger_avviser_uten_administrator_og_beskytter_hele_svaret(string? rolle, string grupper, int kode)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        foreach (var uri in new[] { "/api/admin/epostdrift", "/api/admin/epostdrift/stans", "/admin/innstillinger/epostdrift" })
        {
            using var svar = uri.EndsWith("/stans", StringComparison.Ordinal)
                ? await klient.PostAsJsonAsync(uri, new { stanset = true, versjon = Guid.NewGuid(), bekreftet = true }) : await klient.GetAsync(uri);
            Assert.Equal(kode, (int)svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
            Assert.False(svar.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.DoesNotContain("sisteForsoek", await svar.Content.ReadAsStringAsync());
        }
        Assert.False((await app.Services.GetRequiredService<IEpostdrift>().HentAsync()).Stanset);
    }

    [Fact]
    public async Task EV11_admin_kan_stanse_og_gjenoppta_med_serverbundet_aktoer_og_versjonsvern()
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        var foer = await klient.GetFromJsonAsync<EpostdriftDto>("/api/admin/epostdrift");
        using var stans = await klient.PostAsJsonAsync("/api/admin/epostdrift/stans", new { stanset = true, foer!.Versjon, bekreftet = true, aktoerId = "falsk" });
        Assert.Equal(HttpStatusCode.OK, stans.StatusCode); Assert.True(stans.Headers.CacheControl?.NoStore);
        var etter = await klient.GetFromJsonAsync<EpostdriftDto>("/api/admin/epostdrift");
        Assert.True(etter!.Stanset); Assert.NotEqual("falsk", etter.EndretAv); Assert.NotNull(etter.EndretAv);
        using var konflikt = await klient.PostAsJsonAsync("/api/admin/epostdrift/stans", new { stanset = false, foer.Versjon, bekreftet = true });
        Assert.Equal(HttpStatusCode.Conflict, konflikt.StatusCode);
        using var gjenoppta = await klient.PostAsJsonAsync("/api/admin/epostdrift/stans", new { stanset = false, etter.Versjon, bekreftet = true });
        Assert.Equal(HttpStatusCode.OK, gjenoppta.StatusCode);
        Assert.False((await klient.GetFromJsonAsync<EpostdriftDto>("/api/admin/epostdrift"))!.Stanset);
    }

    [Theory]
    [InlineData("{}", "application/json", 400)] [InlineData("{", "application/json", 400)]
    [InlineData("null", "application/json", 400)] [InlineData("stanset=true", "application/x-www-form-urlencoded", 415)]
    public async Task EV11_mutasjon_krever_JSON_og_bekreftelse_med_trygg_feil(string kropp, string type, int kode)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        using var svar = await klient.PostAsync("/api/admin/epostdrift/stans", new StringContent(kropp, System.Text.Encoding.UTF8, type));
        Assert.Equal(kode, (int)svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
        Assert.Equal(new[] { "feil" }, (await svar.Content.ReadFromJsonAsync<JsonObject>())!.Select(x => x.Key));
        Assert.False(svar.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData(null)] [InlineData("Leser")] [InlineData("Bidragsyter")]
    public async Task EV11_krets_lesing_og_mutasjon_revurderer_policy_etter_nedgradering(string? rolle)
    {
        using var app = new TestApplikasjon(); using var scope = app.Services.CreateScope();
        var tilstand = new Krets(Principal("Administrator"));
        var kommando = new EpostdriftKommando(tilstand, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), scope.ServiceProvider.GetRequiredService<IEpostdrift>());
        var foer = await kommando.HentAsync(); tilstand.Bruker = Principal(rolle);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.SettStansAsync(true, foer.Versjon, true));
    }

    [Fact]
    public async Task EV11_lagringsfeil_er_generisk_JSON_uten_databasespor_eller_falsk_stans()
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        var drift = app.Services.GetRequiredService<IEpostdrift>(); var foer = await drift.HentAsync();
        await using (var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER TR_EV11_fail BEFORE UPDATE ON Epostdrift BEGIN SELECT RAISE(ABORT, 'Ikke eksponer databasespor'); END");
        using var svar = await klient.PostAsJsonAsync("/api/admin/epostdrift/stans", new { stanset = true, foer.Versjon, bekreftet = true });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("databasespor", await svar.Content.ReadAsStringAsync()); Assert.False((await drift.HentAsync()).Stanset);
    }

    private static ClaimsPrincipal Principal(string? rolle) => rolle is null ? new(new ClaimsIdentity())
        : new(new ClaimsIdentity([new(Brukerclaims.Rolle, rolle), new(ClaimTypes.NameIdentifier, "driftsadmin"), new("gruppe", "FA")], "Test"));
    private sealed class Krets(ClaimsPrincipal bruker) : AuthenticationStateProvider
    { public ClaimsPrincipal Bruker = bruker; public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker)); }
}
