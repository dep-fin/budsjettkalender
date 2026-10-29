using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class InternkalenderTilgangTester
{
    [Theory]
    [InlineData(null, "", HttpStatusCode.Unauthorized)]
    [InlineData("Leser", "FA", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", "FA", HttpStatusCode.Forbidden)]
    [InlineData("Bidragsyter", "FIN-FAG", HttpStatusCode.Forbidden)]
    [InlineData("Administrator", "", HttpStatusCode.OK)]
    [InlineData("Administrator", "SBR", HttpStatusCode.OK)]
    public async Task Alle_ni_interne_direkteruter_har_ErSbr_policy_og_hele_avviste_svar_er_uten_interndata(string? rolle, string grupper, HttpStatusCode forventet)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var k = scope.ServiceProvider.GetRequiredService<IInternkalender>();
        var r = scope.ServiceProvider.GetRequiredService<IGjoeremaalRegler>();
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "UX16SECRETORIGIN");
        var g = await k.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16SECRETTITLE", Notat = "UX16SECRETNOTE", Ansvarlige = [new(null, "UX16SECRETASSIGNEE")] });
        await k.FullfoerAsync(g, "UX16SECRETID", "UX16SECRETCOMPLETER");
        var regel = await r.OpprettAsync(new() { Tittel = "UX16SECRETRULE", Notat = "UX16SECRETRULENOTE", Rundetyper = [Rundetype.Regnskap] });
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        foreach (var sti in Stier(runde, g, regel))
        {
            var svar = await klient.GetAsync(sti);
            Assert.Equal(forventet, svar.StatusCode);
            var innhold = await svar.Content.ReadAsStringAsync();
            if (forventet != HttpStatusCode.OK) Assert.DoesNotContain("UX16SECRET", innhold);
        }
        Assert.Equal("UX16SECRETTITLE", (await k.HentForRedigeringAsync(g))!.Tittel);
        Assert.Equal("UX16SECRETRULE", (await r.HentForRedigeringAsync(regel))!.Tittel);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FA")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    public async Task Hver_faktiske_interne_skrivekommando_avviser_anonym_leser_og_begge_bidragsroller_uten_lagring(string? rolle, string gruppe)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var k = scope.ServiceProvider.GetRequiredService<IInternkalender>();
        var r = scope.ServiceProvider.GetRequiredService<IGjoeremaalRegler>();
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var g = await k.HurtiglaggAsync(runde, "UX16 bevares");
        var regel = await r.OpprettAsync(Regel("UX16 bevares regel"));
        var forslag = await k.ForberedSynkAsync(runde);
        var foer = await Tilstand(k, r, runde);
        var principal = new Kretsidentitet(Principal(rolle, gruppe));
        var kommando = Kommando(scope.ServiceProvider, principal, k, r);
        var handlinger = new Func<Task>[] {
            () => kommando.OpprettRundeAsync(Rundetype.Marsrunden, 2036, false),
            () => kommando.OpprettRundeAsync(Rundetype.Marsrunden, 2036, true),
            () => kommando.AapneOvrigAsync(),
            () => kommando.SlettRundeAsync(runde),
            () => kommando.HurtiglaggAsync(runde, "Ulovlig"),
            () => kommando.OpprettGjoeremaalAsync(runde, new() { Tittel = "Ulovlig" }),
            () => kommando.OppdaterGjoeremaalAsync(g, new() { Tittel = "Ulovlig" }),
            () => kommando.SlettGjoeremaalAsync(g),
            () => kommando.FullfoerAsync(g),
            () => kommando.GjenaapneAsync(g),
            () => kommando.SynkroniserAsync(runde, forslag),
            () => kommando.OpprettRegelAsync(Regel("Ulovlig")),
            () => kommando.OppdaterRegelAsync(regel, Regel("Ulovlig")),
            () => kommando.SlettRegelAsync(regel)
        };
        foreach (var handling in handlinger)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(handling);
            Assert.Equal(foer, await Tilstand(k, r, runde));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    public async Task Aapen_internkommando_gjenleser_kretsens_rolle_foer_hver_handling(string? nyRolle)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var k = scope.ServiceProvider.GetRequiredService<IInternkalender>();
        var r = scope.ServiceProvider.GetRequiredService<IGjoeremaalRegler>();
        var identitet = new Kretsidentitet(Principal("Administrator", ""));
        var kommando = Kommando(scope.ServiceProvider, identitet, k, r);
        var runde = await kommando.OpprettRundeAsync(Rundetype.Regnskap, 2036, false);
        var id = await kommando.HurtiglaggAsync(runde, "UX16 opprettet med tilgang");
        identitet.Bruker = Principal(nyRolle, "FA");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.FullfoerAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.HurtiglaggAsync(runde, "Avvises"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kommando.SlettRundeAsync(runde));
        var g = Assert.Single(await k.HentGjoeremaalAsync(runde, null));
        Assert.Equal(GjoeremaalStatus.Aktiv, g.Status);
        Assert.Equal("UX16 opprettet med tilgang", g.Tittel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SBR")]
    public async Task Manuell_og_SBR_administrator_bruker_samme_faktiske_kommandoer_og_fullfoerer_med_serveridentiteten(string gruppe)
    {
        await using var app = new TestApplikasjon();
        using var scope = app.Services.CreateScope();
        var k = scope.ServiceProvider.GetRequiredService<IInternkalender>();
        var r = scope.ServiceProvider.GetRequiredService<IGjoeremaalRegler>();
        var identitet = new Kretsidentitet(Principal("Administrator", gruppe));
        var kommando = Kommando(scope.ServiceProvider, identitet, k, r);
        var regel = await kommando.OpprettRegelAsync(Regel("UX16 ny regel"));
        await kommando.OppdaterRegelAsync(regel, Regel("UX16 endret regel"));
        var runde = await kommando.OpprettRundeAsync(Rundetype.Regnskap, 2036, true);
        var generert = Assert.Single(await k.HentGjoeremaalAsync(runde, null));
        Assert.Equal("UX16 endret regel", generert.Tittel);
        var g = await kommando.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16 ny oppgave" });
        await kommando.OppdaterGjoeremaalAsync(g, new() { Tittel = "UX16 endret oppgave" });
        await kommando.FullfoerAsync(g);
        var full = Assert.Single(await k.HentGjoeremaalAsync(runde, null), x => x.Id == g);
        Assert.Equal(GjoeremaalStatus.Fullfoert, full.Status);
        Assert.Equal("UX16 serverbruker", full.FullfoertAvNavn);
        Assert.NotNull(full.FullfoertTid);
        await kommando.GjenaapneAsync(g);
        Assert.Null(Assert.Single(await k.HentGjoeremaalAsync(runde, null), x => x.Id == g).FullfoertTid);
        await kommando.SlettGjoeremaalAsync(g);
        await kommando.SlettRegelAsync(regel);
        await kommando.SynkroniserAsync(runde, await k.ForberedSynkAsync(runde));
        Assert.Empty(await k.HentGjoeremaalAsync(runde, null));
        var oevrig = await kommando.AapneOvrigAsync();
        Assert.Equal(Rundetype.Ovrig, (await k.HentRundeAsync(oevrig))!.Rundetype);
        await kommando.SlettRundeAsync(runde);
        Assert.Null(await k.HentRundeAsync(runde));
    }

    private static InternkalenderKommando Kommando(IServiceProvider tjenester, Kretsidentitet identitet, IInternkalender k, IGjoeremaalRegler r)
        => new(identitet, tjenester.GetRequiredService<IAuthorizationService>(),
            new Gjeldendebrukerkilde(identitet, tjenester.GetRequiredService<IBrukeroppslag>()), k, r);
    private static RegelInndata Regel(string tittel) => new() { Tittel = tittel, Rundetyper = [Rundetype.Regnskap] };
    private static async Task<string> Tilstand(IInternkalender k, IGjoeremaalRegler r, Guid runde)
        => JsonSerializer.Serialize(new { Runder = await k.HentRunderAsync(), Oppgaver = await k.HentGjoeremaalAsync(runde, null), Regler = await r.HentAlleAsync() });
    private static ClaimsPrincipal Principal(string? rolle, string gruppe) => rolle is null ? new(new ClaimsIdentity())
        : new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "ux16-server-id"), new(ClaimTypes.Name, "UX16 serverbruker"), new(Brukerclaims.Rolle, rolle), new(Brukerclaims.Gruppe, gruppe)], "Test"));
    private sealed class Kretsidentitet(ClaimsPrincipal bruker) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Bruker { get; set; } = bruker;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Bruker));
    }
    private static string[] Stier(Guid runde, Guid g, Guid regel) => ["/internkalender", "/internkalender/mine", "/internkalender/regler",
        "/internkalender/regler/ny", $"/internkalender/regler/{regel}", $"/internkalender/runde/{runde}",
        $"/internkalender/runde/{runde}/synk", $"/internkalender/gjoeremaal/ny/{runde}", $"/internkalender/gjoeremaal/{g}"];
}
