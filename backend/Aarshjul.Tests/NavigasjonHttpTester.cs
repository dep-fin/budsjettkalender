namespace Aarshjul.Tests;

public sealed class NavigasjonHttpTester(TestApplikasjon app) : IClassFixture<TestApplikasjon>
{
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
