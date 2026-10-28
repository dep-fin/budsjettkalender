using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

public sealed class AdministratoroversiktTester
{
    [Fact]
    public async Task Tom_database_gir_tom_lesende_oversikt()
    {
        using var db = new Testdatabase();
        Assert.Empty(await new AdministratoroversiktTjeneste(db.Factory).HentAsync());
        await using var kontroll = await db.Factory.CreateDbContextAsync();
        Assert.Empty(await kontroll.Brukere.ToArrayAsync());
    }

    [Fact]
    public async Task Bare_appkjente_administratorer_returneres_med_kilde_i_stabil_norsk_navnerekkefolge()
    {
        using var db = new Testdatabase();
        db.Db.Brukere.AddRange(
            Person("z", "Åse", true), Person("y", "Øyvind", false), Person("x", "Ægir", true),
            Person("b", "Ada", false), Person("a", "Ada", true),
            new Bruker { Id = "leser", Navn = "Skjult leser", AdminViaEntra = true },
            new Bruker { Id = "bidrag", Navn = "Skjult bidragsyter", Funksjonsrolle = Funksjonsrolle.Bidragsyter });
        await db.Db.SaveChangesAsync();
        var tjeneste = new AdministratoroversiktTjeneste(db.Factory);
        var forventet = new[] { new AdministratortilgangDto("a", "Ada", true), new("b", "Ada", false),
            new("x", "Ægir", true), new("y", "Øyvind", false), new("z", "Åse", true) };
        Assert.Equal(forventet, await tjeneste.HentAsync());
        Assert.Equal(forventet, await tjeneste.HentAsync());
    }

    [Fact]
    public async Task Gjentatte_lesinger_endrer_ingen_navn_roller_Entra_kilder_eller_medlemskap()
    {
        using var db = new Testdatabase();
        foreach (var kode in new[] { "FA", "POL" })
            db.Db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = kode, Navn = kode });
        var entra = Person("entra", "Entra navnet <beholdes>", true);
        entra.Grupper = [new() { BrukerId = "entra", GruppeKode = "FA", Kilde = GruppeMedlemskapKilde.Entra }, new() { BrukerId = "entra", GruppeKode = "POL", Kilde = GruppeMedlemskapKilde.Manuell }];
        db.Db.Brukere.AddRange(entra, Person("manuell", "Manuell", false),
            new Bruker { Id = "leser", Navn = "Leser", ErFin = true });
        await db.Db.SaveChangesAsync();
        var foer = await Lagret(db.Factory);
        var tjeneste = new AdministratoroversiktTjeneste(db.Factory);
        for (var i = 0; i < 3; i++) Assert.Equal(2, (await tjeneste.HentAsync()).Count);
        Assert.Equal(foer, await Lagret(db.Factory));
    }

    [Fact]
    public async Task Normal_claimssynk_skjer_ved_innlogging_foer_lesing_og_manuell_admin_bevares()
    {
        using var db = new Testdatabase();
        db.Db.Brukere.Add(Person("manuell", "Manuell", false));
        await db.Db.SaveChangesAsync();
        var oppslag = new BrukeroppslagTjeneste(db.Factory, Options.Create(new EntraGruppeOpsjoner()),
            Options.Create(new AdministratortilgangOpsjoner { Gruppeider = ["SBR"] }));
        ClaimsPrincipal Principal(string id, bool sbr) => new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, id), new Claim("name", "Innlogget " + id) }
                .Concat(sbr ? [new Claim("groups", "SBR")] : []), "Test"));
        await oppslag.HentEllerOpprettAsync(Principal("entra", true));
        await oppslag.HentEllerOpprettAsync(Principal("manuell", true));
        await oppslag.HentEllerOpprettAsync(Principal("manuell", false));
        // Baseline etter legitim autentiseringssynk, før selve leseoperasjonene.
        var foer = await Lagret(db.Factory);
        var tjeneste = new AdministratoroversiktTjeneste(db.Factory);
        Assert.Equal(new[] { new AdministratortilgangDto("entra", "Innlogget entra", true),
            new("manuell", "Innlogget manuell", false) }, await tjeneste.HentAsync());
        await tjeneste.HentAsync();
        Assert.Equal(foer, await Lagret(db.Factory));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FAG")]
    [InlineData("Leser", "POL")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    public async Task Hele_http_svaret_avviser_uautorisert_lesing_uten_personer_eller_tilgangskilder(string? rolle, string gruppe)
    {
        await using var app = new TestApplikasjon();
        await Seed(app);
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", gruppe);
        var svar = await klient.GetAsync("/admin/innstillinger?bruker=annen");
        Assert.Equal(rolle is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, svar.StatusCode);
        var heleSvaret = await svar.Content.ReadAsStringAsync();
        foreach (var skjult in new[] { "UX92 manuell", "UX92 Entra", "ux92-manuell-id", "ux92-entra-id", "Manuelt tildelt", "Forvaltes i Entra" })
            Assert.DoesNotContain(skjult, heleSvaret, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    public async Task Faktisk_Blazor_lesekommando_kontrollerer_identiteten_ved_hvert_kall(string? nyRolle)
    {
        await using var app = new TestApplikasjon();
        await Seed(app);
        using var scope = app.Services.CreateScope();
        var fabrikk = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var tilstand = new Kretsidentitet(Principal("Administrator", [new(ClaimTypes.NameIdentifier, "ux92-entra-id")]));
        var kommando = new AdministratoroversiktKommando(tilstand, scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),
            scope.ServiceProvider.GetRequiredService<IAdministratoroversikt>());
        Assert.True(Assert.Single(await kommando.HentAsync(), b => b.AdminViaEntra).ErDeg);
        var foer = await Lagret(fabrikk);
        tilstand.Bruker = Principal(nyRolle, []);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync());
        Assert.Equal(foer, await Lagret(fabrikk));
    }

    [Fact]
    public async Task Administratorclaim_uten_autentisert_identitet_gir_ikke_leseadgang()
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var bruker = new ClaimsPrincipal(new ClaimsIdentity([new(Brukerclaims.Rolle, "Administrator")]));
        var kommando = new AdministratoroversiktKommando(new Kretsidentitet(bruker), scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),
            scope.ServiceProvider.GetRequiredService<IAdministratoroversikt>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HentAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Deg_foelger_stabil_claimprioritet_aldri_likt_visningsnavn(int foersteClaim)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var fabrikk = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var typer = new[] { "http://schemas.microsoft.com/identity/claims/objectidentifier", "oid", ClaimTypes.NameIdentifier, "sub" };
        await using (var db = await fabrikk.CreateDbContextAsync())
        {
            for (var i = 0; i < 4; i++) db.Brukere.Add(Person("id-" + i, "Samme visningsnavn", i % 2 == 0));
            await db.SaveChangesAsync();
        }
        var claims = typer.Select((type, i) => new Claim(type, "id-" + i)).Skip(foersteClaim).ToList();
        claims.Add(new Claim("name", "Samme visningsnavn"));
        var kommando = new AdministratoroversiktKommando(new Kretsidentitet(Principal("Administrator", claims)),
            scope.ServiceProvider.GetRequiredService<IAuthorizationService>(), scope.ServiceProvider.GetRequiredService<IAdministratoroversikt>());
        var foer = await Lagret(fabrikk);
        var resultat = await kommando.HentAsync();
        Assert.Equal(new[] { 0, 1, 2, 3 }.Select(i => i == foersteClaim), resultat.Select(b => b.ErDeg));
        Assert.Equal(foer, await Lagret(fabrikk));
    }

    private static Bruker Person(string id, string navn, bool entra) => new()
    { Id = id, Navn = navn, Funksjonsrolle = Funksjonsrolle.Administrator, AdminViaEntra = entra, ErFin = true };

    private static async Task Seed(TestApplikasjon app)
    {
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        db.Brukere.AddRange(Person("ux92-manuell-id", "UX92 manuell", false), Person("ux92-entra-id", "UX92 Entra", true));
        await db.SaveChangesAsync();
    }

    private static ClaimsPrincipal Principal(string? rolle, IEnumerable<Claim> claims) => rolle is null ? new(new ClaimsIdentity())
        : new(new ClaimsIdentity(claims.Append(new Claim(Brukerclaims.Rolle, rolle)), "Test"));

    private static async Task<string> Lagret(IDbContextFactory<AppDbContext> fabrikk)
    {
        await using var db = await fabrikk.CreateDbContextAsync();
        var brukere = await db.Brukere.AsNoTracking().Include(b => b.Grupper).OrderBy(b => b.Id).ToArrayAsync();
        return JsonSerializer.Serialize(brukere.Select(b => new { b.Id, b.Navn, b.ErFin, b.Funksjonsrolle, b.AdminViaEntra,
            Grupper = b.Grupper.OrderBy(g => g.GruppeKode).Select(g => new { g.GruppeKode, g.Kilde }) }));
    }

    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
}
