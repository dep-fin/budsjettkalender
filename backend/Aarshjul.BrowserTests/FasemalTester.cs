using System.Text.Json;
using Aarshjul.Application.Faser;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Faser;
using Aarshjul.Testkontrakter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class FasemalTester
{
    private const string Editor = "/admin/innstillinger/faseinndeling";
    private static readonly string[] Navn = ["Tidlig plan", "Nytt forslag", "Vedtak", "Iverksetting", "Avslutning"];

    [Fact]
    public Task Administrator_aapner_lagrer_og_alle_roller_leser_felles_mal_etter_ny_sesjon_og_ekte_omstart() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        await using (var db = fabrikk.CreateDbContext())
        {
            foreach (var aar in new[] { 2030, 2031 })
                db.Frister.Add(new() { Id = Guid.NewGuid(), Tittel = "UX83 felles år " + aar, Dato = new(aar - 1, 11, 20), Budsjettaar = aar,
                    Synlighet = [new() { GruppeKode = "FA" }, new() { GruppeKode = "FIN-FAG" }, new() { GruppeKode = "FAG" }] });
            await db.SaveChangesAsync();
        }
        var historikk = await FasedataBevis.HentAsync(fabrikk);
        var standard = await HentMal(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/innstillinger");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Faseinndeling", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Faseinndeling", Exact = true })).ToBeVisibleAsync();
        foreach (var (navn, i) in new[] { "Forberedelser", "Utarbeidelse av budsjettforslag", "Fremleggelse og vedtak", "Gjennomføring og revidering", "Regnskap" }.Select((v, i) => (v, i)))
            await Expect(n.Side.GetByLabel($"Fase {i + 1} navn", new() { Exact = true })).ToHaveValueAsync(navn);
        foreach (var (maaned, aar, i) in new[] { (10, -2, 1), (3, -1, 2), (10, -1, 3), (1, 0, 4), (1, 1, 5) })
        {
            await Expect(n.Side.GetByLabel($"Fase {i} startmåned", new() { Exact = true })).ToHaveValueAsync(maaned.ToString());
            await Expect(n.Side.GetByLabel($"Fase {i} relativt år", new() { Exact = true })).ToHaveValueAsync(aar.ToString());
        }
        await Expect(n.Side.GetByLabel("Siste inkluderte måned", new() { Exact = true })).ToHaveValueAsync("4");
        await Expect(n.Side.GetByLabel("Slutt relativt år", new() { Exact = true })).ToHaveValueAsync("1");
        await FyllEndret(n.Side);
        await Lagre(n.Side).EvaluateAsync("el => { el.click(); el.click(); }");
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToHaveTextAsync("Fasemalen er lagret for alle budsjettår. Ingen frister er endret.");
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToBeFocusedAsync();
        var lagret = await HentMal(n);
        Assert.NotEqual(standard.Versjon, lagret.Versjon);
        Assert.Equal(Navn, lagret.Faser.Select(f => f.Navn));
        Assert.Equal(new[] { -16, -9, -2, 1, 13 }, lagret.Faser.Select(f => f.Startmaaned));
        Assert.Equal(17, lagret.SisteMaanedInkludert);
        Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        foreach (var persona in new[] { "demo-admin", "demo-fag", "demo-fa", "demo-finfag" })
        {
            await n.LoggInn(persona);
            await KontrollerAar(n, 2030);
            await KontrollerAar(n, 2031);
            await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Rediger faser", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre fasemal", Exact = true })).ToHaveCountAsync(0);
        }
        await n.LoggInn("demo-admin");
        await n.Gaa(Editor);
        await Expect(n.Side.GetByLabel("Fase 1 navn", new() { Exact = true })).ToHaveValueAsync("Tidlig plan");
        Assert.Equal(MalJson(lagret), MalJson(await HentMal(n)));
        // Faktisk stopp/start av bare dette scenarioets Kestrel, med samme SQLite-fil.
        await n.OmstartAsync();
        await n.LoggInn("demo-fag");
        await KontrollerAar(n, 2030);
        await KontrollerAar(n, 2031);
        Assert.Equal(MalJson(lagret), MalJson(await HentMal(n)));
        Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Gammel_adminfane_beholder_verdier_ved_konflikt_og_krever_eksplisitt_ny_innlasting() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var historikk = await FasedataBevis.HentAsync(fabrikk);
        await n.LoggInn("demo-admin");
        await n.Gaa(Editor);
        var forste = n.Side;
        await n.LoggInn("demo-admin");
        await n.Gaa(Editor);
        var gammel = n.Side;
        await gammel.GetByLabel("Fase 1 navn", new() { Exact = true }).FillAsync("Min gamle fane");
        await gammel.GetByLabel("Siste inkluderte måned", new() { Exact = true }).SelectOptionAsync("8");
        await FyllEndret(forste);
        await Lagre(forste).ClickAsync();
        await Expect(forste.GetByRole(AriaRole.Status)).ToContainTextAsync("Fasemalen er lagret");
        var vinner = await HentMal(n);
        await Lagre(gammel).PressAsync("Enter");
        await Expect(gammel.GetByRole(AriaRole.Alert)).ToContainTextAsync("nyere fasemal");
        await Expect(gammel.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(gammel.GetByLabel("Fase 1 navn", new() { Exact = true })).ToHaveValueAsync("Min gamle fane");
        await Expect(gammel.GetByLabel("Siste inkluderte måned", new() { Exact = true })).ToHaveValueAsync("8");
        await Expect(Lagre(gammel)).ToBeDisabledAsync();
        Assert.Equal(MalJson(vinner), MalJson(await HentMal(n)));
        Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        await gammel.GetByRole(AriaRole.Button, new() { Name = "Last inn nyeste fasemal", Exact = true }).PressAsync("Enter");
        await Expect(gammel.GetByRole(AriaRole.Status)).ToContainTextAsync("Nyeste fasemal er lastet inn");
        await Expect(gammel.GetByRole(AriaRole.Status)).ToBeFocusedAsync();
        await Expect(gammel.GetByLabel("Fase 1 navn", new() { Exact = true })).ToHaveValueAsync("Tidlig plan");
        await Expect(gammel.GetByLabel("Siste inkluderte måned", new() { Exact = true })).ToHaveValueAsync("6");
        await gammel.GetByLabel("Fase 1 navn", new() { Exact = true }).FillAsync("Bevisst etter ny innlasting");
        await Lagre(gammel).PressAsync("Enter");
        await Expect(gammel.GetByRole(AriaRole.Status)).ToContainTextAsync("Fasemalen er lagret");
        Assert.NotEqual(vinner.Versjon, (await HentMal(n)).Versjon);
        Assert.Equal("Bevisst etter ny innlasting", (await HentMal(n)).Faser[0].Navn);
        Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ugyldige_navn_grenser_og_slutt_beholder_feltene_hele_datagrunnlaget_og_versjonen() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var historikk = await FasedataBevis.HentAsync(fabrikk);
        var foer = await HentMal(n);
        await n.LoggInn("demo-admin");
        foreach (var feil in new[] { "tomt navn", "like grenser", "synkende grenser", "slutt foer start", "tomt aar", "overloep" })
        {
            await n.Gaa(Editor);
            await Expect(n.Side.GetByLabel("Fase 1 navn", new() { Exact = true })).ToHaveValueAsync("Forberedelser");
            await n.Side.GetByLabel("Fase 3 navn", new() { Exact = true }).FillAsync("Ulagret navn " + feil);
            switch (feil)
            {
                case "tomt navn": await n.Side.GetByLabel("Fase 1 navn", new() { Exact = true }).FillAsync(""); break;
                case "like grenser":
                case "synkende grenser":
                    await n.Side.GetByLabel("Fase 2 startmåned", new() { Exact = true }).SelectOptionAsync(feil == "like grenser" ? "10" : "9");
                    await n.Side.GetByLabel("Fase 2 relativt år", new() { Exact = true }).FillAsync("-2"); break;
                case "slutt foer start": await n.Side.GetByLabel("Slutt relativt år", new() { Exact = true }).FillAsync("0"); break;
                case "tomt aar": await n.Side.GetByLabel("Fase 2 relativt år", new() { Exact = true }).FillAsync(""); break;
                case "overloep": await n.Side.GetByLabel("Slutt relativt år", new() { Exact = true }).FillAsync("2147483647"); break;
            }
            await Lagre(n.Side).PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Fasemalen ble ikke lagret");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
            await Expect(n.Side.GetByLabel("Fase 3 navn", new() { Exact = true })).ToHaveValueAsync("Ulagret navn " + feil);
            if (feil == "tomt navn") await Expect(n.Side.GetByLabel("Fase 1 navn", new() { Exact = true })).ToHaveValueAsync("");
            Assert.Equal(MalJson(foer), MalJson(await HentMal(n)));
            Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        }
        await n.Side.SetViewportSizeAsync(390, 844);
        Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Smalt_fasevindu_beholder_alle_fristidentiteter_og_utenforliggende_frister_i_begge_visninger() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var historikk = await FasedataBevis.HentAsync(fabrikk);
        await n.LoggInn("demo-admin");
        var frister = (await n.Frister()).Where(f => (int)f!["budsjettaar"]! == 2030).Select(f => (string)f!["id"]!).Order().ToArray();
        await n.Gaa(Editor);
        for (var i = 1; i <= 5; i++)
        {
            await n.Side.GetByLabel($"Fase {i} startmåned", new() { Exact = true }).SelectOptionAsync(i.ToString());
            await n.Side.GetByLabel($"Fase {i} relativt år", new() { Exact = true }).FillAsync("0");
        }
        await n.Side.GetByLabel("Siste inkluderte måned", new() { Exact = true }).SelectOptionAsync("5");
        await n.Side.GetByLabel("Slutt relativt år", new() { Exact = true }).FillAsync("0");
        await Lagre(n.Side).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Fasemalen er lagret");
        await n.Gaa("/budsjettaar");
        await n.Side.GetByLabel("Velg budsjettår", new() { Exact = true }).SelectOptionAsync("2030");
        var tidslinje = n.Side.GetByRole(AriaRole.Region, new() { Name = "Tidslinje for budsjettår 2030", Exact = true });
        await Expect(tidslinje.GetByRole(AriaRole.Region, new() { Name = "Frister før tidslinjen", Exact = true })).ToContainTextAsync("UX83 komplett frist 0");
        await Expect(tidslinje.GetByRole(AriaRole.Region, new() { Name = "Frister etter tidslinjen", Exact = true })).ToBeVisibleAsync();
        foreach (var visning in new[] { "Tidslinje", "Fristliste" })
        {
            var knapp = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Budsjettårvisning", Exact = true }).GetByRole(AriaRole.Button, new() { Name = visning, Exact = true });
            await knapp.ClickAsync();
            await Expect(knapp).ToHaveAttributeAsync("aria-pressed", "true");
            var flate = n.Side.GetByRole(AriaRole.Region, new() { Name = (visning == "Tidslinje" ? "Tidslinje for budsjettår " : "Fristliste for budsjettår ") + "2030", Exact = true });
            await Expect(flate.Locator("button[data-frist-id]")).ToHaveCountAsync(frister.Length);
            Assert.Equal(frister, (await flate.Locator("button[data-frist-id]").EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.fristId)")).Order());
            await flate.GetByRole(AriaRole.Button, new() { Name = "UX83 komplett frist 3", Exact = true }).PressAsync("Enter");
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "UX83 komplett frist 3", Exact = true });
            await Expect(dialog).ToContainTextAsync("ultimo mai 2030");
            await Expect(dialog).ToContainTextAsync("Bevar hele innholdet 3");
            await Expect(dialog.GetByRole(AriaRole.Link, new() { NameRegex = new("Les rundskrivet") })).ToBeVisibleAsync();
            await n.Side.Keyboard.PressAsync("Escape");
        }
        Assert.Equal(historikk, await FasedataBevis.HentAsync(fabrikk));
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Lagre(IPage side) => side.GetByRole(AriaRole.Button, new() { Name = "Lagre fasemal", Exact = true });
    private static async Task FyllEndret(IPage side)
    {
        foreach (var (maaned, aar, i) in new[] { (9, -2, 1), (4, -1, 2), (11, -1, 3), (2, 0, 4), (2, 1, 5) })
        {
            await side.GetByLabel($"Fase {i} navn", new() { Exact = true }).FillAsync(Navn[i - 1]);
            await side.GetByLabel($"Fase {i} startmåned", new() { Exact = true }).SelectOptionAsync(maaned.ToString());
            await side.GetByLabel($"Fase {i} relativt år", new() { Exact = true }).FillAsync(aar.ToString());
        }
        await side.GetByLabel("Siste inkluderte måned", new() { Exact = true }).SelectOptionAsync("6");
        await side.GetByLabel("Slutt relativt år", new() { Exact = true }).FillAsync("1");
    }
    private static async Task KontrollerAar(Nettleser n, int aar)
    {
        await n.Gaa("/budsjettaar");
        await n.Side.GetByLabel("Velg budsjettår", new() { Exact = true }).SelectOptionAsync(aar.ToString());
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Tidslinje for budsjettår " + aar, Exact = true })).ToBeVisibleAsync();
        var faser = n.Side.Locator("[aria-label='Budsjettårets fem faser']");
        await Expect(faser.Locator("strong")).ToHaveTextAsync(Navn);
        await Expect(faser).ToContainTextAsync("sep t−2 – mar t−1");
        await Expect(faser).ToContainTextAsync("feb t+1 – jun t+1");
        await Expect(n.Side.Locator("main")).ToContainTextAsync($"september {aar - 2} – juni {aar + 1}");
    }
    private static IDbContextFactory<AppDbContext> Fabrikk(Nettleser n)
        => new Fasefabrikk(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options);
    private static Task<FasemalDto> HentMal(Nettleser n) => new FasemalTjeneste(Fabrikk(n)).HentAsync();
    private static string MalJson(FasemalDto mal) => JsonSerializer.Serialize(mal);
    private sealed class Fasefabrikk(DbContextOptions<AppDbContext> alternativer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(alternativer);
    }
}
