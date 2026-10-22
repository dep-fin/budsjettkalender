using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class KoKildefilterTester
{
    private const string A = "<script data-kilde73>R-A & rå</script>";
    private const string B = "R-B";

    [Fact]
    public Task Kildevalg_kombineres_som_snitt_og_kan_byttes_eller_oppheves_i_tomt_utvalg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await Oppsett(n);
        await n.Gaa("/admin/ko");
        var kilde = Kilde(n);
        await Expect(kilde).ToBeVisibleAsync();
        await Expect(kilde.Locator("option")).ToHaveTextAsync(new[] { "Alle", A, B, "bruker-73", "opplastet73.pdf" });
        await Expect(n.Side.Locator("script[data-kilde73]")).ToHaveCountAsync(0);
        Assert.Contains("&lt;script", await kilde.EvaluateAsync<string>("el => el.outerHTML"));
        await Kort(n, 10);
        await kilde.SelectOptionAsync(A);
        await Kort(n, 5);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true }).SelectOptionAsync("Robot");
        await Kort(n, 4);
        await Expect(kilde).ToHaveValueAsync(A);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Budsjett");
        await Kort(n, 3);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("NyFrist");
        await Kort(n, 2);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Kun «ukjent type»" }).CheckAsync();
        await Kort(n, 1);
        await Expect(ForslagTester.Kort(n.Side, "KF73 a-ukjent")).ToBeVisibleAsync();
        await Expect(kilde.Locator("option")).ToHaveTextAsync(new[] { "Alle", A, B, "bruker-73", "opplastet73.pdf" });
        await kilde.SelectOptionAsync(B);
        await Expect(ForslagTester.Kort(n.Side, "KF73 b-ukjent")).ToBeVisibleAsync();
        await Kort(n, 1);
        await Expect(ForslagTester.Kort(n.Side, "KF73 a-ukjent")).ToHaveCountAsync(0);
        await kilde.SelectOptionAsync("opplastet73.pdf");
        await Kort(n, 0);
        await Expect(n.Side.GetByText("Ingen forslag til behandling.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(kilde).ToHaveValueAsync("opplastet73.pdf");
        await kilde.SelectOptionAsync("");
        await Kort(n, 2); // «Alle» beholder alle fire andre filtre.
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Kun «ukjent type»" }).UncheckAsync();
        await Kort(n, 5); // Også forslagene med null/blank kilde beholdes under «Alle».

        // Administratorgjerde og kilde-/bevismetadata prøves også i reell nettleser.
        foreach (var persona in new[] { "demo-fa", "demo-fag", "demo-pol" })
        {
            await n.LoggInn(persona);
            await n.Side.GotoAsync("/admin/ko");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
            await Expect(Kilde(n)).ToHaveCountAsync(0);
            var dom = await n.Side.ContentAsync();
            foreach (var markor in new[] { "KF73", "data-kilde73", B, "opplastet73.pdf", "bruker-73", "bevis73" })
                Assert.DoesNotContain(markor, dom);
        }
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("Godkjenn")]
    [InlineData("Juster")]
    [InlineData("Avvis")]
    public Task Aktiv_kilde_beholdes_etter_behandling_av_siste_treff_og_endring_bevarer_synlighet(string handling) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("KF73 original");
        var original = (await n.Frister()).Single(f => (string?)f!["tittel"] == "KF73 original")!;
        var id = (string)original["id"]!;
        await n.Gaa($"/admin/frist/{id}");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var synlighet = (await n.Frister()).Single(f => (string?)f!["id"] == id)!["synlighet"]!.ToJsonString();
        await Oppsett(n);
        using (var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}"))
        {
            await db.OpenAsync();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE Forslag SET ForslagType = 1, EndrerFristId = $id WHERE Tittel = 'KF73 b-ukjent'";
            cmd.Parameters.AddWithValue("$id", id.ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync();
        }
        await n.Gaa("/admin/ko");

        if (handling == "Godkjenn")
        {
            await Kilde(n).SelectOptionAsync(A);
            await Kort(n, 5);
            var ny = ForslagTester.Kort(n.Side, "KF73 a-kjent");
            await ny.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
            var pol = ny.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") });
            await Expect(pol).Not.ToBeCheckedAsync();
            await pol.CheckAsync();
            await ny.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning" }).ClickAsync();
            await Kort(n, 4);
            await Expect(Kilde(n)).ToHaveValueAsync(A);
            Assert.Contains("POL", (await n.Frister()).Single(f => (string?)f!["tittel"] == "KF73 a-kjent")!["synlighet"]!.ToJsonString());
        }

        await Kilde(n).SelectOptionAsync("");
        await Kort(n, handling == "Godkjenn" ? 9 : 10);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true }).SelectOptionAsync("Robot");
        await Kort(n, handling == "Godkjenn" ? 7 : 8);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Kun «ukjent type»" }).CheckAsync();
        await Kort(n, 4);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Budsjett");
        await Kort(n, 3);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true }).SelectOptionAsync("Endring");
        await Kort(n, 2);
        await Kilde(n).SelectOptionAsync(B);
        await Kort(n, 1);
        var kort = ForslagTester.Kort(n.Side, "KF73 b-ukjent");
        await kort.GetByRole(AriaRole.Button, new() { Name = handling == "Juster" ? "Vurder" : handling, Exact = true }).ClickAsync();
        if (handling == "Juster")
        {
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag" })).ToBeVisibleAsync();
            await Expect(n.Side.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
            await Expect(Kilde(n)).ToHaveValueAsync(B);
            await Kort(n, 1);
            await kort.GetByRole(AriaRole.Button, new() { Name = "Vurder", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag" })).ToBeVisibleAsync();
            await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("KF73 justert endring");
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø" })).ToBeVisibleAsync();
        }
        else
        {
            var bekreft = kort.GetByRole(AriaRole.Button, new() { Name = handling == "Godkjenn" ? "Bekreft godkjenning" : "Bekreft avvisning" });
            await Expect(bekreft).ToBeVisibleAsync();
            if (handling == "Godkjenn") await Expect(kort.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
            await bekreft.ClickAsync();
        }
        await Expect(Kilde(n)).ToHaveValueAsync(B);
        await Kort(n, 0);
        await Expect(n.Side.GetByText("Ingen forslag til behandling.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Opphav", Exact = true })).ToHaveValueAsync("Robot");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true })).ToHaveValueAsync("Endring");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true })).ToHaveValueAsync("Budsjett");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Kun «ukjent type»" })).ToBeCheckedAsync();
        var etter = (await n.Frister()).Single(f => (string?)f!["id"] == id)!;
        Assert.Equal(synlighet, etter["synlighet"]!.ToJsonString());
        Assert.Equal(handling switch { "Godkjenn" => "KF73 b-ukjent", "Juster" => "KF73 justert endring", _ => "KF73 original" }, (string?)etter["tittel"]);

        await Kilde(n).SelectOptionAsync(A);
        await Kort(n, 1); // Direkte kildebytte etter at siste B-forslag er behandlet.
        await Expect(ForslagTester.Kort(n.Side, "KF73 a-endring")).ToBeVisibleAsync();
        // Vanlig navigasjon starter et nytt utvalg; bare justeringsrundturen skal bevares.
        if (handling == "Juster")
        {
            await ForslagTester.Kort(n.Side, "KF73 a-endring").GetByRole(AriaRole.Button, new() { Name = "Vurder", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag" })).ToBeVisibleAsync();
        }
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kalender", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Kø", Exact = true }).ClickAsync();
        await Expect(Kilde(n)).ToHaveValueAsync("");
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Type", Exact = true })).ToHaveValueAsync("");
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Kilde(Nettleser n)
        => n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kilde eller innsender", Exact = true });

    private static Task Kort(Nettleser n, int antall) => Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(antall);

    private static async Task Oppsett(Nettleser n)
    {
        // Kun scenarioets nye, disponible SQLite. Ingen editor for syntetiske robotdata finnes.
        using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        using (var rydd = db.CreateCommand())
        {
            rydd.CommandText = "DELETE FROM UttrekksBevis; DELETE FROM Forslag;";
            await rydd.ExecuteNonQueryAsync();
        }
        await Legg(db, "a-kjent", A);
        await Legg(db, "a-ukjent", A, loep: null);
        await Legg(db, "a-endring", A, type: 1, loep: null);
        await Legg(db, "a-gul", A, kategori: 1, loep: null);
        await Legg(db, "a-bruker", A, opphav: 1, loep: null);
        await Legg(db, "b-ukjent", B, loep: null);
        await Legg(db, "opplasting", "opplastet73.pdf", kategori: 2);
        await Legg(db, "innsender", "bruker-73", opphav: 1);
        await Legg(db, "uten-kilde", null);
        await Legg(db, "blank-kilde", " \t ");
        await Legg(db, "godkjent", "bare-godkjent73", status: 1);
        await Legg(db, "avvist", "bare-avvist73", status: 3);
        await Legg(db, "generert", "bare-generert73", type: 2);
    }

    private static async Task Legg(SqliteConnection db, string navn, string? kilde,
        int opphav = 0, int kategori = 0, int type = 0, string? loep = "rammefordeling", int status = 0)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Forslag (Id, Versjon, ForslagType, Opphav, KildeEllerInnsender, Tittel,
                Dato, Datopresisjon, Budsjettaar, Kategori, Loep, ForeslaattSynlighet, Status)
            VALUES ($id, $versjon, $type, $opphav, $kilde, $tittel, '2030-11-20', 0, 2031, $kategori, $loep, '["FA","POL"]', $status);
            INSERT INTO UttrekksBevis (Id, ForslagId, Felt, TolketVerdi, Kildeutdrag, Konfidens)
            VALUES ($bevis, $id, 'dato', '20.11.2030', 'bevis73 20. november 2030', 0.99);
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$versjon", Guid.NewGuid().ToString().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$bevis", Guid.NewGuid().ToString().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$opphav", opphav);
        cmd.Parameters.AddWithValue("$kilde", (object?)kilde ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tittel", "KF73 " + navn);
        cmd.Parameters.AddWithValue("$kategori", kategori);
        cmd.Parameters.AddWithValue("$loep", (object?)loep ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", status);
        await cmd.ExecuteNonQueryAsync();
    }
}
