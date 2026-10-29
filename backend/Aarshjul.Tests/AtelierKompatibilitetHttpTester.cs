using System.Net;
using System.Net.Http.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class AtelierKompatibilitetHttpTester
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("Leser", "FAG")]
    [InlineData("Leser", "POL")]
    [InlineData("Bidragsyter", "FA")]
    [InlineData("Bidragsyter", "FIN-FAG")]
    [InlineData("Administrator", "")]
    public async Task Samlet_Atelier_holder_skjulte_frister_forslag_metadata_og_interndata_ute_av_hele_HTTP_svar(string? rolle, string gruppe)
    {
        await using var app = new AkseptanseApplikasjon();
        using var scope = app.Services.CreateScope();
        var internkalender = scope.ServiceProvider.GetRequiredService<IInternkalender>();
        var runde = await internkalender.OpprettRundeAsync(Rundetype.Regnskap, 2032, "UX93SECRETORIGIN");
        var gjoeremaal = await internkalender.OpprettGjoeremaalAsync(runde, new()
        {
            Tittel = "UX93SECRETINTERNALTITLE", Notat = "UX93SECRETINTERNALNOTE",
            Ansvarlige = [new(null, "UX93SECRETASSIGNEE")]
        });
        Guid synlig, skjult, forslag, dokument;
        await using (var db = await app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            synlig = Guid.NewGuid(); skjult = Guid.NewGuid(); forslag = Guid.NewGuid(); dokument = Guid.NewGuid();
            db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = "UX93-SKJULT", Navn = "UX93SECRETGROUP" });
            db.BehandledeDokumenter.Add(new()
            {
                Id = dokument, Kilde = "opplasting", DokumentNokkel = "UX93SECRETDOCUMENTKEY",
                Tittel = "UX93SECRETDOCUMENTTITLE", Url = "https://www.regjeringen.no/no/ux93-offentlig/"
            });
            db.Frister.Add(new()
            {
                Id = synlig, Tittel = "UX93 felles publisert frist", Budsjettaar = 2032, Dato = new(2030, 11, 20),
                Kilde = "Opplastet: UX93SECRETUPLOAD.pdf", DokumentId = dokument,
                Synlighet = [new() { GruppeKode = "FA" }, new() { GruppeKode = "FIN-FAG" }, new() { GruppeKode = "FAG" }, new() { GruppeKode = "POL" }]
            });
            db.Frister.Add(new()
            {
                Id = skjult, Tittel = "UX93SECRETHIDDENTITLE", Notat = "UX93SECRETHIDDENNOTE", Loep = "UX93SECRETHIDDENLOEP",
                Budsjettaar = 2098, Dato = new(2030, 11, 20), Synlighet = [new() { GruppeKode = "UX93-SKJULT" }]
            });
            db.Forslag.Add(new()
            {
                Id = forslag, Tittel = "UX93SECRETPROPOSALTITLE", Notat = "UX93SECRETPROPOSALNOTE",
                EndrerFristId = synlig, ForslagType = ForslagType.Endring, KildeEllerInnsender = "UX93SECRETREQUESTER",
                Dato = new(2030, 11, 20), Budsjettaar = 2032, DokumentId = dokument,
                UttrekksBevis = [new() { Id = Guid.NewGuid(), Felt = "tittel", Kildeutdrag = "UX93SECRETEVIDENCE", TolketVerdi = "UX93SECRETPROPOSALTITLE", Konfidens = 0.8 }]
            });
            await db.SaveChangesAsync();
        }

        // Positiv kontroll: de fortrolige dataene finnes og serveren leverer dem til administratoren.
        using var administrator = app.CreateClient();
        administrator.DefaultRequestHeaders.Add("X-Test-Rolle", "Administrator");
        Assert.Contains("UX93SECRETHIDDENTITLE", await administrator.GetStringAsync("/admin/frister?aar=2098"));
        Assert.Contains("UX93SECRETPROPOSALTITLE", await administrator.GetStringAsync("/admin/ko?aar=2032"));
        Assert.Contains("UX93SECRETINTERNALTITLE", await administrator.GetStringAsync($"/internkalender/runde/{runde}"));

        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", gruppe);
        foreach (var sti in new[] { "/", "/kalender", "/aarshjul", "/budsjettaar", "/api/frister?historikk=true", "/api/frister/landing" })
        {
            using var svar = await klient.GetAsync(sti + (sti.Contains('?') ? "&" : "?") + "serAlt=true&rolle=Administrator&gruppe=UX93-SKJULT");
            Assert.Equal(rolle is null ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, svar.StatusCode);
            var innhold = await svar.Content.ReadAsStringAsync();
            if (rolle != "Administrator")
            {
                Assert.DoesNotContain("UX93SECRET", innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(skjult.ToString(), innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(forslag.ToString(), innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(gjoeremaal.ToString(), innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(runde.ToString(), innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("venter endring", innhold, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("value=\"2098\"", innhold, StringComparison.OrdinalIgnoreCase);
                if (rolle is not null && sti.StartsWith("/api/frister", StringComparison.Ordinal))
                {
                    var data = (await svar.Content.ReadFromJsonAsync<List<FristDto>>())!;
                    var forventetAntall = sti == "/api/frister/landing" ? 1 : gruppe switch { "FA" => 3, "FAG" or "POL" => 2, _ => 1 };
                    Assert.Equal(forventetAntall, data.Count);
                    var frist = Assert.Single(data, f => f.Id == synlig);
                    Assert.False(frist.VenterEndring);
                    Assert.Null(frist.Kilde);
                    Assert.Equal("https://www.regjeringen.no/no/ux93-offentlig/", frist.KildeUrl);
                    Assert.DoesNotContain(data, f => f.Id == skjult || f.Budsjettaar == 2098);
                }
            }
        }

        // Samlet ruteflate etter UX17; avvisningen prøves på serverens hele svar.
        foreach (var sti in new[]
        {
            "/admin/frister?aar=2098", "/admin/frist/ny", $"/admin/frist/{skjult}", $"/admin/frist/juster/{forslag}", "/admin/ko?aar=2032", "/admin/last-opp", "/admin/generer?aar=2032",
            "/admin/mal", "/admin/mal/ny", "/admin/mal/proev", "/admin/mal/overfor", "/admin/grupper", "/admin/innsyn", "/admin/eksport", "/admin/abonnement", "/admin/data",
            "/admin/innstillinger", "/admin/innstillinger/faseinndeling", "/internkalender", "/internkalender/mine", "/internkalender/regler", "/internkalender/regler/ny",
            $"/internkalender/runde/{runde}", $"/internkalender/runde/{runde}/synk", $"/internkalender/gjoeremaal/{gjoeremaal}", $"/internkalender/gjoeremaal/ny/{runde}",
            "/api/eksport/frister-json", "/api/eksport/maler-json", "/api/eksport/word?alt=true", "/api/eksport/ics?alt=true"
        })
        {
            using var svar = await klient.GetAsync(sti);
            var forventet = rolle is null ? HttpStatusCode.Unauthorized : rolle == "Administrator" ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            Assert.Equal(forventet, svar.StatusCode);
            if (forventet != HttpStatusCode.OK) Assert.DoesNotContain("UX93SECRET", await svar.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class AkseptanseApplikasjon : TestApplikasjon
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FastKlokke(new(2030, 11, 1))));
        }
    }
}
