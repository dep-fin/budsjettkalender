using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class KoKildefilterHttpTester : IDisposable
{
    private readonly TestApplikasjon _app = new();
    private readonly Guid _id = Guid.NewGuid();

    public KoKildefilterHttpTester()
    {
        using var scope = _app.Services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Forslag.Add(new()
        {
            Id = _id, Tittel = "KO73-hemmelig-forslag", Opphav = Opphav.Bruker,
            KildeEllerInnsender = "KO73-hemmelig-innsender", Notat = "KO73-hemmelig-notat",
            Dato = new DateOnly(2030, 11, 20), Budsjettaar = 2031,
            UttrekksBevis = [new()
            {
                Id = Guid.NewGuid(), Felt = "dato", TolketVerdi = "20.11.2030",
                Kildeutdrag = "KO73-hemmelig-bevis", Konfidens = 0.99
            }]
        });
        db.Forslag.Add(new()
        {
            Id = Guid.NewGuid(), Tittel = "KO73-hemmelig-robotforslag", Opphav = Opphav.Robot,
            KildeEllerInnsender = "KO73-hemmelig-robotkilde", Dato = new DateOnly(2030, 11, 20)
        });
        db.SaveChanges();
    }

    [Theory]
    [InlineData(null, null, 401)]
    [InlineData("Leser", "FA", 403)]
    [InlineData("Bidragsyter", "FA", 403)]
    [InlineData("Leser", "POL", 403)]
    [InlineData("Administrator", "FA", 200)]
    public async Task Faktisk_ko_GET_beskytter_kildevalg_og_alle_metadata(string? rolle, string? grupper, int status)
    {
        using var klient = _app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        if (grupper is not null) klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        var svar = await klient.GetAsync("/admin/ko?aar=alle");
        Assert.Equal(status, (int)svar.StatusCode);
        var innhold = await svar.Content.ReadAsStringAsync();
        var markoerer = new[] { "KO73-hemmelig-forslag", "KO73-hemmelig-innsender", "KO73-hemmelig-notat",
            "KO73-hemmelig-bevis", "KO73-hemmelig-robotkilde", "KO73-hemmelig-robotforslag", _id.ToString() };
        if (status == 200)
        {
            Assert.Contains("Kilde eller innsender", innhold);
            foreach (var markoer in markoerer[..^1]) Assert.Contains(markoer, innhold, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.DoesNotContain("Kilde eller innsender", innhold);
            foreach (var markoer in markoerer) Assert.DoesNotContain(markoer, innhold, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null, null, 401)]
    [InlineData("Leser", "FA", 403)]
    [InlineData("Bidragsyter", "FA", 403)]
    [InlineData("Bidragsyter", "FIN-FAG", 403)]
    [InlineData("Leser", "POL", 403)]
    public async Task Arbeidsaarsruter_sender_ingen_administrative_metadata_til_andre_roller(
        string? rolle, string? grupper, int status)
    {
        using var klient = _app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        if (grupper is not null) klient.DefaultRequestHeaders.Add("X-Test-Grupper", grupper);
        foreach (var aar in new[] { "2031", "alle" })
        foreach (var sti in new[] { "/admin/frister", "/admin/ko", "/admin/last-opp", "/admin/generer",
            "/admin/frist/ny", $"/admin/frist/{_id}", $"/admin/frist/juster/{_id}" })
        {
            var svar = await klient.GetAsync($"{sti}?aar={aar}");
            Assert.Equal(status, (int)svar.StatusCode);
            var tekst = await svar.Content.ReadAsStringAsync();
            foreach (var markoer in new[] { "KO73-hemmelig", _id.ToString(), "Kilde eller innsender",
                "Arbeidsår", "åpne forslag", "Uttrekksbevis", "Budsjettår 2031" })
                Assert.DoesNotContain(markoer, tekst, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Dispose() => _app.Dispose();
}
