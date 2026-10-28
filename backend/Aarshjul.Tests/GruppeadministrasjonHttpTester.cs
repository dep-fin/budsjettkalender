using System.Net;
using System.Net.Http.Json;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class GruppeadministrasjonHttpTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Leser", 403)]
    [InlineData("Bidragsyter", 403)]
    [InlineData("Administrator", 200)]
    public async Task Gruppeforvaltning_krever_administrator_i_HTTP(string? rolle, int status)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var svar = await klient.GetAsync("/admin/grupper");
        Assert.Equal(status, (int)svar.StatusCode);
        if (status == 200) Assert.Contains("Grupper", await svar.Content.ReadAsStringAsync());
        else Assert.DoesNotContain("Opprett gruppe", await svar.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Leser", 403)]
    [InlineData("Bidragsyter", 403)]
    [InlineData("Administrator", 200)]
    public async Task Gruppeinnsyn_krever_administrator_ogsaa_ved_direkte_HTTP_inngang(string? rolle, int status)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var svar = await klient.GetAsync("/admin/innsyn");
        Assert.Equal(status, (int)svar.StatusCode);
        if (status != 200) Assert.DoesNotContain("Velg gruppe", await svar.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Leser")]
    [InlineData("Bidragsyter")]
    public async Task Direkte_post_uten_interaktivt_skjema_avvises_uten_endring(string? rolle)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var foer = await Grupper().HentAlleAsync();
        var svar = await klient.PostAsync("/admin/grupper", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["kode"] = "UAUTORISERT", ["navn"] = "Uautorisert gruppe"
        }));
        // Ingen HTTP-skrive-API er lagt til. Blazor avviser ukjente/ubeskyttede skjema-POSTer
        // med 400 før en krets opprettes; rollepolicyen prøves separat på GET-inngangen.
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
        Assert.Equal(foer, await Grupper().HentAlleAsync());
    }

    [Fact]
    public async Task Lagret_egendefinert_synlighet_filtreres_i_HTTP_og_bevares_etter_deaktivering()
    {
        await Grupper().OpprettAsync("SKOK", "Skatteøkonomisk avdeling");
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IFristskriving>().OpprettAsync(new()
        {
            Tittel = "Egendefinert avdelingsfrist", Dato = new DateOnly(2030, 11, 20),
            Budsjettaar = 2031, Synlighetskoder = ["SKOK"]
        });
        foreach (var aktiv in new[] { true, false })
        {
            if (!aktiv) await Grupper().OppdaterAsync("SKOK", "Nytt avdelingsnavn", aktiv: false);
            using var klient = app.CreateClient();
            klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
            klient.DefaultRequestHeaders.Add("X-Test-Grupper", "SKOK");
            var synlig = Assert.Single((await klient.GetFromJsonAsync<List<FristDto>>("/api/frister?historikk=true"))!);
            Assert.Equal("Egendefinert avdelingsfrist", synlig.Tittel);
            Assert.Equal(["SKOK"], synlig.Synlighet);

            klient.DefaultRequestHeaders.Remove("X-Test-Grupper");
            foreach (var utenfor in new[] { "FA", "POL", "" })
            {
                klient.DefaultRequestHeaders.Remove("X-Test-Grupper");
                klient.DefaultRequestHeaders.Add("X-Test-Grupper", utenfor);
                var svar = await klient.GetAsync("/api/frister?historikk=true");
                Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
                Assert.DoesNotContain("Egendefinert avdelingsfrist", await svar.Content.ReadAsStringAsync());
            }
        }
    }

    private IGruppetjeneste Grupper() => app.Services.GetRequiredService<IGruppetjeneste>();
}
