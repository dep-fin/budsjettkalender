namespace Aarshjul.Tests;

public sealed class NavigasjonHttpTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Leser", 403)]
    [InlineData("Bidragsyter", 403)]
    [InlineData("Administrator", 200)]
    public async Task Regelbyggerens_liste_ny_rediger_og_kopiruter_krever_administrator_paa_server(string? rolle, int status)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var id = Guid.NewGuid();
        foreach (var sti in new[] { "/admin/mal", "/admin/mal/ny", $"/admin/mal/{id}", $"/admin/mal/ny?kopierFra={id}", "/admin/mal/proev?aar=2032", "/admin/generer?aar=2032" })
        {
            var svar = await klient.GetAsync(sti);
            Assert.Equal(status, (int)svar.StatusCode);
            if (status != 200)
            {
                var tekst = await svar.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Lagre regel", tekst);
                Assert.DoesNotContain("Forhåndsvisning for fem budsjettår", tekst);
            }
        }
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Leser", 403)]
    [InlineData("Administrator", 403)]
    [InlineData("Bidragsyter", 200)]
    public async Task Forslagsinnganger_avviser_uautoriserte_serverkall_uavhengig_av_skjulte_UI_handlinger(string? rolle, int status)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        var id = Guid.NewGuid();
        foreach (var sti in new[] { "/forslag/ny", $"/forslag/endre/{id}", $"/forslag/{id}/rediger" })
        {
            var svar = await klient.GetAsync(sti);
            Assert.Equal(status, (int)svar.StatusCode);
            var tekst = await svar.Content.ReadAsStringAsync();
            if (status != 200)
            {
                Assert.DoesNotContain("Send inn", tekst);
                Assert.DoesNotContain("Foreslått synlig", tekst);
            }
            else
            {
                Assert.Contains(sti == "/forslag/ny" ? "Send inn" : "finnes ikke", tekst);
            }
        }
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData("Leser", 200, 403)]
    [InlineData("Bidragsyter", 200, 403)]
    [InlineData("Administrator", 200, 200)]
    public async Task Nye_innganger_haandhever_samme_servergrenser(string? rolle, int lesestatus, int adminstatus)
    {
        using var klient = app.CreateClient();
        if (rolle is not null) klient.DefaultRequestHeaders.Add("X-Test-Rolle", rolle);
        foreach (var (sti, status, innhold) in new[]
        {
            ("/", lesestatus, "<h1>Kalender</h1>"),
            ("/kalender", lesestatus, "<h1>Kalender</h1>"),
            ("/aarshjul", lesestatus, "Fristliste"),
            ("/budsjettaar", lesestatus, "Fristliste"),
            ("/admin/innstillinger", adminstatus, "Dataforvaltning")
        })
        {
            var svar = await klient.GetAsync(sti);
            Assert.Equal(status, (int)svar.StatusCode);
            var tekst = await svar.Content.ReadAsStringAsync();
            if (status == 200) Assert.Contains(innhold, tekst);
            else Assert.DoesNotContain(innhold, tekst);
        }
    }
}
