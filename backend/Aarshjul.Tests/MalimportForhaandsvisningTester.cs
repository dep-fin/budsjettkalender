using System.Net;
using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Datautveksling;
using Microsoft.AspNetCore.Mvc.Testing;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class MalimportForhaandsvisningTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IMalDatautveksling Tjeneste => new MalDatautveksling(_t.Factory, TimeProvider.System);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sideeffektfri_preview_og_import_er_enige_om_tom_eller_blandet_hel_erstatning(bool blandet)
    {
        var tjeneste = Tjeneste;
        var gammel = Regel("gammel");
        await tjeneste.ImporterAsync(new() { Regler = [gammel] });
        var gyldig = Regel("  eldre  ") with { Tittel = "  ", Regelparametre = " {\"maaned\":2,\"dag\":29,\"aar_forskyvning\":-1} " };
        var ugyldig = Regel("utelatt") with { Regelparametre = "{}" };
        var fil = new MalDatabase { Regler = blandet ? [gyldig, ugyldig] : [] };

        var preview = await tjeneste.ForhaandsvisAsync(fil);

        Assert.Equal(1, preview.EksisterendeAntall);
        Assert.Equal(new[] { gammel }, (await tjeneste.EksporterAsync()).Regler);
        Assert.Equal(blandet ? 1 : 0, preview.GyldigeRegler.Count);
        Assert.Equal(blandet ? 1 : 0, preview.UtelatteRegler.Count);
        if (blandet)
        {
            var kandidat = Assert.Single(preview.GyldigeRegler);
            Assert.Equal(gyldig.Id, kandidat.Id);
            Assert.Equal("eldre", kandidat.Loep);
            Assert.Equal("", kandidat.Tittel);
            Assert.Equal(gyldig.Regelparametre.Trim(), kandidat.Regelparametre);
        }
        var resultat = await tjeneste.ImporterAsync(fil);
        Assert.Equal(preview.GyldigeRegler, (await tjeneste.EksporterAsync()).Regler);
        Assert.Equal(preview.UtelatteRegler.Select(r => r.Begrunnelse), resultat.Advarsler);
        Assert.Equal(1, resultat.AntallErstattet);
    }

    [Fact]
    public async Task Preview_av_eldre_tomme_IDer_er_stabil_og_tildeler_foerst_unike_IDer_ved_import()
    {
        var tjeneste = Tjeneste;
        var fil = new MalDatabase { Regler = [Regel("a") with { Id = Guid.Empty }, Regel("b") with { Id = Guid.Empty }] };
        var a = await tjeneste.ForhaandsvisAsync(fil);
        var b = await tjeneste.ForhaandsvisAsync(fil);
        Assert.Equal(a.GyldigeRegler, b.GyldigeRegler);
        Assert.All(a.GyldigeRegler, r => Assert.Equal(Guid.Empty, r.Id));
        Assert.Empty((await tjeneste.EksporterAsync()).Regler);
        await tjeneste.ImporterAsync(fil);
        var lagret = (await tjeneste.EksporterAsync()).Regler;
        Assert.All(lagret, r => Assert.NotEqual(Guid.Empty, r.Id));
        Assert.Equal(2, lagret.Select(r => r.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(null, "/admin/mal/overfor", HttpStatusCode.Unauthorized)]
    [InlineData("Leser", "/admin/mal/overfor", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", "/admin/mal/overfor", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", "/admin/mal/overfor", HttpStatusCode.OK)]
    [InlineData(null, "/api/eksport/maler-json", HttpStatusCode.Unauthorized)]
    [InlineData("Leser", "/api/eksport/maler-json", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", "/api/eksport/maler-json", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", "/api/eksport/maler-json", HttpStatusCode.OK)]
    public async Task Overfoeringsrute_og_eksport_svarer_med_serverpolicy(string? rolle, string sti, HttpStatusCode forventet)
    {
        await using var app = new TestApplikasjon();
        using var klient = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var svar = await klient.GetAsync(sti);
        Assert.Equal(forventet, svar.StatusCode);
        if (forventet != HttpStatusCode.OK)
            Assert.DoesNotContain("Last ned årsmal", await svar.Content.ReadAsStringAsync());
    }

    private static MalRegelEksport Regel(string loep) => new()
    {
        Id = Guid.NewGuid(), Loep = loep, Tittel = "Tittel " + loep,
        Kategori = Kategori.Gulbok, Regeltype = Regeltype.FastDato,
        Regelparametre = "{\"maaned\":5,\"dag\":2}", Valgaarssensitiv = true
    };

    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FA")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    [InlineData("Administrator", "")]
    public async Task Faktisk_Blazor_kommando_krever_administrator_for_baade_preview_og_import(string? rolle, string gruppe)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var tjeneste = scope.ServiceProvider.GetRequiredService<IMalDatautveksling>();
        var foer = (await tjeneste.EksporterAsync()).Regler;
        var identitet = new Kretsidentitet(Principal(rolle, gruppe));
        var kommando = new MaloverfoeringKommando(identitet, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), tjeneste);
        var fil = new MalDatabase { Regler = [Regel("importert")] };
        if (rolle == "Administrator")
        {
            Assert.Single((await kommando.ForhaandsvisAsync(fil)).GyldigeRegler);
            Assert.Equal(foer, (await tjeneste.EksporterAsync()).Regler);
            Assert.Equal(1, (await kommando.ImporterAsync(fil)).AntallImportert);
            identitet.Bruker = Principal("Leser", "FA");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ForhaandsvisAsync(new() { Regler = [] }));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ImporterAsync(new() { Regler = [] }));
            Assert.Equal(fil.Regler, (await tjeneste.EksporterAsync()).Regler);
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ForhaandsvisAsync(fil));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ImporterAsync(fil));
            Assert.Equal(foer, (await tjeneste.EksporterAsync()).Regler);
        }
    }

    private static ClaimsPrincipal Principal(string? rolle, string gruppe)
        => rolle is null ? new(new ClaimsIdentity()) : new(new ClaimsIdentity([new Claim(Brukerclaims.Rolle, rolle), new Claim(Brukerclaims.Gruppe, gruppe)], "Test"));
    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
    public void Dispose() => _t.Dispose();
}
