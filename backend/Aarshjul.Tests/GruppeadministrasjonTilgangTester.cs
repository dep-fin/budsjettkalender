using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Grupper;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class GruppeadministrasjonTilgangTester
{
    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData("Administrator")]
    public async Task Faktiske_opprettings_og_oppdateringskommandoer_krever_autentisert_administrator(string? rolle)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var grupper = scope.ServiceProvider.GetRequiredService<IGruppetjeneste>();
        var identitet = new Kretsidentitet(Principal(rolle));
        var kommando = new GruppeadministrasjonKommando(identitet,
            scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), grupper);
        var foer = await grupper.HentAlleAsync();
        if (rolle == "Administrator")
        {
            await kommando.OpprettAsync(" skok ", " Ny gruppe ");
            Assert.Equal(new GruppeadministrasjonDto("SKOK", "Ny gruppe", true, false),
                Assert.Single(await grupper.HentAlleAsync(), g => g.Kode == "SKOK"));
            await kommando.OppdaterAsync("SKOK", "Endret gruppe", false);
            Assert.Equal(new GruppeadministrasjonDto("SKOK", "Endret gruppe", false, false),
                Assert.Single(await grupper.HentAlleAsync(), g => g.Kode == "SKOK"));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.OpprettAsync("SKOK", "Uautorisert"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.OppdaterAsync("POL", "Uautorisert", false));
            Assert.Equal(foer, await grupper.HentAlleAsync());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    public async Task Aapen_kommandokontekst_gjenleser_identitet_foer_begge_skrivehandlinger(string? nyRolle)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var grupper = scope.ServiceProvider.GetRequiredService<IGruppetjeneste>();
        var identitet = new Kretsidentitet(Principal("Administrator"));
        var kommando = new GruppeadministrasjonKommando(identitet,
            scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), grupper);
        await kommando.OpprettAsync("SKOK", "Før rolleendring");
        var foer = await grupper.HentAlleAsync();
        identitet.Bruker = Principal(nyRolle);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.OpprettAsync("ANNEN", "Etter rolleendring"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.OppdaterAsync("SKOK", "Etter rolleendring", false));
        Assert.Equal(foer, await grupper.HentAlleAsync());
    }

    private static ClaimsPrincipal Principal(string? rolle) => rolle is null ? new(new ClaimsIdentity())
        : new(new ClaimsIdentity([new Claim(Brukerclaims.Rolle, rolle), new Claim(Brukerclaims.Gruppe, "FA")], "Test"));

    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
}
