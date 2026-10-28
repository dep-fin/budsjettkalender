using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Utskrift;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class FristimportForhaandsvisningTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IFristDatautveksling Tjeneste => new FristDatautveksling(_t.Factory, TimeProvider.System);

    public FristimportForhaandsvisningTester()
    {
        _t.Db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = "FA", Navn = "Finansavdelingen" });
        _t.Db.SaveChanges();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preview_er_sideeffektfri_og_samme_effektive_helfil_som_bekreftet_import(bool tom)
    {
        var tjeneste = Tjeneste;
        await tjeneste.ImporterAsync(new() { Frister = [Frist("gammel")] });
        var foer = Felter(await tjeneste.EksporterAsync());
        var ny = Frist("Årlig <ramme> æøå") with { SynligFor = ["FA", "UKJENT"] };
        var fil = new FristDatabase { Frister = tom ? [] : [ny] };
        var preview = await tjeneste.ForhaandsvisAsync(fil);
        Assert.Equal(1, preview.EksisterendeAntall);
        Assert.Equal(foer, Felter(await tjeneste.EksporterAsync()));
        Assert.Equal(tom ? 0 : 1, preview.Frister.Count);
        if (!tom)
        {
            Assert.Single(preview.Advarsler);
            Assert.Equal(Felter(new() { Frister = [ny with { SynligFor = ["FA"] }] }), Felter(new() { Frister = preview.Frister }));
        }
        var resultat = await tjeneste.ImporterAsync(fil);
        Assert.Equal(Felter(new() { Frister = preview.Frister }), Felter(await tjeneste.EksporterAsync()));
        Assert.Equal(preview.Advarsler, resultat.Advarsler);
        Assert.Equal(1, resultat.AntallErstattet);
        Assert.Equal(tom ? 0 : 1, resultat.AntallImportert);
    }

    [Fact]
    public async Task Preview_bevarer_tomme_IDer_og_import_tildeler_foerst_nye_unike_IDer()
    {
        var fil = new FristDatabase { Frister = [Frist("a") with { Id = Guid.Empty }, Frist("b") with { Id = Guid.Empty }] };
        var a = await Tjeneste.ForhaandsvisAsync(fil);
        var b = await Tjeneste.ForhaandsvisAsync(fil);
        Assert.Equal(Felter(new() { Frister = a.Frister }), Felter(new() { Frister = b.Frister }));
        Assert.All(a.Frister, f => Assert.Equal(Guid.Empty, f.Id));
        Assert.Empty((await Tjeneste.EksporterAsync()).Frister);
        await Tjeneste.ImporterAsync(fil);
        var lagret = (await Tjeneste.EksporterAsync()).Frister;
        Assert.All(lagret, f => Assert.NotEqual(Guid.Empty, f.Id));
        Assert.Equal(2, lagret.Select(f => f.Id).Distinct().Count());
    }

    [Theory]
    [InlineData("versjon0")]
    [InlineData("versjon2")]
    [InlineData("null-liste")]
    [InlineData("null-rad")]
    [InlineData("duplikat")]
    public async Task Preview_og_import_avviser_samme_filfeil_foer_endring(string feil)
    {
        await Tjeneste.ImporterAsync(new() { Frister = [Frist("beholdes")] });
        var foer = Felter(await Tjeneste.EksporterAsync());
        var rad = Frist("ny");
        FristDatabase fil = feil switch
        {
            "versjon0" => new() { Versjon = 0, Frister = new[] { rad } },
            "versjon2" => new() { Versjon = 2, Frister = new[] { rad } },
            "null-liste" => new FristDatabase { Frister = null! },
            "null-rad" => new() { Frister = new FristEksport[] { null! } },
            _ => new() { Frister = new[] { rad, rad } }
        };
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste.ForhaandsvisAsync(fil));
        await Assert.ThrowsAsync<Valideringsfeil>(() => Tjeneste.ImporterAsync(fil));
        Assert.Equal(foer, Felter(await Tjeneste.EksporterAsync()));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FA")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    [InlineData("Administrator", "")]
    public async Task Faktisk_Blazor_kommando_autoriserer_hvert_preview_og_importkall(string? rolle, string gruppe)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var tjeneste = scope.ServiceProvider.GetRequiredService<IFristDatautveksling>();
        var foer = Felter(await tjeneste.EksporterAsync());
        var identitet = new Kretsidentitet(Principal(rolle, gruppe));
        var kommando = new FristoverfoeringKommando(identitet, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), tjeneste);
        var fil = new FristDatabase { Frister = [Frist("importert")] };
        if (rolle == "Administrator")
        {
            Assert.Single((await kommando.ForhaandsvisAsync(fil)).Frister);
            Assert.Equal(foer, Felter(await tjeneste.EksporterAsync()));
            Assert.Equal(1, (await kommando.ImporterAsync(fil)).AntallImportert);
            identitet.Bruker = Principal("Leser", "FA");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ForhaandsvisAsync(new() { Frister = [] }));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ImporterAsync(new() { Frister = [] }));
            Assert.Equal(Felter(fil), Felter(await tjeneste.EksporterAsync()));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ForhaandsvisAsync(fil));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.ImporterAsync(fil));
            Assert.Equal(foer, Felter(await tjeneste.EksporterAsync()));
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Leser", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", HttpStatusCode.OK)]
    public async Task Gamle_ruter_og_faktiske_filendepunkt_beholder_serverpolicy(string? rolle, HttpStatusCode forventet)
    {
        await using var app = new TestApplikasjon();
        using var klient = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        foreach (var sti in new[] { "/admin/data", "/admin/eksport", "/admin/abonnement", "/api/eksport/word?gruppe=FA", "/api/eksport/ics?gruppe=FA", "/api/eksport/frister-json", "/api/eksport/maler-json" })
        {
            var svar = await klient.GetAsync(sti);
            Assert.Equal(forventet, svar.StatusCode);
            if (forventet != HttpStatusCode.OK) Assert.DoesNotContain("Kun FA", await svar.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FA")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    [InlineData("Administrator", "")]
    public async Task Eksportpreview_autoriserer_hvert_kall_og_admin_bruker_eksplisitt_gruppes_utvalg(string? rolle, string gruppe)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var identitet = new Kretsidentitet(Principal(rolle, gruppe));
        var kommando = new EksportKommando(identitet, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), scope.ServiceProvider.GetRequiredService<FristEksportutvalg>());
        if (rolle == "Administrator")
        {
            var fa = await kommando.HentAsync(false, "FA", null, null);
            Assert.Equal(new[] { "FA og POL", "Kun FA" }, fa.Frister.Select(f => f.Tittel).Order());
            Assert.All(fa.Frister, f => Assert.False(f.VenterEndring));
            var pol = await kommando.HentAsync(false, "POL", null, null);
            Assert.Equal("FA og POL", Assert.Single(pol.Frister).Tittel);
            Assert.Equal(3, (await kommando.HentAsync(true, null, null, null)).Frister.Count);
            identitet.Bruker = Principal("Leser", "FA");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync(true, null, null, null));
        }
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync(true, null, null, null));
    }

    private static FristEksport Frist(string tittel) => new()
    {
        Id = Guid.NewGuid(), Tittel = tittel, Dato = new(2030, 11, 23), Datopresisjon = Datopresisjon.Maaned,
        Datokvalifikator = Datokvalifikator.Ultimo, Klokkeslett = null, Budsjettaar = 2032,
        Kategori = Kategori.Regnskap, Loep = "årsregnskap", Kilde = "R-17/2030", DokumentId = Guid.NewGuid(),
        Status = FristStatus.Forkastet, Opphav = Opphav.Bruker, ForeslaattAv = "syntetisk-innsender",
        Notat = "æøå\nAndre linje", GjentaRegelId = Guid.NewGuid(), SynligFor = ["FA"]
    };
    private static string Felter(FristDatabase fil) => JsonSerializer.Serialize(fil.Frister.OrderBy(f => f.Id).Select(f => f with { SynligFor = f.SynligFor.Order().ToArray() }));
    private static ClaimsPrincipal Principal(string? rolle, string gruppe) => rolle is null ? new(new ClaimsIdentity()) : new(new ClaimsIdentity([new Claim(Brukerclaims.Rolle, rolle), new Claim(Brukerclaims.Gruppe, gruppe)], "Test"));
    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
    public void Dispose() => _t.Dispose();
}
