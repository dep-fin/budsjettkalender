using System.Net;
using System.Text.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class FristVentemerkeHttpTester : IDisposable
{
    private readonly VentemerkeApplikasjon _app = new();
    private readonly Guid _synlig;
    private readonly Guid _utenForslag;
    private readonly Guid _pol;
    private readonly Guid _skjult;
    private readonly Guid[] _forslag;

    public FristVentemerkeHttpTester()
    {
        using var db = _app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        _synlig = LeggFrist(db, "HTTP71 publisert FA", "FA");
        _utenForslag = LeggFrist(db, "HTTP71 uten forslag", "FA");
        _pol = LeggFrist(db, "HTTP71 eksplisitt POL", "POL");
        _skjult = LeggFrist(db, "HTTP71 skjult FINFAG", "FIN-FAG");
        _forslag = new[] { _synlig, _pol, _skjult }.Select(id =>
        {
            var forslag = new Forslag
            {
                Id = Guid.NewGuid(), EndrerFristId = id, ForslagType = ForslagType.Endring,
                Tittel = "HTTP71-hemmelig-forslagstittel", Notat = "HTTP71-hemmelig-notat",
                KildeEllerInnsender = "HTTP71-hemmelig-innsender", Dato = new(2030, 12, 24), Budsjettaar = 2099
            };
            db.Forslag.Add(forslag);
            return forslag.Id;
        }).ToArray();
        db.SaveChanges();
    }

    [Theory]
    [InlineData("Administrator", "/api/frister", true)]
    [InlineData("Administrator", "/api/frister/landing", true)]
    [InlineData("Leser", "/api/frister", false)]
    [InlineData("Leser", "/api/frister/landing", false)]
    [InlineData("Bidragsyter", "/api/frister", false)]
    [InlineData("Bidragsyter", "/api/frister/landing", false)]
    public async Task Bare_administrator_faar_avledet_indikator_uten_forslagmetadata(string rolle, string rute, bool venter)
    {
        using var klient = Klient(rolle, "FA");
        var svar = await klient.GetAsync(rute);
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var tekst = await svar.Content.ReadAsStringAsync();
        var frister = JsonSerializer.Deserialize<List<FristDto>>(tekst, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(venter, frister.Single(f => f.Id == _synlig).VenterEndring);
        Assert.False(frister.Single(f => f.Id == _utenForslag).VenterEndring);
        IngenForslagmetadata(tekst);
        if (!venter)
        {
            Assert.All(frister, f => Assert.False(f.VenterEndring));
            IngenSkjulteFrister(tekst);
            Assert.DoesNotContain(_pol.ToString(), tekst);
            Assert.DoesNotContain("HTTP71 eksplisitt POL", tekst);
        }
    }

    [Theory]
    [InlineData("Administrator", true)]
    [InlineData("Leser", false)]
    [InlineData("Bidragsyter", false)]
    public async Task Kalenderens_HTTP_viser_merket_bare_for_administrator(string rolle, bool serMerke)
    {
        using var klient = Klient(rolle, "FA");
        var tekst = await klient.GetStringAsync("/kalender");
        Assert.Contains("HTTP71 publisert FA", tekst);
        Assert.Equal(serMerke, tekst.Contains("venter endring", StringComparison.Ordinal));
        IngenForslagmetadata(tekst);
        if (!serMerke)
        {
            IngenSkjulteFrister(tekst);
            Assert.DoesNotContain("HTTP71 eksplisitt POL", tekst);
            Assert.DoesNotContain(_pol.ToString(), tekst);
        }
    }

    [Theory]
    [InlineData("FA", "/api/frister", false)]
    [InlineData("FA", "/api/frister/landing", false)]
    [InlineData("POL", "/api/frister", true)]
    [InlineData("POL", "/api/frister/landing", true)]
    [InlineData("", "/api/frister", false)]
    [InlineData("", "/api/frister/landing", false)]
    public async Task POL_valg_og_forslagshemmelighold_bevares_ogsaa_med_forsok_paa_rolleparametere(
        string gruppe, string rute, bool serPol)
    {
        using var klient = Klient("Leser", gruppe);
        var tekst = await klient.GetStringAsync(rute + "?serAlt=true&VisGrupper=true&rolle=Administrator&gruppe=POL");
        var frister = JsonSerializer.Deserialize<List<FristDto>>(tekst, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(serPol, frister.Any(f => f.Id == _pol));
        Assert.All(frister, f => Assert.False(f.VenterEndring));
        IngenForslagmetadata(tekst);
        IngenSkjulteFrister(tekst);
        if (serPol)
        {
            Assert.Single(frister);
            Assert.DoesNotContain(_synlig.ToString(), tekst);
        }
        else
        {
            Assert.DoesNotContain(_pol.ToString(), tekst);
            Assert.DoesNotContain("HTTP71 eksplisitt POL", tekst);
        }
    }

    [Theory]
    [InlineData("/api/frister")]
    [InlineData("/api/frister/landing")]
    [InlineData("/kalender")]
    public async Task Anonym_faar_ingen_frister_eller_forslag(string rute)
    {
        using var klient = _app.CreateClient();
        var svar = await klient.GetAsync(rute);
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.DoesNotContain("HTTP71", await svar.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Enkeltoppslag_i_endringsskjema_viser_kun_publisert_synlig_innhold()
    {
        using var klient = Klient("Bidragsyter", "FA");
        foreach (var id in new[] { _synlig, _skjult, _pol })
        {
            var tekst = await klient.GetStringAsync($"/forslag/endre/{id}");
            if (id == _synlig) Assert.Contains("HTTP71 publisert FA", tekst);
            IngenForslagmetadata(tekst);
            // Den forespurte ID-en finnes i rutens prerender-metadata; innholdet skal mangle.
            Assert.DoesNotContain("HTTP71 skjult FINFAG", tekst);
            Assert.DoesNotContain("HTTP71 eksplisitt POL", tekst);
            Assert.DoesNotContain("venter endring", tekst);
        }
        using var anonym = _app.CreateClient();
        var svar = await anonym.GetAsync($"/forslag/endre/{_synlig}");
        Assert.Equal(HttpStatusCode.Unauthorized, svar.StatusCode);
        Assert.DoesNotContain("HTTP71", await svar.Content.ReadAsStringAsync());
    }

    private void IngenForslagmetadata(string tekst)
    {
        foreach (var markor in new[] { "HTTP71-hemmelig-forslagstittel", "HTTP71-hemmelig-notat",
            "HTTP71-hemmelig-innsender", "2030-12-24", "\"budsjettaar\":2099" }.Concat(_forslag.Select(id => id.ToString())))
            Assert.DoesNotContain(markor, tekst, StringComparison.OrdinalIgnoreCase);
    }

    private void IngenSkjulteFrister(string tekst)
    {
        Assert.DoesNotContain("HTTP71 skjult FINFAG", tekst);
        Assert.DoesNotContain(_skjult.ToString(), tekst);
    }

    private HttpClient Klient(string rolle, string grupper)
    {
        var klient = _app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        return klient;
    }

    private static Guid LeggFrist(AppDbContext db, string tittel, string gruppe)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = tittel, Dato = new(2030, 11, 20), Budsjettaar = 2031,
            Status = FristStatus.Godkjent, Synlighet = [new() { GruppeKode = gruppe }]
        };
        db.Frister.Add(frist);
        return frist.Id;
    }

    public void Dispose() => _app.Dispose();

    private sealed class VentemerkeApplikasjon : TestApplikasjon
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddSingleton<TimeProvider>(new FastKlokke(new(2030, 11, 1))));
        }
    }
}
