using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class DokumentTester
{
    [Fact]
    public Task PDF_opplasting_validerer_filtype_lager_uttrekksbevis_og_publiserer_ikke() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        var antall = (await n.Frister()).Count;
        await n.Gaa("/admin/last-opp?aar=2031");
        var fil = n.Side.GetByLabel("Dokument (PDF)");
        await fil.SetInputFilesAsync(new FilePayload { Name = "avvist.txt", MimeType = "text/plain", Buffer = Encoding.UTF8.GetBytes("ufarlig") });
        await Expect(n.Side.Locator(".feil")).ToContainTextAsync("Kun PDF");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true })).ToBeDisabledAsync();
        await fil.SetInputFilesAsync(new FilePayload { Name = "akseptanse.pdf", MimeType = "application/pdf", Buffer = Pdf("AK dokument rammefordeling 20. november 2030") });
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("laget 1 forslag");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("allerede lest inn tidligere");
        await n.Gaa("/admin/ko?aar=alle");
        var kort = n.Side.Locator("article.kort").Filter(new() { HasText = "AK dokument rammefordeling" });
        await Expect(kort).ToHaveCountAsync(1);
        await Expect(kort.Locator("table.bevis")).ToContainTextAsync("2030-11-20");
        await Expect(kort.Locator("table.bevis")).ToContainTextAsync("AK dokument rammefordeling 20. november 2030");
        await Expect(kort.Locator(".usikkerhetsflagg li")).ToHaveCountAsync(0);

        await n.Gaa("/admin/last-opp?aar=2031");
        // Datoen står etter det lagrede kildespennet på 600 tegn. Offline-uttrekket gir dermed
        // en konkret verdi med relativt kildeutdrag uten dato, utenfor budsjettvinduet.
        var usikkerKilde = "AK usikker rammefordeling ultimo mars <b>kildefelt</b> "
            + string.Concat(Enumerable.Repeat("kildetekst ", 61)) + "20. november 2040";
        await n.Side.GetByLabel("Dokument (PDF)").SetInputFilesAsync(new FilePayload
        {
            Name = "usikker.pdf", MimeType = "application/pdf", Buffer = Pdf(usikkerKilde)
        });
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Les inn dokument", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("laget 1 forslag");
        await n.Gaa("/admin/ko?aar=alle");
        var usikkertKort = n.Side.Locator("article.kort").Filter(new() { HasText = "AK usikker rammefordeling" });
        await Expect(usikkertKort).ToHaveCountAsync(1);
        var datobevis = usikkertKort.Locator("table.bevis tbody tr").Filter(new() { HasText = "2040-11-20" });
        await Expect(datobevis.Locator(".usikkerhetsflagg li")).ToHaveCountAsync(3);
        await Expect(datobevis).ToContainTextAsync("Kilden er relativt formulert, men er tolket til en konkret dato");
        await Expect(datobevis).ToContainTextAsync("Tolket dato har ingen gjenkjennelig dato i kildeutdraget");
        await Expect(datobevis).ToContainTextAsync("Tolket årstall 2040 er utenfor forventet vindu for budsjettår 2031");
        await Expect(datobevis).ToContainTextAsync("<b>kildefelt</b>");
        await Expect(datobevis.Locator("b")).ToHaveCountAsync(0);
        await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, "usikkerhetsflagg.png"), FullPage = true });
        Assert.Equal(antall, (await n.Frister()).Count);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Word_og_ICS_lastes_ned_fra_UI_med_korrekte_datoer_tid_og_tentativmerking() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("AK eksport heldag");
        await n.OpprettFrist("AK eksport tid", tid: "13:45");
        await n.OpprettFrist("AK eksport tentativ", tentativ: true);
        await n.OpprettFrist("AK eksport skjult", "POL");
        await n.Gaa("/admin/eksport");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true }).SelectOptionAsync("FA");
        await n.Side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2030-11-01");
        await n.Side.GetByLabel("Til dato", new() { Exact = true }).FillAsync("2030-11-30");
        var word = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned Word (.docx)" }).ClickAsync());
        var wordsti = Path.Combine(n.Mappe, "frister.docx");
        await word.SaveAsAsync(wordsti);
        using (var zip = ZipFile.OpenRead(wordsti))
        using (var les = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            var tekst = string.Join(" ", XDocument.Parse(await les.ReadToEndAsync()).Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
            Assert.Contains("AK eksport heldag", tekst);
            Assert.Contains("AK eksport tid", tekst);
            Assert.Contains("13:45", tekst);
            Assert.Contains("medio november", tekst);
            Assert.DoesNotContain("AK eksport skjult", tekst);
        }
        var ics = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned kalender (.ics)" }).ClickAsync());
        var icssti = Path.Combine(n.Mappe, "frister.ics");
        await ics.SaveAsAsync(icssti);
        var kalender = await File.ReadAllTextAsync(icssti);
        Assert.Contains("DTSTART;VALUE=DATE:20301120", kalender);
        Assert.Contains("DTEND;VALUE=DATE:20301121", kalender);
        Assert.Contains("DTSTART:20301120T124500Z", kalender);
        Assert.Contains("DTSTART;VALUE=DATE:20301115", kalender);
        Assert.Contains("AK eksport tentativ (tentativ)", kalender);
        Assert.DoesNotContain("AK eksport skjult", kalender);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task JSON_import_avbryt_ugyldig_versjon_rundtur_og_blandet_regelvarsel() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var lastNed = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned frister (JSON)" }).ClickAsync());
        var sti = Path.Combine(n.Mappe, "frister.json");
        await lastNed.SaveAsAsync(sti);
        var original = JsonNode.Parse(await File.ReadAllTextAsync(sti))!;
        var foer = (await n.Frister()).ToJsonString();
        var frister = n.Side.Locator("section").Filter(new() { Has = n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true }) });
        await frister.GetByLabel("Fil (JSON)").SetInputFilesAsync(sti);
        await frister.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)" }).ClickAsync();
        await frister.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        Assert.Equal(foer, (await n.Frister()).ToJsonString());
        var ugyldig = original.DeepClone(); ugyldig["versjon"] = 999;
        await frister.GetByLabel("Fil (JSON)").SetInputFilesAsync(JsonFil("ugyldig.json", ugyldig.ToJsonString()));
        await Importer(frister, "Importer frister (erstatt alt)");
        await Expect(frister.Locator(".feil").Filter(new() { HasText = "versjon" })).ToBeVisibleAsync();
        Assert.Equal(foer, (await n.Frister()).ToJsonString());
        await frister.GetByLabel("Fil (JSON)").SetInputFilesAsync(sti);
        await Importer(frister, "Importer frister (erstatt alt)");
        await Expect(frister).ToContainTextAsync("Importerte");
        Assert.Equal(foer, (await n.Frister()).ToJsonString());
        var mal = n.Side.Locator("section").Filter(new() { Has = n.Side.GetByRole(AriaRole.Heading, new() { Name = "Årsmaler (gjentaksregler)" }) });
        var blandet = new JsonObject { ["versjon"] = 1, ["regler"] = new JsonArray(
            new JsonObject { ["id"] = Guid.NewGuid(), ["loep"] = "ak-gyldig", ["tittel"] = "AK importregel", ["regeltype"] = "FastDato", ["regelparametre"] = "{\"maaned\":5,\"dag\":1}" },
            new JsonObject { ["id"] = Guid.NewGuid(), ["loep"] = "ak-ugyldig", ["regeltype"] = "FastDato", ["regelparametre"] = "{}" }) };
        await mal.GetByLabel("Fil (JSON)").SetInputFilesAsync(JsonFil("maler.json", blandet.ToJsonString()));
        await Importer(mal, "Importer årsmaler (erstatt alt)");
        await Expect(mal).ToContainTextAsync("Importerte 1 malregler");
        await Expect(mal).ToContainTextAsync("ak-ugyldig");
        await n.Gaa("/admin/mal");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToContainTextAsync("AK importregel");
        n.BekreftIngenKretsfeil();
    });

    private static async Task Importer(ILocator seksjon, string knapp)
    {
        await seksjon.GetByRole(AriaRole.Button, new() { Name = knapp, Exact = true }).ClickAsync();
        await seksjon.GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).ClickAsync();
    }

    private static FilePayload JsonFil(string navn, string json)
        => new() { Name = navn, MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(json) };

    private static byte[] Pdf(string tekst)
    {
        var innhold = $"BT /F1 12 Tf 40 750 Td ({tekst}) Tj ET\n";
        var objekter = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {innhold.Length} >>\nstream\n{innhold}endstream" };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var posisjoner = new List<int>();
        for (var i = 0; i < objekter.Length; i++) { posisjoner.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objekter[i]}\nendobj\n"); }
        var start = pdf.Length;
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var pos in posisjoner) pdf.Append($"{pos:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{start}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
