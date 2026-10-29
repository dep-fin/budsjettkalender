using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderperspektivTester;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

// Eksisterende kalendergrense: ekte Kestrel/Blazor og disponibel SQLite, bare syntetisk oppsett.
public sealed class KalenderlisteTester
{
    [Fact]
    public Task Kommende_liste_har_25_per_side_og_alle_53_IDer_en_gang_paa_tvers_av_maaned_og_aar() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var frister = Enumerable.Range(0, 53).Select(i => Frist($"LISTE138 {i:00}", i < 26 ? new(2030, 12, 31) : new(2031, 1, 1))).ToArray();
        frister[25].Datopresisjon = Datopresisjon.Maaned;
        frister[25].Datokvalifikator = Datokvalifikator.Ultimo;
        frister[27].Datopresisjon = Datopresisjon.Maaned;
        frister[27].Datokvalifikator = Datokvalifikator.Primo;
        await Seed(n, frister);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("LISTE138");
        await Bytt(n, "Liste");
        await Expect(Liste(n).GetByRole(AriaRole.Heading, new() { Name = "Kommende frister", Exact = true })).ToBeVisibleAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await Expect(Forrige(n)).ToBeDisabledAsync();
        var alle = new List<string>();
        for (var side = 1; side <= 3; side++)
        {
            await Expect(Sidestatus(n)).ToHaveTextAsync($"Side {side} av 3 · 53 frister");
            var forventet = frister.Skip((side - 1) * 25).Take(25).Select(f => f.Id.ToString()).ToArray();
            await Expect(Rader(n)).ToHaveCountAsync(side == 3 ? 3 : 25);
            var ider = await Ider(n);
            Assert.Equal(forventet, ider);
            alle.AddRange(ider);
            if (side < 3) await Neste(n).ClickAsync();
        }
        Assert.Equal(53, alle.Distinct().Count());
        await Expect(Neste(n)).ToBeDisabledAsync();
        await Forrige(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        Assert.Equal(frister.Skip(25).Take(25).Select(f => f.Id.ToString()), await Ider(n));
        await Bytt(n, "Måned");
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
        await Bytt(n, "Liste");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Historikk_har_egen_side_seneste_foerst_og_bevarer_filtre_detaljer_fokus_og_kalenderkontekst() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var kommende = Enumerable.Range(0, 53).Select(i => Frist($"HIST138 kommende {i:00}", new(2030, 1, 1))).ToArray();
        var tidligere = Enumerable.Range(0, 28).Select(i => Frist($"HIST138 tidligere {i:00}", i < 26 ? new(2029, 12, 31) : new(2029, 12, 30))).ToArray();
        foreach (var f in kommende.Concat(tidligere)) f.Budsjettaar = f.Tittel.EndsWith('0') ? 2026 : 2028;
        await Seed(n, kommende.Concat(tidligere).ToArray());
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2028-02");
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 29. februar 2028", Exact = true }).ClickAsync();
        await Bytt(n, "Uke");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("HIST138");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await n.Side.GetByLabel("2026", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("2028", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("Gul bok", new() { Exact = true }).UncheckAsync();
        await Bytt(n, "Liste");
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 3 av 3 · 53 frister");
        await Historikk(n).ClickAsync();
        await Expect(Liste(n).GetByRole(AriaRole.Heading, new() { Name = "Tidligere hendelser", Exact = true })).ToBeVisibleAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 2 · 28 frister");
        Assert.Equal(tidligere.Take(25).Select(f => f.Id.ToString()), await Ider(n));
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 28 frister");
        Assert.Equal(tidligere.Skip(25).Select(f => f.Id.ToString()), await Ider(n));
        var fristknapp = Rader(n).First;
        await fristknapp.PressAsync("Enter");
        await Expect(n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(fristknapp).ToBeFocusedAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 28 frister");
        await Kommende(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 3 av 3 · 53 frister");
        await Historikk(n).PressAsync("Enter");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 28 frister");
        await Bytt(n, "Uke");
        await Expect(Utvalg(n)).ToContainTextAsync("28. februar 2028–5. mars 2028");
        await Expect(Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg 29. februar 2028", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Bytt(n, "Liste");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 28 frister");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("HIST138 tidligere 27");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 1 · 1 frist");
        await Kommende(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre", Exact = true }).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await n.Side.GetByRole(AriaRole.Group, new() { Name = "Kategori", Exact = true }).GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        await Expect(Neste(n)).ToBeDisabledAsync();
        n.BekreftIngenKretsfeil();
    }, kontrollertEpost: true);

    [Fact]
    public Task Sidenavigasjon_laster_ferske_data_og_starter_paa_side_en_ved_endret_ordning_eller_synlighet() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var frister = Enumerable.Range(0, 53).Select(i => Frist($"FERSK138 {i:00}", new(2030, 1, 1))).ToArray();
        var skjult = Frist("FERSK138 skjult POL", new(2030, 1, 1), "POL");
        var upublisert = Frist("FERSK138 upublisert", new(2030, 1, 1));
        upublisert.Status = FristStatus.Forslag;
        await Seed(n, frister.Concat([skjult, upublisert]).ToArray());
        await n.LoggInn("demo-fa");
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("FERSK138");
        await Bytt(n, "Liste");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await using (var db = Database(n))
        {
            var frist = await db.Frister.SingleAsync(f => f.Id == frister[0].Id);
            frist.Tittel = "FERSK138 sist";
            await db.SaveChangesAsync();
        }
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await Expect(Liste(n).GetByRole(AriaRole.Status).Filter(new() { HasText = "Utvalget er oppdatert" })).ToBeVisibleAsync();
        Assert.Equal(frister.Skip(1).Take(25).Select(f => f.Id.ToString()), await Ider(n));
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        await using (var db = Database(n))
        {
            db.FristSynlighet.RemoveRange(await db.FristSynlighet.Where(s => s.FristId == frister[2].Id).ToArrayAsync());
            db.FristSynlighet.Add(new() { FristId = frister[2].Id, GruppeKode = "POL" });
            await db.SaveChangesAsync();
        }
        await Forrige(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 52 frister");
        var dom = await n.Side.ContentAsync();
        foreach (var hemmelig in new[] { skjult.Tittel, skjult.Id.ToString(), upublisert.Tittel, upublisert.Id.ToString(), frister[2].Tittel, frister[2].Id.ToString() })
            Assert.DoesNotContain(hemmelig, dom, StringComparison.OrdinalIgnoreCase);
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 0 av 0 · 0 frister");
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(390)]
    public Task Tastaturblaing_annonserer_ny_side_og_bevarer_kilde_detaljer_og_fokusretur(int bredde) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var frister = Enumerable.Range(0, 27).Select(i => Frist($"BILDE138 Frist med en fullstendig og lesbar tittel {i:00}", new(2030, 11, 20))).ToArray();
        await Seed(n, frister);
        await using (var db = Database(n))
        {
            var kilde = new BehandletDokument { Id = Guid.NewGuid(), Kilde = "syntetisk", DokumentNokkel = "BILDE138", Url = "https://www.regjeringen.no/no/liste138" };
            db.BehandledeDokumenter.Add(kilde);
            var frist = await db.Frister.SingleAsync(f => f.Id == frister[25].Id);
            frist.DokumentId = kilde.Id;
            frist.Notat = "Hele notatet fra en side i kommende frister.";
            await db.SaveChangesAsync();
        }
        await n.Gaa("/kalender");
        await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("BILDE138");
        await Bytt(n, "Liste");
        await Neste(n).PressAsync("Enter");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 27 frister");
        await Expect(Liste(n).GetByRole(AriaRole.Heading, new() { Name = "Kommende frister", Exact = true })).ToBeFocusedAsync();
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await n.Side.EvaluateAsync("window.scrollTo(0, 0)");
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"liste138-kommende-{bredde}.png"), FullPage = true });
        var knapp = Rader(n).First;
        await knapp.PressAsync("Enter");
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = frister[25].Tittel, Exact = true });
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
        await Expect(dialog).ToContainTextAsync("Hele notatet fra en side i kommende frister.");
        await Expect(dialog.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") })).ToHaveAttributeAsync("href", "https://www.regjeringen.no/no/liste138");
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"liste138-detaljer-{bredde}.png"), FullPage = false });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
        await Expect(knapp).ToBeFocusedAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 2 · 27 frister");
        await knapp.PressAsync("Space");
        await Expect(dialog).ToBeVisibleAsync();
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(knapp).ToBeFocusedAsync();
        await Forrige(n).PressAsync("Space");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 2 · 27 frister");
        await Expect(Liste(n).GetByRole(AriaRole.Heading, new() { Name = "Kommende frister", Exact = true })).ToBeFocusedAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tentative_maaneder_bevarer_alle_kvalifikatorer_og_dagens_tider_med_stabil_sortering() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await SettTid(n.Side, "2030-01-25T18:00");
        await n.LoggInn("demo-admin"); // Ny cookie etter den store kontrollerte tidsreisen.
        Frist[] Maaned(string navn, DateOnly dato) => new Datokvalifikator?[] { null, Datokvalifikator.Primo, Datokvalifikator.Medio, Datokvalifikator.Ultimo }
            .Select((k, i) => { var f = Frist($"PERIODE138 {navn} {(char)('A' + i)}", dato); f.Datopresisjon = Datopresisjon.Maaned; f.Datokvalifikator = k; return f; }).ToArray();
        var forrige = Maaned("forrige", new(2029, 12, 20));
        var denne = Maaned("denne", new(2030, 1, 20));
        var neste = Maaned("neste", new(2030, 2, 20));
        var dag = new[] { Frist("PERIODE138 dag A heldag", new(2030, 1, 25)), Frist("PERIODE138 dag B tidlig", new(2030, 1, 25)), Frist("PERIODE138 dag C sent", new(2030, 1, 25)), Frist("PERIODE138 dag D lik", new(2030, 1, 25)), Frist("PERIODE138 dag D lik", new(2030, 1, 25)) };
        dag[1].Klokkeslett = new(0, 5); dag[2].Klokkeslett = new(18, 0);
        dag[3].Klokkeslett = dag[4].Klokkeslett = new(18, 0);
        dag[3].Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        dag[4].Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var gammel = new[] { Frist("PERIODE138 gammel A heldag", new(2030, 1, 24)), Frist("PERIODE138 gammel B tidlig", new(2030, 1, 24)), Frist("PERIODE138 gammel C sent", new(2030, 1, 24)) };
        gammel[1].Klokkeslett = new(0, 5); gammel[2].Klokkeslett = new(23, 55);
        await Seed(n, forrige.Concat(denne).Concat(neste).Concat(dag.Reverse()).Concat(gammel).ToArray());
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("PERIODE138");
        await Bytt(n, "Liste");
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 1 · 13 frister");
        var ordnet = denne.Take(3).Concat(dag).Concat(denne.Skip(3)).Concat(neste).Select(f => f.Id.ToString());
        Assert.Equal(ordnet, await Ider(n));
        foreach (var f in denne.Concat(neste))
        {
            var rad = Rader(n).Filter(new() { Has = n.Side.GetByText(f.Tittel, new() { Exact = true }) });
            await Expect(rad).ToContainTextAsync("Dato ikke fastsatt");
            await Expect(rad).Not.ToContainTextAsync("20.");
            await Expect(rad).Not.ToContainTextAsync("Heldag");
            await rad.PressAsync("Enter");
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = f.Tittel, Exact = true });
            await Expect(dialog).ToContainTextAsync("Tentativ måned – dato ikke fastsatt");
            await Expect(dialog).Not.ToContainTextAsync("20.");
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(rad).ToBeFocusedAsync();
        }
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.EvaluateAsync("window.scrollTo(0, 0)");
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"liste138-tentative-{bredde}.png"), FullPage = true });
        }
        await Historikk(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 1 · 7 frister");
        Assert.Equal(new[] { gammel[0], gammel[2], gammel[1], forrige[3], forrige[2], forrige[0], forrige[1] }.Select(f => f.Id.ToString()), await Ider(n));
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.EvaluateAsync("window.scrollTo(0, 0)");
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"liste138-historikk-{bredde}.png"), FullPage = true });
        }
        n.BekreftIngenKretsfeil();
    }, kontrollertEpost: true);

    [Fact]
    public Task Oslo_midnatt_nullstiller_siden_selv_naar_de_ordnede_IDene_er_uendret() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await SettTid(n.Side, "2030-01-31T22:59");
        await n.LoggInn("demo-admin");
        await Seed(n, Enumerable.Range(0, 53).Select(i => Frist($"MIDNATT138 {i:00}", new(2030, 2, 15))).ToArray());
        await n.Gaa("/kalender");
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("MIDNATT138");
        await Bytt(n, "Liste");
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 2 av 3 · 53 frister");
        var tidsside = await n.Side.Context.NewPageAsync();
        try { await SettTid(tidsside, "2030-01-31T23:01"); }
        finally { await tidsside.CloseAsync(); }
        await Neste(n).ClickAsync();
        await Expect(Sidestatus(n)).ToHaveTextAsync("Side 1 av 3 · 53 frister");
        await Expect(Liste(n)).ToContainTextAsync("Fra og med 1. februar 2030.");
        await Expect(Liste(n).GetByRole(AriaRole.Status).Filter(new() { HasText = "Utvalget er oppdatert" })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    }, kontrollertEpost: true);

    private static async Task SettTid(IPage side, string tid)
    {
        await side.GotoAsync("/demo/epostkjoring");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await side.GetByLabel("Kontrollert UTC-tid", new() { Exact = true }).FillAsync(tid);
        await side.GetByRole(AriaRole.Button, new() { Name = "Sett kontrollert tid" }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("Kontrollert tid er satt");
    }

    private static ILocator Historikk(Nettleser n) => Liste(n).GetByRole(AriaRole.Button, new() { Name = "Vis tidligere hendelser", Exact = true });
    private static ILocator Kommende(Nettleser n) => Liste(n).GetByRole(AriaRole.Button, new() { Name = "Tilbake til kommende frister", Exact = true });

    private static Frist Frist(string tittel, DateOnly dato, string gruppe = "FA") => new()
    {
        Id = Guid.NewGuid(), Tittel = tittel, Dato = dato, Budsjettaar = 2031,
        Kategori = Kategori.Budsjett, Synlighet = [new() { GruppeKode = gruppe }]
    };

    private static async Task Seed(Nettleser n, params Frist[] frister)
    {
        await using var db = Database(n);
        db.Frister.AddRange(frister);
        await db.SaveChangesAsync();
    }

    private static AppDbContext Database(Nettleser n) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options);

    private static ILocator Rader(Nettleser n) => Liste(n).Locator(".kalenderliste [data-frist-id]");
    private static Task<string[]> Ider(Nettleser n) => Rader(n).EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.fristId)");
    private static ILocator Sidestatus(Nettleser n) => Liste(n).GetByRole(AriaRole.Navigation, new() { Name = "Sider med frister", Exact = true }).GetByRole(AriaRole.Status);
    private static ILocator Neste(Nettleser n) => Liste(n).GetByRole(AriaRole.Button, new() { Name = "Neste side", Exact = true });
    private static ILocator Forrige(Nettleser n) => Liste(n).GetByRole(AriaRole.Button, new() { Name = "Forrige side", Exact = true });
}
