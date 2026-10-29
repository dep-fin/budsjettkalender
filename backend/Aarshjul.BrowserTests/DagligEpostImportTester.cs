using System.Text.Json;
using System.Text;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed partial class DagligEpostTester
{
    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV07_faktisk_fristfil_importerer_ny_endring_og_sletting_med_trygg_kalenderlenke_avbryt_og_feilretry(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-fag"); await MeldPaa(n, true, true); await Bekreft(n);
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T07:00");
            await n.Gaa("/admin/data"); var opprinnelig = await EV07LastNed(n, "opprinnelig.json");
            var rad = new FristEksport { Id = Guid.NewGuid(), Tittel = "EV07 importert", Dato = new(2044, 11, 20), Budsjettaar = 2045,
                Kategori = Kategori.Regnskap, Status = FristStatus.Godkjent, SynligFor = ["FAG"],
                ManuellKildeUrl = "https://regjeringen.no/no/importert140" };
            var pol = rad with { Id = Guid.NewGuid(), Tittel = "EV07 skjult politisk", SynligFor = ["POL"] };
            var fil = opprinnelig with { Frister = opprinnelig.Frister.Concat([rad, pol]).ToArray() };
            await EV07Forhaandsvis(n, fil); await EV07Bekreft(n);
            await Expect(n.Side.Locator("main")).ToContainTextAsync($"Importerte {fil.Frister.Count}");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Ny frist: EV07 importert");
            await Expect(n.Side.GetByRole(AriaRole.Article)).Not.ToContainTextAsync("EV07 skjult politisk");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-02T07:00"); await n.Gaa("/admin/data");
            var faktisk = await EV07LastNed(n, "identisk.json");
            Assert.Equal(rad.ManuellKildeUrl, Assert.Single(faktisk.Frister, f => f.Id == rad.Id).ManuellKildeUrl);
            Assert.Equal(rad.ManuellKildeUrl, (string?)(await n.Frister()).Single(f => (string?)f!["id"] == rad.Id.ToString())!["kildeUrl"]);
            await EV07Forhaandsvis(n, faktisk); await EV07Bekreft(n);
            await Expect(n.Side.Locator("main")).ToContainTextAsync($"Importerte {fil.Frister.Count}");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/data");
            rad = rad with { ManuellKildeUrl = "https://www.regjeringen.no/no/endret140" };
            var lenkefil = faktisk with { Frister = faktisk.Frister.Select(f => f.Id == rad.Id ? f with { ManuellKildeUrl = rad.ManuellKildeUrl } : f).ToArray() };
            await EV07Forhaandsvis(n, lenkefil); await EV07Bekreft(n);
            await Expect(n.Side.Locator("main")).ToContainTextAsync($"Importerte {lenkefil.Frister.Count}");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await n.Gaa("/admin/data");
            Assert.Equal(rad.ManuellKildeUrl, Assert.Single((await EV07LastNed(n, "lenkeendring.json")).Frister, f => f.Id == rad.Id).ManuellKildeUrl);
            rad = rad with { Dato = new(2044, 12, 5) };
            fil = opprinnelig with { Frister = opprinnelig.Frister.Append(rad).ToArray() };
            await n.Side.SetViewportSizeAsync(bredde, 844); await EV07Forhaandsvis(n, fil);
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
            var dialog = n.Side.GetByRole(AriaRole.Dialog);
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeFocusedAsync();
            await n.Side.Keyboard.PressAsync("Enter"); await Expect(dialog).ToBeHiddenAsync();
            Assert.Contains((await EV07LastNed(n, "avbrutt.json")).Frister, f => f.Id == rad.Id && f.Dato == new DateOnly(2044, 11, 20));
            await EV07Forhaandsvis(n, fil);
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER TR_EV07_ui AFTER INSERT ON Frister WHEN NEW.Tittel = 'EV07 importert' BEGIN SELECT RAISE(ABORT, 'EV07 kontrollert feil'); END;");
            await EV07Bekreft(n); await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("beholdt");
            await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("EV07 kontrollert feil");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_EV07_ui;");
            await EV07Bekreft(n); await Expect(n.Side.Locator("main")).ToContainTextAsync($"Importerte {fil.Frister.Count}");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(2);
            var endret = n.Side.GetByRole(AriaRole.Article).Nth(1);
            await Expect(endret).ToContainTextAsync("Vesentlig endring: EV07 importert");
            await Expect(endret).ToContainTextAsync("Tidligere: 20.11.2044"); await Expect(endret).ToContainTextAsync("05.12.2044");
            await Expect(endret).Not.ToContainTextAsync("EV07 skjult politisk");
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-03T07:00"); await n.Gaa("/admin/data");
            await EV07Forhaandsvis(n, opprinnelig); await EV07Bekreft(n);
            await Expect(n.Side.Locator("main")).ToContainTextAsync($"Importerte {opprinnelig.Frister.Count}");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var slettet = n.Side.GetByRole(AriaRole.Article).Nth(2); await Expect(slettet).ToContainTextAsync("Fjernet frist: EV07 importert");
            await n.Side.SetViewportSizeAsync(bredde, 844);
            var lenke = slettet.GetByRole(AriaRole.Link, new() { Name = "Åpne relevant kalenderutvalg (krever innlogging)", Exact = true });
            var href = await lenke.GetAttributeAsync("href"); Assert.NotNull(href);
            Assert.Equal("?aar=2045&kategori=2&dato=2044-12-05&fjernet=1", new Uri(href).Query);
            await lenke.ClickAsync(); await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByText("Fristen er fjernet. Kalenderen viser det relevante utvalget med din vanlige tilgang.", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg" })).ToContainTextAsync("2045");
            await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Aktivt kalenderutvalg" })).ToContainTextAsync("Regnskap");
            await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2044-12");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"ev07-slettingslenke-{bredde}.png"), FullPage = true });
            var anonym = await n.NySide(); await anonym.GotoAsync(href);
            await Expect(anonym.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToHaveCountAsync(0);
        });

    private static async Task<FristDatabase> EV07LastNed(Nettleser n, string navn)
    {
        var nedlasting = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned frister (JSON)", Exact = true }).ClickAsync());
        var sti = Path.Combine(n.Mappe, navn); await nedlasting.SaveAsAsync(sti);
        return JsonSerializer.Deserialize<FristDatabase>(await File.ReadAllTextAsync(sti), Aarshjul.Web.Api.FristEndepunkter.DatabaseJson)!;
    }
    private static async Task EV07Forhaandsvis(Nettleser n, FristDatabase fil)
    {
        await n.Side.GetByLabel("Fil (JSON)", new() { Exact = true }).SetInputFilesAsync(new FilePayload
        { Name = "ev07-frister.json", MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fil, Aarshjul.Web.Api.FristEndepunkter.DatabaseJson)) });
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Importforhåndsvisning", Exact = true })).ToBeVisibleAsync();
    }
    private static async Task EV07Bekreft(Nettleser n)
    {
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        var knapp = n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true });
        await knapp.FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
    }
}
