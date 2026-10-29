using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed partial class DagligEpostTester
{
    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV10_administratoroversikt_annullert_og_bekreftet_stans_omstart_og_ny_paamelding(int bredde)
        => await Trygt(async n =>
        {
            await n.LoggInn("demo-admin"); await SettTid(n, "2030-01-01T06:00");
            await Aktiver(n, "demo-fag");
            await n.LoggInn("demo-admin"); await n.OpprettFrist("EV10 før stans", "FAG");
            await n.Side.SetViewportSizeAsync(bredde, 844); await n.Gaa("/admin/innstillinger");
            await n.Side.GetByRole(AriaRole.Link, new() { Name = "Åpne e-postabonnenter", Exact = true }).ClickAsync();
            var abonnent = n.Side.GetByRole(AriaRole.Article);
            await Expect(abonnent).ToHaveCountAsync(1); await Expect(abonnent).ToContainTextAsync("ev03@fin.dep.no");
            await Expect(abonnent).ToContainTextAsync("Alle budsjettår"); await Expect(abonnent).ToContainTextAsync("Alle kategorier");
            await Expect(abonnent).ToContainTextAsync("Nye frister");
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            var bekreftelse = n.Side.GetByRole(AriaRole.Region, new() { Name = "Bekreft stans av e-postvarsling" });
            await Expect(bekreftelse).ToContainTextAsync("ev03@fin.dep.no"); await Expect(bekreftelse).ToContainTextAsync("Aktiv");
            await bekreftelse.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(bekreftelse).ToHaveCountAsync(0); await Expect(abonnent).ToContainTextAsync("Aktiv");
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            await bekreftelse.GetByRole(AriaRole.Button, new() { Name = "Bekreft stans", Exact = true }).FocusAsync(); await n.Side.Keyboard.PressAsync("Enter");
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Videre e-postvarsling er stanset");
            await Expect(abonnent).ToContainTextAsync("Avmeldt"); await Expect(abonnent).ToContainTextAsync("demo-admin");
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Abonnentoversikten skal passe mobilbredden.");
            if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is { } rot)
            { Directory.CreateDirectory(Path.Combine(rot, "ev10")); await n.Side.ScreenshotAsync(new() { Path = Path.Combine(rot, "ev10", $"stanset-{bredde}.png"), FullPage = true }); }
            await n.OmstartAsync(); await n.LoggInn("demo-admin"); await n.Gaa("/admin/innstillinger/epostvarsling");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Avmeldt");
            await SettTid(n, "2030-01-02T07:00"); await n.OpprettFrist("EV10 under stans", "FAG");
            await n.Gaa("/demo/epostkjoring"); await Kjor(n, "0 bekreftet");
            await Aktiver(n, "demo-fag");
            await n.LoggInn("demo-admin"); await n.OpprettFrist("EV10 etter ny påmelding", "FAG");
            await SettTid(n, "2030-01-02T07:00"); await Kjor(n, "1 bekreftet");
            await n.LoggInn("demo-fag"); await n.Gaa("/demo/epostmottaker");
            var post = n.Side.GetByRole(AriaRole.Article); await Expect(post).ToHaveCountAsync(1);
            await Expect(post).ToContainTextAsync("EV10 etter ny påmelding");
            await Expect(post).Not.ToContainTextAsync("EV10 før stans"); await Expect(post).Not.ToContainTextAsync("EV10 under stans");
        });

    [Theory]
    [InlineData(390)] [InlineData(1280)]
    public async Task EV10_gammel_bekreftelse_viser_konflikt_og_krever_ny_lesing_og_bekreftelse(int bredde)
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag"); await n.LoggInn("demo-admin");
            await n.Side.SetViewportSizeAsync(bredde, 844); await n.Gaa("/admin/innstillinger/epostvarsling");
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            var administratorside = n.Side;
            await n.LoggInn("demo-fag"); await n.Gaa("/epostvarsling");
            await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Vesentlige endringer", Exact = true }).CheckAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre varselvalg", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Varselvalgene er lagret");
            n.Side = administratorside;
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft stans", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Abonnementet ble endret samtidig");
            await Expect(n.Side.GetByRole(AriaRole.Region, new() { Name = "Bekreft stans av e-postvarsling" })).ToHaveCountAsync(0);
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Vesentlige endringer");
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft stans", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Videre e-postvarsling er stanset");
        });

    [Fact]
    public async Task EV10_lagringsfeil_gir_trygg_UI_feil_og_oppdatert_nytt_forsoek()
        => await Trygt(async n =>
        {
            await Aktiver(n, "demo-fag"); await n.LoggInn("demo-admin"); await n.Gaa("/admin/innstillinger/epostvarsling");
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options;
            await using (var db = new AppDbContext(options))
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER TR_EV10_fail BEFORE INSERT ON Epostadministratorstanser BEGIN SELECT RAISE(ABORT, 'Kontrollert feil'); END");
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft stans", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Stans kunne ikke bekreftes");
            await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("Aktiv");
            await using (var db = new AppDbContext(options)) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_EV10_fail");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Oppdater abonnenter", Exact = true }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Stans varsling for") }).ClickAsync();
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Bekreft stans", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Videre e-postvarsling er stanset");
        });
}
