using System.Net;
using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Faser;
using Aarshjul.Domain;
using Aarshjul.Testkontrakter;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class FasemalTilgangTester
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FA")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    [InlineData("Administrator", "")]
    public async Task Faktisk_serverkommando_tillater_bare_autentisert_administrator(string? rolle, string gruppe)
    {
        using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var mal = scope.ServiceProvider.GetRequiredService<IFasemal>();
        var foer = await mal.HentAsync();
        var identitet = new Kretsidentitet(Principal(rolle, gruppe));
        // Dette er samme offentlige serverinngang som Blazor-editorens lagringshandling.
        var kommando = new FasemalKommando(identitet,
            scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), mal);
        if (rolle == "Administrator")
        {
            var lagret = await kommando.OppdaterAsync(foer.Versjon, FasemalKontrakt.EndredeFaser(), 17);
            Assert.NotEqual(foer.Versjon, lagret.Versjon);
            FasemalKontrakt.Like(lagret, await mal.HentAsync());
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                kommando.OppdaterAsync(foer.Versjon, FasemalKontrakt.EndredeFaser(), 17));
            FasemalKontrakt.Like(foer, await mal.HentAsync());
        }
    }

    [Fact]
    public async Task Lagring_leser_gjeldende_kretsidentitet_ved_hvert_kall()
    {
        using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var mal = scope.ServiceProvider.GetRequiredService<IFasemal>();
        var foer = await mal.HentAsync();
        var identitet = new Kretsidentitet(Principal("Administrator", ""));
        var kommando = new FasemalKommando(identitet,
            scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), mal);
        var lagret = await kommando.OppdaterAsync(foer.Versjon, FasemalKontrakt.EndredeFaser(), 17);
        identitet.Bruker = Principal("Leser", "FA");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.OppdaterAsync(lagret.Versjon, foer.Faser, 15));
        FasemalKontrakt.Like(lagret, await mal.HentAsync());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Leser", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", HttpStatusCode.OK)]
    public async Task Editorens_http_inngang_er_administratorbeskyttet(string? rolle, HttpStatusCode forventet)
    {
        using var app = new TestApplikasjon();
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", "FA");
        var svar = await klient.GetAsync("/admin/innstillinger/faseinndeling");
        Assert.Equal(forventet, svar.StatusCode);
        if (forventet != HttpStatusCode.OK)
            Assert.DoesNotContain("Fase 1 navn", await svar.Content.ReadAsStringAsync());
    }

    private static ClaimsPrincipal Principal(string? rolle, string gruppe)
        => rolle is null ? new(new ClaimsIdentity()) : new(new ClaimsIdentity(
            [new Claim(Brukerclaims.Rolle, rolle), new Claim(Brukerclaims.Gruppe, gruppe)], "Test"));

    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
}
