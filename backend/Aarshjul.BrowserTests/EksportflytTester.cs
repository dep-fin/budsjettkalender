using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class EksportflytTester
{
    [Theory]
    [InlineData("FA", "2030-11-01", "2030-11-30", "vinter,tentativ")]
    [InlineData("FAG", "2030-11-01", "2030-11-30", "fag")]
    [InlineData("FIN-FAG", "2030-11-01", "2030-11-30", "finfag")]
    [InlineData("POL", "2030-11-01", "2030-11-30", "pol")]
    [InlineData("alt", "2030-11-01", "2030-11-30", "vinter,tentativ,fag,finfag,pol,intern")]
    [InlineData("FA", "2030-11-01", "", "vinter,tentativ")]
    [InlineData("FA", "", "2030-11-30", "vinter,tentativ,historikk,utenfor,sommer")]
    [InlineData("FA", "", "", "vinter,tentativ,historikk,utenfor,sommer")]
    public Task Serverkontrollert_preview_og_faktiske_DOCX_ICS_har_eksakt_samme_publikum_og_inklusive_periode(string gruppe, string fra, string til, string navn) => Nettleser.Kjor(async n =>
    {
        var rader = Utvalg();
        await new FristDatautveksling(new DatabaseFactory(n.Mappe), TimeProvider.System).ImporterAsync(new() { Frister = rader });
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            var runde = new InternRunde { Id = Guid.NewGuid(), Rundetype = Rundetype.Augustrunden, Aar = 2031 };
            db.InternRunder.Add(runde);
            db.InterneGjoeremaal.Add(new() { Id = Guid.NewGuid(), RundeId = runde.Id, Tittel = "UX90 internt-gjøremål", Notat = "UX90 ansvarlig-status", Status = GjoeremaalStatus.Fullfoert, FullfoertAvNavn = "UX90 internansvarlig" });
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        // Andre kalenderfiltre får ikke i stillhet endre filutvalget.
        await n.Gaa("/kalender");
        await n.Gaa("/admin/eksport");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true })).ToHaveValueAsync("");
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned Word (.docx)", Exact = true })).ToHaveCountAsync(0);
        if (gruppe == "alt") await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("^Alt ") }).CheckAsync();
        else await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync(gruppe);
        await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync(fra);
        await n.Side.GetByLabel("Til dato", new() { Exact = true }).FillAsync(til);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Kontroller utvalg", Exact = true }).ClickAsync();
        var preview = n.Side.GetByRole(AriaRole.Region, new() { Name = "Eksportutvalg", Exact = true });
        var forventet = navn.Split(',').Select(k => rader.Single(r => r.Tittel == "UX90 " + k)).ToArray();
        await Expect(preview.Locator("tbody tr")).ToHaveCountAsync(forventet.Length);
        foreach (var rad in rader)
        {
            if (forventet.Contains(rad)) await Expect(preview).ToContainTextAsync(rad.Tittel);
            else await Expect(preview).Not.ToContainTextAsync(rad.Tittel);
        }
        await Expect(preview).Not.ToContainTextAsync("UX90 internt-gjøremål");
        var wordsti = await LastNed(n, "Last ned Word (.docx)", "frister.docx");
        string word;
        using (var zip = ZipFile.OpenRead(wordsti))
        using (var les = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            word = string.Join("\n", XDocument.Parse(await les.ReadToEndAsync()).Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
        var icssti = await LastNed(n, "Last ned kalender (.ics)", "frister.ics");
        var bytes = await File.ReadAllBytesAsync(icssti);
        var ics = Encoding.UTF8.GetString(bytes);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
        var unfold = Regex.Replace(ics, "\r\n[ \\t]", "");
        var ids = Regex.Matches(unfold, @"(?m)^UID:([0-9a-f-]+)@aarshjul\.fin\.dep\.no\r?$").Select(m => Guid.Parse(m.Groups[1].Value)).Order().ToArray();
        Assert.Equal(forventet.Select(f => f.Id).Order(), ids);
        foreach (var rad in rader)
        {
            Assert.Equal(forventet.Contains(rad), word.Contains(rad.Tittel, StringComparison.Ordinal));
            Assert.Equal(forventet.Contains(rad), unfold.Contains("SUMMARY:" + rad.Tittel, StringComparison.Ordinal));
        }
        Assert.DoesNotContain("UX90 internt-gjøremål", word + unfold);
        Assert.DoesNotContain("UX90 internansvarlig", word + unfold);
        Assert.Contains("Budsjettår 2031", word);
        if (gruppe == "alt") Assert.Contains("FIN-internt", word);
        if (forventet.Any(r => r.Tittel == "UX90 vinter"))
        {
            Assert.Contains("20.11.2030 kl. 13:45", word);
            Assert.Contains("DTSTART:20301120T124500Z", unfold);
            Assert.Contains("æøå, semikolon; bak\\linje", word);
            Assert.Contains("æøå\\, semikolon\\; bak\\\\linje\\nny linje", unfold);
        }
        if (forventet.Any(r => r.Tittel == "UX90 sommer")) Assert.Contains("DTSTART:20300620T114500Z", unfold);
        if (forventet.Any(r => r.Tittel == "UX90 tentativ"))
        {
            Assert.Contains("medio november 2030 (tentativ)", word);
            Assert.Contains("DTSTART;VALUE=DATE:20301115", unfold);
            Assert.Contains("DTEND;VALUE=DATE:20301116", unfold);
            Assert.Contains("SUMMARY:UX90 tentativ (tentativ)", unfold);
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Eksport_delings_og_dataflater_samt_filer_avviser_anonym_leser_og_begge_bidragspersonaer() => Nettleser.Kjor(async n =>
    {
        foreach (var persona in new string?[] { null, "demo-fag", "demo-fa", "demo-finfag", "demo-admin" })
        {
            await n.LoggInn(persona);
            foreach (var (sti, tittel) in new[] { ("/admin/eksport", "Eksporter frister"), ("/admin/data", "Eksporter / importer data"), ("/admin/abonnement", "Del kalender (abonnement)") })
            {
                await n.Side.GotoAsync(sti);
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = persona == "demo-admin" ? tittel : "Velg hvem du vil være", Exact = true })).ToBeVisibleAsync();
            }
            foreach (var sti in new[] { "/api/eksport/word?gruppe=FA", "/api/eksport/ics?gruppe=FA", "/api/eksport/frister-json", "/api/eksport/maler-json" })
            {
                var svar = await n.Side.Context.APIRequest.GetAsync(sti, new() { MaxRedirects = 0 });
                if (persona == "demo-admin") Assert.Equal(200, svar.Status);
                else
                {
                    Assert.Contains(svar.Status, new[] { 302, 401, 403 });
                    Assert.DoesNotContain("Frist for innspill", await svar.TextAsync());
                }
            }
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ugyldig_periode_skjuler_tidligere_utvalg_og_tomt_utvalg_gir_reelle_tomme_filer() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/eksport");
        await TaBilder(n, "eksport-uten-publikum");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync("FA");
        await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2050-11-01");
        await n.Side.GetByLabel("Til dato", new() { Exact = true }).FillAsync("2050-11-30");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Kontroller utvalg", Exact = true }).ClickAsync();
        var preview = n.Side.GetByRole(AriaRole.Region, new() { Name = "Eksportutvalg", Exact = true });
        await Expect(preview).ToContainTextAsync("Ingen godkjente frister");
        await Expect(preview.GetByRole(AriaRole.Heading, new() { Name = "0 godkjente frister", Exact = true })).ToBeFocusedAsync();
        await TaBilder(n, "eksport-tomt");
        var ics = await File.ReadAllTextAsync(await LastNed(n, "Last ned kalender (.ics)", "tom.ics"));
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.DoesNotContain("BEGIN:VEVENT", ics);
        var docx = await LastNed(n, "Last ned Word (.docx)", "tom.docx");
        using (var zip = ZipFile.OpenRead(docx))
        using (var les = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            Assert.Contains("Ingen frister i utvalget.", await les.ReadToEndAsync());
        await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2050-12-01");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(preview).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned Word (.docx)", Exact = true })).ToHaveCountAsync(0);
        await TaBilder(n, "eksport-periodefeil");
        await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2050-11-01");
        var knapp = n.Side.GetByRole(AriaRole.Button, new() { Name = "Kontroller utvalg", Exact = true });
        await knapp.FocusAsync();
        await knapp.PressAsync("Enter");
        await Expect(preview).ToBeVisibleAsync();
        await TaBilder(n, "eksport-tastatur");
        n.BekreftIngenKretsfeil();
    });

    internal static async Task TaBilder(Nettleser n, string navn)
    {
        foreach (var (bredde, hoyde) in new[] { (1440, 1000), (390, 844) })
        {
            await n.Side.SetViewportSizeAsync(bredde, hoyde);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.EvaluateAsync("window.scrollTo(0, 0)");
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"ux90-{navn}-{bredde}.png"), FullPage = !navn.Contains("bekreftelse", StringComparison.Ordinal) });
        }
    }
    private static async Task<string> LastNed(Nettleser n, string lenke, string navn)
    {
        var download = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = lenke, Exact = true }).ClickAsync());
        var sti = Path.Combine(n.Mappe, navn);
        await download.SaveAsAsync(sti);
        return sti;
    }
    private static FristEksport[] Utvalg() => [
        Rad("vinter", "FA") with { Klokkeslett = new(13, 45), Notat = "æøå, semikolon; bak\\linje\nny linje" },
        Rad("tentativ", "FA") with { Dato = new(2030, 11, 7), Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio },
        Rad("fag", "FAG"), Rad("finfag", "FIN-FAG"), Rad("pol", "POL"), Rad("intern"),
        Rad("historikk", "FA") with { Dato = new(2020, 11, 20) },
        Rad("utenfor", "FA") with { Dato = new(2030, 10, 31) },
        Rad("sommer", "FA") with { Dato = new(2030, 6, 20), Klokkeslett = new(13, 45) },
        Rad("upublisert", "FA") with { Status = FristStatus.Forslag }
    ];
    private static FristEksport Rad(string tittel, params string[] grupper) => new()
    {
        Id = Guid.NewGuid(), Tittel = "UX90 " + tittel, Dato = new(2030, 11, 20), Budsjettaar = 2031,
        Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, SynligFor = grupper
    };
    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }
}
