using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aarshjul.Infrastructure.Epost;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class EpostabonnementHttpTester
{
    [Theory]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData("Administrator")]
    public async Task Varselvalg_haandheves_med_eierskap_versjon_og_minst_ett_valg(string rolle)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@fin.dep.no", godtarStandardvalg = true, nyeFrister = false, vesentligeEndringer = true });
        var status = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.False((bool)status!["nyeFrister"]!); Assert.True((bool)status["vesentligeEndringer"]!);
        var versjon = (string)status["versjon"]!;
        using var annen = app.CreateClient(); annen.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator"); annen.DefaultRequestHeaders.Add("X-Test-Bruker", "annen");
        Assert.Equal(HttpStatusCode.BadRequest, (await annen.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = true, vesentligeEndringer = true, versjon, brukerId = "test-bruker" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = false, vesentligeEndringer = false, versjon })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await klient.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = true, vesentligeEndringer = true, versjon })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await klient.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = false, vesentligeEndringer = true, versjon })).StatusCode);
        status = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.True((bool)status!["nyeFrister"]!); Assert.True((bool)status["vesentligeEndringer"]!);
    }
    [Fact]
    public async Task Leser_maa_velge_paa_egen_adresse_foer_abonnementet_venter_paa_bekreftelse()
    {
        using var app = new TestApplikasjon();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        var foer = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.Equal("IkkePaameldt", (string?)foer!["status"]);
        using var svar = await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@FIN.DEP.NO", godtarStandardvalg = true });
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var etter = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.Equal("VenterPaaBekreftelse", (string?)etter!["status"]);
        Assert.Equal("egen@fin.dep.no", (string?)etter["adresse"]);
        Assert.True((bool)etter["nyeFrister"]!);
        Assert.True((bool)etter["alleBudsjettaar"]!);
        Assert.True((bool)etter["alleKategorier"]!);
        Assert.Null(etter["aktivert"]);
    }

    [Fact]
    public async Task Bare_eieren_kan_bekrefte_en_gang_og_melde_seg_av()
    {
        using var app = new TestApplikasjon();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@fin.dep.no", godtarStandardvalg = true });
        var token = app.Services.GetRequiredService<KontrollertBekreftelsespost>().Hent("test-bruker")!.Token;
        using var feilBruker = app.CreateClient();
        feilBruker.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        feilBruker.DefaultRequestHeaders.Add("X-Test-Bruker", "annen-bruker");
        Assert.Equal(HttpStatusCode.BadRequest, (await feilBruker.PostAsJsonAsync("/api/epostabonnement/bekreft", new { token })).StatusCode);
        var aktiv = await klient.PostAsJsonAsync("/api/epostabonnement/bekreft", new { token });
        Assert.Equal(HttpStatusCode.OK, aktiv.StatusCode);
        Assert.Equal("Aktiv", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
        Assert.Equal(HttpStatusCode.OK, (await feilBruker.PostAsJsonAsync("/api/epostabonnement/meld-av", new { brukerId = "test-bruker" })).StatusCode);
        Assert.Equal("Aktiv", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
        var egetSvar = await klient.GetStringAsync("/api/epostabonnement");
        Assert.False(egetSvar.Contains(token, StringComparison.Ordinal), "Status må aldri returnere hemmelig bekreftelse.");
        Assert.DoesNotContain("bekreftelsesHash", egetSvar);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/epostabonnement/bekreft", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await klient.PostAsJsonAsync("/api/epostabonnement/meld-av", new { })).StatusCode);
        Assert.Equal("Avmeldt", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
    }

    [Theory]
    [InlineData("egen@dep.no")]
    [InlineData("egen@fin.dep.no.example.org")]
    [InlineData("egen@findep.no")]
    [InlineData("Egen <egen@fin.dep.no>")]
    [InlineData("a@fin.dep.no,b@jd.dep.no")]
    [InlineData("egen@fin.dep.no\r\nBcc:x@jd.dep.no")]
    [InlineData("egen@fin_.dep.no")]
    [InlineData("egen@-fin.dep.no")]
    [InlineData("egen@fin..dep.no")]
    [InlineData("ikke-en-adresse")]
    [InlineData("a..b@fin.dep.no")]
    [InlineData(".egen@fin.dep.no")]
    [InlineData("egen.@fin.dep.no")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@fin.dep.no")]
    public async Task Serveren_avviser_adresser_som_ikke_er_ett_gyldig_departementsdomene(string adresse)
    {
        using var app = new TestApplikasjon();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse, godtarStandardvalg = true })).StatusCode);
        Assert.Equal("IkkePaameldt", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
        Assert.Null(app.Services.GetRequiredService<KontrollertBekreftelsespost>().Hent("test-bruker"));
    }

    [Theory]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData("Administrator")]
    public async Task Alle_roller_har_bare_egen_selvbetjening_uten_utvidet_forslagsrolle(string rolle)
    {
        using var app = new TestApplikasjon();
        using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        Assert.Equal(HttpStatusCode.OK, (await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@jd.dep.no", godtarStandardvalg = true, brukerId = "annen" })).StatusCode);
        using var annen = app.CreateClient(); annen.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser"); annen.DefaultRequestHeaders.Add("X-Test-Bruker", "annen");
        Assert.Equal("IkkePaameldt", (string?)(await annen.GetFromJsonAsync<JsonObject>("/api/epostabonnement?brukerId=test-bruker"))!["status"]);
        var html = await klient.GetStringAsync("/epostvarsling");
        Assert.Contains("E-postvarsling", html);
        Assert.Equal(rolle == "Bidragsyter", html.Contains("href=\"/forslag/mine\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/bekreft")]
    [InlineData("/ny-lenke")]
    [InlineData("/meld-av")]
    [InlineData("/valg")]
    public async Task Anonyme_skrivekall_avvises(string sti)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        using var svar = await klient.PostAsJsonAsync("/api/epostabonnement" + sti, new { adresse = "egen@fin.dep.no", godtarStandardvalg = true, token = "ugyldig" });
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await klient.GetAsync("/api/epostabonnement")).StatusCode);
    }

    [Theory]
    [InlineData("/ny-lenke")]
    [InlineData("/meld-av")]
    [InlineData("/valg")]
    public async Task Enkel_HTML_form_POST_kan_ikke_endre_et_eget_abonnement(string sti)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@fin.dep.no", godtarStandardvalg = true });
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await klient.PostAsync("/api/epostabonnement" + sti, form)).StatusCode);
        Assert.Equal("VenterPaaBekreftelse", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
    }
}
