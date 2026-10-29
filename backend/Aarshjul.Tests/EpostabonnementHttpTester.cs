using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aarshjul.Infrastructure.Epost;
using Microsoft.Extensions.DependencyInjection;
using Aarshjul.Application.Datautveksling;
using System.Text.Json;
using Aarshjul.Domain;

namespace Aarshjul.Tests;

public sealed class EpostabonnementHttpTester
{
    [Theory]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData("Administrator")]
    public async Task Adressebytte_serverbinder_eier_bevarer_valg_og_returnerer_trygg_versjonert_status(string rolle)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var fabrikk = app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Aarshjul.Infrastructure.AppDbContext>>();
        var post = app.Services.GetRequiredService<KontrollertBekreftelsespost>();
        var tjeneste = new Aarshjul.Infrastructure.Epost.EpostabonnementTjeneste(fabrikk, new TidligereKlokke(), post);
        var status = await tjeneste.MeldPaaAsync("test-bruker", "gammel@fin.dep.no", true, false, true, [2031], [Aarshjul.Domain.Kategori.Regnskap]);
        var token = post.Hent("test-bruker")!.Token;
        using var annen = app.CreateClient(); annen.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator"); annen.DefaultRequestHeaders.Add("X-Test-Bruker", "annen");
        Assert.Equal(HttpStatusCode.BadRequest, (await annen.PostAsJsonAsync("/api/epostabonnement/adresse", new { adresse = "ny@jd.dep.no", egenadresseSamtykke = true, status.Versjon, brukerId = "test-bruker" })).StatusCode);
        using var svar = await klient.PostAsJsonAsync("/api/epostabonnement/adresse", new { adresse = "ny@JD.DEP.NO", egenadresseSamtykke = true, status.Versjon, brukerId = "annen", nyeFrister = true, vesentligeEndringer = false, budsjettaar = new[] { 2040 } });
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
        var json = await svar.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("VenterPaaBekreftelse", (string?)json!["status"]); Assert.Equal("ny@jd.dep.no", (string?)json["adresse"]);
        Assert.False((bool)json["nyeFrister"]!); Assert.True((bool)json["vesentligeEndringer"]!); Assert.Null(json["aktivert"]);
        Assert.Equal(2031, (int)json["budsjettaar"]![0]!); Assert.Equal((int)Aarshjul.Domain.Kategori.Regnskap, (int)json["kategorier"]![0]!);
        var tekst = await svar.Content.ReadAsStringAsync();
        Assert.False(tekst.Contains(token, StringComparison.Ordinal), "Status må ikke inneholde hemmelig bekreftelse.");
        Assert.DoesNotContain("Hash", tekst, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("token", tekst, StringComparison.OrdinalIgnoreCase);
        using var stale = await klient.PostAsJsonAsync("/api/epostabonnement/adresse", new { adresse = "annen@jd.dep.no", egenadresseSamtykke = true, status.Versjon });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.True(stale.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/epostabonnement/bekreft", new { token })).StatusCode);
        var nyToken = post.Hent("test-bruker")!.Token;
        Assert.Equal(HttpStatusCode.OK, (await klient.PostAsJsonAsync("/api/epostabonnement/bekreft", new { token = nyToken })).StatusCode);
        Assert.Equal("IkkePaameldt", (string?)(await annen.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
    }

    [Theory]
    [InlineData("ny@dep.no", true)]
    [InlineData("ny@jd.dep.no", false)]
    public async Task Adressebytte_avviser_ugyldig_inndata_uten_mutasjon_eller_ny_post(string adresse, bool samtykke)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        var status = await (await klient.PostAsJsonAsync("/api/epostabonnement", new { adresse = "egen@fin.dep.no", godtarStandardvalg = true })).Content.ReadFromJsonAsync<JsonObject>();
        var post = app.Services.GetRequiredService<KontrollertBekreftelsespost>(); var token = post.Hent("test-bruker")!.Token;
        using var svar = await klient.PostAsJsonAsync("/api/epostabonnement/adresse", new { adresse, egenadresseSamtykke = samtykke, versjon = (string)status!["versjon"]! });
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode); Assert.True(svar.Headers.CacheControl?.NoStore);
        Assert.Equal((string)status["versjon"]!, (string)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["versjon"]!);
        Assert.True(post.Hent("test-bruker")!.Token == token, "Ugyldig adressebytte må ikke sende ny bekreftelse.");
    }

    private sealed class TidligereKlokke : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddMinutes(-2);
    }

    [Fact]
    public async Task Faktisk_frist_eksport_inneholder_bare_v1_frister_etter_import_sletting_og_paamelding()
    {
        using var app = new TestApplikasjon(); using var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/epostabonnement",
            new { adresse = "eksportvern@fin.dep.no", godtarStandardvalg = true })).StatusCode);
        var import = app.Services.GetRequiredService<IFristDatautveksling>();
        await import.ImporterAsync(new() { Frister = [] });
        await import.ImporterAsync(new() { Frister = [new() { Id = Guid.NewGuid(), Tittel = "Bare fristdata",
            Dato = new(2030, 11, 20), Budsjettaar = 2031, Status = FristStatus.Godkjent, SynligFor = ["FAG"] }] });
        var svar = await admin.GetAsync("/api/eksport/frister-json"); Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        Assert.Equal("application/json", svar.Content.Headers.ContentType!.MediaType);
        var json = await svar.Content.ReadAsStringAsync(); using var dokument = JsonDocument.Parse(json);
        Assert.Equal(new[] { "versjon", "eksportertTid", "frister" }, dokument.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, dokument.RootElement.GetProperty("versjon").GetInt32());
        Assert.Equal("Bare fristdata", dokument.RootElement.GetProperty("frister")[0].GetProperty("tittel").GetString());
        foreach (var hemmelig in new[] { "eksportvern@", "Bekreftelses", "HendelseId", "FoerJson", "Publiseringssekvens", "OppsummeringId" })
            Assert.DoesNotContain(hemmelig, json, StringComparison.OrdinalIgnoreCase);
        using var anonym = app.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonym.GetAsync("/api/eksport/frister-json")).StatusCode);
        using var leser = app.CreateClient(); leser.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        Assert.Equal(HttpStatusCode.Forbidden, (await leser.GetAsync("/api/eksport/frister-json")).StatusCode);
    }
    [Theory]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    [InlineData("Administrator")]
    public async Task Aars_og_kategoriutvalg_lagres_normalisert_for_egen_identitet(string rolle)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var svar = await klient.PostAsJsonAsync("/api/epostabonnement", new
        {
            adresse = "egen@fin.dep.no", godtarStandardvalg = true,
            budsjettaar = new[] { 2032, 2031, 2032 }, kategorier = new[] { 2, 0, 2 }, brukerId = "annen"
        });
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var status = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.False((bool)status!["alleBudsjettaar"]!); Assert.False((bool)status["alleKategorier"]!);
        Assert.Equal(new[] { 2031, 2032 }, status["budsjettaar"]!.AsArray().Select(x => (int)x!));
        Assert.Equal(new[] { 0, 2 }, status["kategorier"]!.AsArray().Select(x => (int)x!));
        using var nyInnlogging = app.CreateClient(); nyInnlogging.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        status = await nyInnlogging.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        var versjon = (string)status!["versjon"]!;
        using var annen = app.CreateClient(); annen.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator"); annen.DefaultRequestHeaders.Add("X-Test-Bruker", "annen");
        Assert.Equal(HttpStatusCode.BadRequest, (await annen.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = true, vesentligeEndringer = false, versjon, budsjettaar = new[] { 2040 }, brukerId = "test-bruker" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await nyInnlogging.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = true, vesentligeEndringer = false, versjon, budsjettaar = Array.Empty<int>(), kategorier = (int[]?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await klient.PostAsJsonAsync("/api/epostabonnement/valg", new { nyeFrister = true, vesentligeEndringer = false, versjon, budsjettaar = new[] { 2031 } })).StatusCode);
        status = await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement");
        Assert.True((bool)status!["alleBudsjettaar"]!); Assert.True((bool)status["alleKategorier"]!);
    }

    [Theory]
    [InlineData("{\"budsjettaar\":[0]}")]
    [InlineData("{\"budsjettaar\":[10000]}")]
    [InlineData("{\"kategorier\":[-1]}")]
    [InlineData("{\"kategorier\":[999]}")]
    [InlineData("{\"budsjettaar\":\"2031\"}")]
    public async Task Ugyldige_filter_avvises_ogsaa_ved_direkte_JSON(string utvalg)
    {
        using var app = new TestApplikasjon(); using var klient = app.CreateClient(); klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        var json = JsonNode.Parse(utvalg)!.AsObject(); json["adresse"] = "egen@fin.dep.no"; json["godtarStandardvalg"] = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await klient.PostAsJsonAsync("/api/epostabonnement", json)).StatusCode);
        Assert.Equal("IkkePaameldt", (string?)(await klient.GetFromJsonAsync<JsonObject>("/api/epostabonnement"))!["status"]);
    }
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
    [InlineData("/adresse")]
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
    [InlineData("/adresse")]
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
