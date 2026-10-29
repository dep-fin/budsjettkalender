using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderperspektivTester;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

// Avtalt grense i #139: faktisk Kalender/Blazor, Kestrel og disponibel SQLite.
public sealed class KalendersoekTester
{
    [Fact]
    public Task Alle_datoer_finner_tittel_og_notat_i_andre_maaneder_aar_og_historikk_med_eksplisitt_returtur() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var gammel = Frist("SØK139 historisk tittel", new(2020, 3, 14));
        var neste = Frist("Frist med notat", new(2031, 2, 3));
        neste.Notat = "Notatet inneholder søk139 og mer informasjon.";
        await Seed(n, gammel, neste, Frist("Annen frist", new(2030, 11, 20)));
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Bytt(n, "Uke");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("  sØk139  ");
        await Expect(Ukekalender(n)).Not.ToContainTextAsync(gammel.Tittel);
        await n.Side.GetByRole(AriaRole.Searchbox).PressAsync("Enter");
        await Expect(Treff(n)).ToContainTextAsync("Alle datoer, inkludert tidligere frister");
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        await Expect(Rader(n)).ToHaveCountAsync(2);
        await Expect(Rader(n).First).ToContainTextAsync("Tidligere frist");
        await Expect(Rader(n).First).ToContainTextAsync("14. mars 2020");
        await Expect(Rader(n).Last).ToContainTextAsync("3. februar 2031");
        await Expect(Treff(n).GetByRole(AriaRole.Heading, new() { Name = "Søketreff", Exact = true })).ToBeFocusedAsync();
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Neste måned", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-12");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til kalenderen", Exact = true }).ClickAsync();
        await Expect(Utvalg(n)).ToContainTextAsync("Uke 44, 2030");
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("  sØk139  ");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til søketreff", Exact = true }).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(390)]
    public Task Treffside_detaljer_og_kalendernavigasjon_bevarer_soek_og_tentativ_presisjon_og_fokus(int bredde) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var frister = Enumerable.Range(0, 53).Select(i => Frist($"SIDE139 frist {i:00}", i < 26 ? new(2020, 3, 14) : new(2032, 2, 20))).ToArray();
        frister[26].Datopresisjon = Datopresisjon.Maaned;
        frister[26].Datokvalifikator = Datokvalifikator.Medio;
        frister[26].ManuellKildeUrl = "https://www.regjeringen.no/no/soek139";
        frister[26].Notat = "Hele notatet fra søketreffet.";
        await Seed(n, frister);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Bytt(n, "Liste");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("SIDE139");
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Søk i alle datoer", Exact = true }).PressAsync("Enter");
        await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        var alle = new List<string>();
        for (var side = 1; side <= 3; side++)
        {
            await Expect(Status(n)).ToHaveTextAsync($"Side {side} av 3 · 53 frister");
            alle.AddRange(await Treff(n).Locator(".listefrist").EvaluateAllAsync<string[]>("r => r.map(x => x.dataset.fristId)"));
            if (side < 3) await Neste(n).PressAsync("Space");
        }
        Assert.Equal(frister.Select(f => f.Id.ToString()), alle);
        await Treff(n).GetByRole(AriaRole.Button, new() { Name = "Forrige side", Exact = true }).PressAsync("Enter");
        await Expect(Status(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await Expect(Treff(n).GetByRole(AriaRole.Heading, new() { Name = "Søketreff", Exact = true })).ToBeFocusedAsync();
        var rad = Rader(n).Filter(new() { HasText = frister[26].Tittel });
        await Expect(rad).ToContainTextAsync("medio februar 2032");
        await Expect(rad).ToContainTextAsync("Dato ikke fastsatt");
        await Expect(rad).Not.ToContainTextAsync("20.");
        await Expect(rad).Not.ToContainTextAsync("Tidligere frist");
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"soeketreff139-{bredde}.png"), FullPage = true });
        var aapne = rad.GetByRole(AriaRole.Button, new() { Name = frister[26].Tittel, Exact = true });
        await aapne.PressAsync("Enter");
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = frister[26].Tittel, Exact = true });
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
        await Expect(dialog).ToContainTextAsync("Hele notatet fra søketreffet.");
        await Expect(dialog.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") })).ToHaveAttributeAsync("href", "https://www.regjeringen.no/no/soek139");
        await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToBeVisibleAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(aapne).ToBeFocusedAsync();
        await aapne.PressAsync("Space");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Vis i kalenderen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2032-02");
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Kalender", Exact = true })).ToBeFocusedAsync();
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Region, new() { Name = "Tentative frister i februar 2032", Exact = true })).ToContainTextAsync(frister[26].Tittel);
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("SIDE139");
        await Expect(n.Side.GetByLabel("Gul bok", new() { Exact = true })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til søketreff", Exact = true }).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Tilbake til kalenderen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2032-02");
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Kalender", Exact = true })).ToBeFocusedAsync();
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("demo-fa")]
    [InlineData("demo-finfag")]
    [InlineData("demo-fag")]
    public Task Treff_aar_tellinger_og_tomme_utvalg_folger_lesetilgang_og_eksplisitte_filtre(string persona) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var gruppe = persona == "demo-fa" ? "FA" : persona == "demo-finfag" ? "FIN-FAG" : "FAG";
        var synlig = Frist("UTVALG139 synlig", new(2020, 1, 1), gruppe);
        var annetAar = Frist("UTVALG139 annet år", new(2033, 2, 1), gruppe); annetAar.Budsjettaar = 2033;
        var regnskap = Frist("UTVALG139 regnskap", new(2032, 1, 1), gruppe); regnskap.Kategori = Kategori.Regnskap;
        var pol = Frist("UTVALG139 politisk hemmelig", new(2030, 1, 1), "POL"); pol.Budsjettaar = 2097;
        var annen = Frist("UTVALG139 annen gruppe hemmelig", new(2030, 1, 1), gruppe == "FA" ? "FAG" : "FA"); annen.Budsjettaar = 2098;
        var forslag = Frist("UTVALG139 upublisert hemmelig", new(2030, 1, 1), gruppe); forslag.Status = FristStatus.Forslag; forslag.Budsjettaar = 2099;
        await Seed(n, synlig, annetAar, regnskap, pol, annen, forslag);
        await n.LoggInn(persona);
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("UTVALG139");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Søk i alle datoer", Exact = true }).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        var dom = await n.Side.ContentAsync();
        foreach (var skjult in new[] { pol, annen, forslag })
        {
            Assert.DoesNotContain(skjult.Tittel, dom);
            Assert.DoesNotContain(skjult.Id.ToString(), dom);
            await Expect(n.Side.GetByLabel(skjult.Budsjettaar.ToString(), new() { Exact = true })).ToHaveCountAsync(0);
        }
        await n.Side.GetByLabel("2033", new() { Exact = true }).UncheckAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        await n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true }).GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        await Expect(Treff(n)).ToContainTextAsync("Alle datoer er søkt, inkludert tidligere frister.");
        await Treff(n).GetByRole(AriaRole.Button, new() { Name = "Endre utvalg", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Complementary, new() { Name = "Fristutvalg", Exact = true })).ToBeFocusedAsync();
        await Treff(n).GetByRole(AriaRole.Button, new() { Name = "Tøm søket", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("");
        await Expect(Status(n)).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("UTVALG139");
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 1 · 2 frister");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Sidenavigasjon_oppdaterer_ordning_og_bortfalt_tilgang_uten_gamle_treff() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var frister = Enumerable.Range(0, 53).Select(i => Frist($"FERSK139 {i:00}", new(2030, 1, 1))).ToArray();
        await Seed(n, frister);
        await n.LoggInn("demo-fa");
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("FERSK139");
        await n.Side.GetByRole(AriaRole.Searchbox).PressAsync("Enter");
        await Neste(n).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await using (var db = Database(n))
        {
            var f = await db.Frister.SingleAsync(f => f.Id == frister[0].Id); f.Tittel = "FERSK139 sist";
            await db.SaveChangesAsync();
        }
        await Neste(n).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await Expect(Treff(n)).ToContainTextAsync("Utvalget er oppdatert");
        await using (var db = Database(n))
        {
            db.FristSynlighet.RemoveRange(await db.FristSynlighet.Where(s => s.FristId == frister[1].Id).ToArrayAsync());
            db.FristSynlighet.Add(new() { FristId = frister[1].Id, GruppeKode = "POL" });
            await db.SaveChangesAsync();
        }
        await Treff(n).GetByRole(AriaRole.Button, new() { Name = frister[1].Tittel, Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Frist utilgjengelig", Exact = true })).ToContainTextAsync("Fristen er ikke lenger tilgjengelig.");
        await Expect(Status(n)).ToHaveTextAsync("Side 1 av 3 · 52 frister");
        Assert.DoesNotContain(frister[1].Tittel, await n.Side.ContentAsync());
        await n.Side.Keyboard.PressAsync("Escape");
        await Neste(n).ClickAsync();
        await Expect(Status(n)).ToHaveTextAsync("Side 2 av 3 · 52 frister");
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Neste(Nettleser n) => Treff(n).GetByRole(AriaRole.Button, new() { Name = "Neste side", Exact = true });

    private static Frist Frist(string tittel, DateOnly dato, string gruppe = "FA") => new()
    {
        Id = Guid.NewGuid(), Tittel = tittel, Dato = dato, Budsjettaar = 2031,
        Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = gruppe }]
    };

    private static AppDbContext Database(Nettleser n) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options);

    private static async Task Seed(Nettleser n, params Frist[] frister)
    {
        await using var db = Database(n);
        db.Frister.AddRange(frister);
        await db.SaveChangesAsync();
    }

    private static ILocator Treff(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Søketreff", Exact = true });
    private static ILocator Rader(Nettleser n) => Treff(n).Locator(".kalenderliste > li");
    private static ILocator Status(Nettleser n) => Treff(n).GetByRole(AriaRole.Navigation, new() { Name = "Sider med frister", Exact = true }).GetByRole(AriaRole.Status);
}
