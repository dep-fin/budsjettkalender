using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Kalender;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class OpplastingsmetadataHttpTester : IAsyncLifetime
{
    private readonly MetadataApplikasjon _app = new();
    private const string Filnavn = "UX13 administrativ original + hemmelig.pdf";
    private const string Etikett = "Opplastet: " + Filnavn;
    private const string OffentligUrl = "https://www.regjeringen.no/no/offentlig-original/id88/";
    private Guid _fristId, _dokumentId;

    public async Task InitializeAsync()
    {
        using var scope = _app.Services.CreateScope();
        var forslagId = Guid.NewGuid();
        await using (var db = _app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
        {
            _dokumentId = Guid.NewGuid();
            db.BehandledeDokumenter.Add(new() { Id = _dokumentId, Kilde = "opplasting", DokumentNokkel = "UX13-intern-dokumentnokkel", Tittel = Filnavn, Url = OffentligUrl });
            db.Forslag.Add(new() { Id = forslagId, Tittel = "UX13 publisert", Dato = new(2030, 11, 20), Budsjettaar = 2031, Kategori = Kategori.Gulbok, Opphav = Opphav.Robot, KildeEllerInnsender = Etikett, DokumentId = _dokumentId });
            await db.SaveChangesAsync();
        }
        _fristId = await scope.ServiceProvider.GetRequiredService<IGodkjenningsko>().GodkjennAsync(new() { ForslagId = forslagId, Synlighetskoder = ["FA"] });
    }

    [Theory]
    [InlineData(null, "/api/frister?historikk=true")]
    [InlineData(null, "/api/frister/landing")]
    [InlineData("Leser", "/api/frister?historikk=true")]
    [InlineData("Leser", "/api/frister/landing")]
    [InlineData("Bidragsyter", "/api/frister?historikk=true")]
    [InlineData("Bidragsyter", "/api/frister/landing")]
    [InlineData("Administrator", "/api/frister?historikk=true")]
    [InlineData("Administrator", "/api/frister/landing")]
    public async Task Faktisk_HTTP_returnerer_opplastingsetiketten_bare_til_fullt_administratorinnsyn(string? rolle, string rute)
    {
        using var klient = Klient(rolle);
        using var svar = await klient.GetAsync(rute);
        var tekst = await svar.Content.ReadAsStringAsync();
        if (rolle is null)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
            Assert.DoesNotContain("UX13", tekst);
            return;
        }
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var frist = Assert.Single((await svar.Content.ReadFromJsonAsync<List<FristDto>>())!, f => f.Id == _fristId);
        Assert.Equal(rolle == "Administrator" ? Etikett : null, frist.Kilde);
        Assert.Equal(OffentligUrl, frist.KildeUrl);
        Assert.Equal(_dokumentId, frist.DokumentId);
        Assert.Equal("UX13 publisert", frist.Tittel);
        Assert.Equal(new DateOnly(2030, 11, 20), frist.Dato);
        Assert.Equal(2031, frist.Budsjettaar);
        Assert.Equal(Kategori.Gulbok, frist.Kategori);
        Assert.Equal(new[] { "FA" }, frist.Synlighet);
        Assert.DoesNotContain("UX13-intern-dokumentnokkel", tekst);
        if (rolle != "Administrator") Assert.DoesNotContain("UX13 administrativ original", tekst);
    }

    [Fact]
    public async Task Administrators_gruppeinnsyn_skjuler_filnavnet_og_full_JSON_eksport_bevarer_originaldata()
    {
        using var scope = _app.Services.CreateScope();
        var lesing = scope.ServiceProvider.GetRequiredService<IFristlesing>();
        var dto = await lesing.HentEnAsync(Synlighetskontekst.ForGruppe("FA"), _fristId);
        Assert.Null(dto!.Kilde);
        Assert.Equal(OffentligUrl, dto.KildeUrl);
        using var admin = Klient("Administrator");
        var json = await admin.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>("/api/eksport/frister-json");
        var rad = Assert.Single(json!["frister"]!.AsArray(), f => (Guid)f!["id"]! == _fristId)!;
        Assert.Equal(Etikett, (string?)rad["kilde"]);
        Assert.Equal(_dokumentId, (Guid)rad["dokumentId"]!);
    }

    [Fact]
    public async Task Gruppefeed_og_gruppeeksport_skjuler_filnavnet_mens_full_ICS_beholder_kilden()
    {
        using var scope = _app.Services.CreateScope();
        var abonnement = scope.ServiceProvider.GetRequiredService<IKalenderabonnement>();
        using var anonym = Klient(null);
        using var admin = Klient("Administrator");
        foreach (var gruppe in new string?[] { "FA", null })
        {
            var lenke = await abonnement.OpprettAsync(gruppe, "test-administrator");
            using var svar = await anonym.GetAsync($"/kalender/feed/{lenke.Token}.ics");
            Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
            BekreftIcs(await svar.Content.ReadAsStringAsync(), gruppe is null);
            var eksport = await admin.GetStringAsync(gruppe is null ? "/api/eksport/ics?alt=true" : "/api/eksport/ics?gruppe=FA");
            BekreftIcs(eksport, gruppe is null);
        }
        using var word = await admin.GetStreamAsync("/api/eksport/word?gruppe=FA");
        using var zip = new ZipArchive(word);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.Open());
            Assert.DoesNotContain("UX13 administrativ original", await reader.ReadToEndAsync());
        }
    }

    private void BekreftIcs(string tekst, bool full)
    {
        // Opphev bare RFC5545-linjebretting; ingen tolkning av kilden eller utvalget.
        tekst = tekst.Replace("\r\n ", "");
        Assert.Contains($"UID:{_fristId}@aarshjul.fin.dep.no", tekst);
        Assert.Equal(full, tekst.Contains(Filnavn, StringComparison.Ordinal));
    }

    private HttpClient Klient(string? rolle)
    {
        var klient = _app.CreateClient();
        if (rolle is not null)
        {
            klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
            klient.DefaultRequestHeaders.Add("X-Test-Grupper", "FA");
        }
        return klient;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
    private sealed class MetadataApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FastKlokke(new(2030, 11, 1))));
        }
    }
}
