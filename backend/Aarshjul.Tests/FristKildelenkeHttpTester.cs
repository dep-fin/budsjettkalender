using System.Net;
using System.Text.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class FristKildelenkeHttpTester : IDisposable
{
    private readonly KildeApplikasjon _app = new();
    private readonly Guid _synlig;
    private readonly Guid _skjult;
    private readonly Guid _skjultDokument;
    private const string TillattUrl = "https://www.regjeringen.no/no/http-kilde?a=1&tekst=%22%3E";

    public FristKildelenkeHttpTester()
    {
        using var db = _app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        _synlig = LeggTil(db, "HTTP69 synlig", "FA", TillattUrl);
        _skjult = LeggTil(db, "HTTP69 skjultPOL", "POL", "https://regjeringen.no/HTTP69-skjult-url");
        _skjultDokument = db.Frister.Local.Single(f => f.Id == _skjult).DokumentId!.Value;
        LeggTil(db, "HTTP69 avvist", "FA", "https://bruker@regjeringen.no/HTTP69-avvist-url");
        LeggTil(db, "HTTP69 upublisert", "FA", "https://regjeringen.no/HTTP69-upublisert-url", FristStatus.Forslag);
        db.SaveChanges();
    }

    [Theory]
    [InlineData("Leser", "/api/frister?historikk=true")]
    [InlineData("Bidragsyter", "/api/frister?historikk=true")]
    [InlineData("Administrator", "/api/frister?historikk=true")]
    [InlineData("Leser", "/api/frister/landing")]
    [InlineData("Bidragsyter", "/api/frister/landing")]
    [InlineData("Administrator", "/api/frister/landing")]
    public async Task Samme_kontrollerte_URL_returneres_til_alle_autoriserte_roller(string rolle, string rute)
    {
        using var klient = Klient(rolle, "FA");
        var svar = await klient.GetAsync(rute);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var tekst = await svar.Content.ReadAsStringAsync();
        var frister = JsonSerializer.Deserialize<List<FristDto>>(tekst, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(TillattUrl, frister.Single(f => f.Id == _synlig).KildeUrl);
        Assert.Null(frister.Single(f => f.Tittel == "HTTP69 avvist").KildeUrl);
        Assert.DoesNotContain("HTTP69-avvist-url", tekst);
        Assert.DoesNotContain("HTTP69-upublisert", tekst);
        Assert.DoesNotContain("HTTP69-dokumenttittel", tekst);
        Assert.DoesNotContain("HTTP69-dokumentnokkel", tekst);
        if (rolle != "Administrator") BekreftIngenSkjultMetadata(tekst);
    }

    [Theory]
    [InlineData("FA", "/api/frister?historikk=true", false)]
    [InlineData("FA", "/api/frister/landing", false)]
    [InlineData("POL", "/api/frister?historikk=true", true)]
    [InlineData("POL", "/api/frister/landing", true)]
    [InlineData("", "/api/frister?historikk=true", false)]
    [InlineData("", "/api/frister/landing", false)]
    public async Task POL_kilden_foelger_bare_eksplisitt_gruppetilgang(string gruppe, string rute, bool serPol)
    {
        using var klient = Klient("Leser", gruppe);
        var svar = await klient.GetAsync(rute);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var tekst = await svar.Content.ReadAsStringAsync();
        using var data = JsonDocument.Parse(tekst);
        var pol = data.RootElement.EnumerateArray().Where(f => f.GetProperty("id").GetGuid() == _skjult).ToArray();
        if (serPol)
        {
            Assert.Equal("https://regjeringen.no/HTTP69-skjult-url", Assert.Single(pol).GetProperty("kildeUrl").GetString());
            Assert.DoesNotContain("HTTP69 synlig", tekst);
            Assert.DoesNotContain(TillattUrl, tekst);
        }
        else
        {
            Assert.Empty(pol);
            BekreftIngenSkjultMetadata(tekst);
        }
    }

    [Theory]
    [InlineData("/api/frister")]
    [InlineData("/api/frister/landing")]
    public async Task Anonym_faar_ingen_frist_eller_kildemetadata(string rute)
    {
        using var klient = _app.CreateClient();
        var svar = await klient.GetAsync(rute);
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.DoesNotContain("HTTP69", await svar.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Eksisterende_enkeltoppslag_i_endringsskjema_lekker_ikke_skjult_frist()
    {
        // Det finnes ingen enkeltfrist-API. Denne HTTP-inngangen bruker HentEnAsync.
        using var klient = Klient("Bidragsyter", "FA");
        var synlig = await klient.GetStringAsync($"/forslag/endre/{_synlig}");
        Assert.Contains("HTTP69 synlig", synlig);
        var skjult = await klient.GetStringAsync($"/forslag/endre/{_skjult}");
        BekreftIngenSkjultMetadata(skjult);
        using var anonym = _app.CreateClient();
        var svar = await anonym.GetAsync($"/forslag/endre/{_synlig}");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.DoesNotContain("HTTP69", await svar.Content.ReadAsStringAsync());
    }

    private HttpClient Klient(string rolle, string grupper)
    {
        var klient = _app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        return klient;
    }

    private void BekreftIngenSkjultMetadata(string tekst)
    {
        foreach (var markor in new[] { "HTTP69 skjultPOL", "HTTP69-skjult-url", "HTTP69-dokumenttittel", "HTTP69-dokumentnokkel", _skjult.ToString(), _skjultDokument.ToString() })
            Assert.DoesNotContain(markor, tekst);
    }

    private static Guid LeggTil(AppDbContext db, string tittel, string gruppe, string? url, FristStatus status = FristStatus.Godkjent)
    {
        var dokument = new BehandletDokument
        {
            Id = Guid.NewGuid(), Kilde = "syntetisk", DokumentNokkel = "HTTP69-dokumentnokkel-" + Guid.NewGuid(),
            Tittel = "HTTP69-dokumenttittel", Url = url
        };
        db.BehandledeDokumenter.Add(dokument);
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = tittel, Dato = new(2030, 11, 20), Budsjettaar = 2031,
            DokumentId = dokument.Id, Status = status, Synlighet = [new() { GruppeKode = gruppe }]
        };
        db.Frister.Add(frist);
        return frist.Id;
    }

    public void Dispose() => _app.Dispose();

    private sealed class KildeApplikasjon : TestApplikasjon
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FastKlokke(new(2030, 11, 1))));
        }
    }
}
