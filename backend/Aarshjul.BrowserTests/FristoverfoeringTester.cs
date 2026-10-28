using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class FristoverfoeringTester
{
    [Fact]
    public Task Valgt_fristfil_forhaandsvises_foer_bekreftelse_og_avbryt_bevarer_hele_databasen() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var (foer, sti) = await LastNed(n, "foer-avbryt.json");
        await Fil(n).SetInputFilesAsync(sti);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(Preview(n)).ToContainTextAsync($"{foer.Frister.Count}");
        await Expect(Preview(n).GetByRole(AriaRole.Heading, new() { Name = "Forhåndsvisning av fristfil", Exact = true })).ToBeFocusedAsync();
        await EksportflytTester.TaBilder(n, "fristimport-preview");
        Assert.Equal(Normaliser(foer), Normaliser((await LastNed(n, "etter-preview.json")).Fil));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync("erstatt");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeFocusedAsync();
        await EksportflytTester.TaBilder(n, "fristimport-bekreftelse");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(Fil(n)).ToBeFocusedAsync();
        await EksportflytTester.TaBilder(n, "fristimport-avbryt-fokus");
        Assert.Equal(Normaliser(foer), Normaliser((await LastNed(n, "etter-avbryt.json")).Fil));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Faktisk_fristfil_roundtrip_bevarer_alle_kontraktfelt_synlighet_status_og_sorteringsdag() => Nettleser.Kjor(async n =>
    {
        var tjeneste = Tjeneste(n);
        var mal = Enumerable.Range(0, 5).Select(i => Rad("fullfelt-" + i) with
        {
            Status = (FristStatus)i, Opphav = (Opphav)(i % 3),
            Datopresisjon = i == 1 ? Datopresisjon.Maaned : Datopresisjon.Dag,
            Datokvalifikator = i == 1 ? Datokvalifikator.Ultimo : null,
            Klokkeslett = i == 1 ? null : new TimeOnly(13, 45),
            SynligFor = i == 1 ? ["FA", "FAG", "FIN-FAG", "POL"] : ["FA"]
        }).ToArray();
        await tjeneste.ImporterAsync(new() { Frister = mal });
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var (fil, sti) = await LastNed(n, "fullfelt.json");
        Assert.Equal(1, fil.Versjon);
        Assert.NotEqual(default, fil.EksportertTid);
        Assert.Equal(Normaliser(new() { Frister = mal }), Normaliser(fil));
        var gammel = new FristDatabase { Frister = [Rad("erstattes")] };
        await tjeneste.ImporterAsync(gammel);
        await Forhaandsvis(n, sti);
        await Expect(Preview(n)).ToContainTextAsync("fullfelt.json");
        await Expect(Preview(n)).ToContainTextAsync("Godkjent");
        await Expect(Preview(n)).ToContainTextAsync("POL");
        Assert.Equal(Normaliser(gammel), Normaliser((await LastNed(n, "foer-bekreftet.json")).Fil));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(Fil(n)).ToBeFocusedAsync();
        Assert.Equal(Normaliser(gammel), Normaliser((await LastNed(n, "fullfelt-avbrutt.json")).Fil));
        await Forhaandsvis(n, sti);
        await Bekreft(n);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 5");
        Assert.Equal(Normaliser(fil), Normaliser((await LastNed(n, "fullfelt-etter.json")).Fil));
        var synlig = Assert.Single(await n.Frister());
        Assert.Equal(mal[1].Id.ToString(), (string?)synlig!["id"]);
        Assert.Equal("2030-11-30", (string?)synlig["sorteringsdag"]);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("versjon0")]
    [InlineData("versjon2")]
    [InlineData("null-liste")]
    [InlineData("null-rad")]
    [InlineData("duplikat")]
    [InlineData("mangler-frister")]
    [InlineData("json")]
    [InlineData("toppnivaa")]
    [InlineData("stor")]
    [InlineData("filtype")]
    public Task Filfeil_fjerner_gammel_preview_og_bevarer_alle_lagrede_fristfelt(string feil) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var (foer, sti) = await LastNed(n, "foer-feil.json");
        await Forhaandsvis(n, sti);
        await Expect(Preview(n)).ToBeVisibleAsync();
        var node = JsonSerializer.SerializeToNode(new FristDatabase { Frister = [Rad("ny")] }, WebJson)!.AsObject();
        var innhold = feil switch
        {
            "versjon0" => Endre("versjon", JsonValue.Create(0)),
            "versjon2" => Endre("versjon", JsonValue.Create(2)),
            "null-liste" => Endre("frister", null),
            "null-rad" => Endre("frister", new JsonArray((JsonNode?)null)),
            "duplikat" => Endre("frister", new JsonArray(node["frister"]![0]!.DeepClone(), node["frister"]![0]!.DeepClone())),
            "mangler-frister" => "{\"versjon\":1}",
            "toppnivaa" => "[]",
            "json" => "{ikke JSON",
            "stor" => new string(' ', 25 * 1024 * 1024 + 1),
            _ => node.ToJsonString()
        };
        string Endre(string felt, JsonNode? verdi) { node[felt] = verdi; return node.ToJsonString(); }
        await Fil(n).SetInputFilesAsync(JsonFil(feil == "filtype" ? "feil.txt" : "feil.json", innhold));
        if (feil is not ("stor" or "filtype")) await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(Preview(n)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true })).ToHaveCountAsync(0);
        Assert.Equal(Normaliser(foer), Normaliser((await LastNed(n, "etter-filfeil.json")).Fil));
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Tom_fristliste_krever_tydelig_egen_bekreftelse_og_avbryt_bevarer_alt(bool bekreft) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var (foer, _) = await LastNed(n, "foer-toemming.json");
        await Forhaandsvis(n, JsonFil("tom.json", "{\"versjon\":1,\"frister\":[]}"));
        await Expect(Preview(n)).ToContainTextAsync("0");
        await Expect(Preview(n)).ToContainTextAsync("fjern");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync($"{foer.Frister.Count}");
        await Expect(dialog).ToContainTextAsync("0");
        await dialog.GetByRole(AriaRole.Button, new() { Name = bekreft ? "Ja, erstatt alt" : "Avbryt", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        if (bekreft)
        {
            await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 0");
            Assert.Empty((await LastNed(n, "toemt.json")).Fil.Frister);
        }
        else Assert.Equal(Normaliser(foer), Normaliser((await LastNed(n, "avbrutt-tom.json")).Fil));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ukjent_gruppe_vises_foer_erstatning_og_kvittering_bruker_faktisk_importresultat() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var rad = Rad("ukjent-gruppe") with { SynligFor = ["FA", "FINNESIKKE"] };
        var fil = new FristDatabase { Frister = [rad] };
        await Forhaandsvis(n, JsonFil("ukjent-gruppe.json", JsonSerializer.Serialize(fil, WebJson)));
        await Expect(Preview(n)).ToContainTextAsync("FINNESIKKE");
        await Bekreft(n);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 1");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("FINNESIKKE");
        Assert.Equal(Normaliser(new() { Frister = [rad with { SynligFor = ["FA"] }] }), Normaliser((await LastNed(n, "kjent-gruppe.json")).Fil));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ny_fil_ugyldiggjoer_bekreftelse_og_lagringsfeil_ruller_tilbake_foer_nytt_forsoek() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/data");
        var (foer, _) = await LastNed(n, "foer-rollback.json");
        var a = new FristDatabase { Frister = [Rad("A-skal-ikke-importeres")] };
        var b = new FristDatabase { Frister = [Rad("B-bekreftet")] };
        await Forhaandsvis(n, JsonFil("A.json", JsonSerializer.Serialize(a, WebJson)));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(Fil(n)).ToBeFocusedAsync();
        await Fil(n).SetInputFilesAsync(JsonFil("B.json", JsonSerializer.Serialize(b, WebJson)));
        await Expect(Preview(n)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true })).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(Preview(n)).ToContainTextAsync("B-bekreftet");
        await Expect(Preview(n)).Not.ToContainTextAsync("A-skal-ikke-importeres");
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER avvis_ux90 AFTER DELETE ON Frister BEGIN SELECT RAISE(ABORT, 'Tekniske databasedetaljer'); END;");
        await Bekreft(n);
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("beholdt");
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("Tekniske databasedetaljer");
        await EksportflytTester.TaBilder(n, "fristimport-lagringsfeil");
        Assert.Equal(Normaliser(foer), Normaliser((await LastNed(n, "etter-rollback.json")).Fil));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_ux90;");
        await Bekreft(n);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 1");
        Assert.Equal(Normaliser(b), Normaliser((await LastNed(n, "etter-retry.json")).Fil));
        n.BekreftIngenKretsfeil();
    });

    private static readonly JsonSerializerOptions WebJson = Aarshjul.Web.Api.FristEndepunkter.DatabaseJson;
    private static ILocator Fil(Nettleser n) => n.Side.GetByLabel("Fil (JSON)", new() { Exact = true });
    private static ILocator Preview(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Importforhåndsvisning", Exact = true });
    private static FilePayload JsonFil(string navn, string json) => new() { Name = navn, MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(json) };
    private static async Task Forhaandsvis(Nettleser n, string sti)
    {
        await Fil(n).SetInputFilesAsync(sti);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(Preview(n)).ToBeVisibleAsync();
    }
    private static async Task Forhaandsvis(Nettleser n, FilePayload fil)
    {
        await Fil(n).SetInputFilesAsync(fil);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Forhåndsvis frister", Exact = true }).ClickAsync();
        await Expect(Preview(n)).ToBeVisibleAsync();
    }
    private static async Task Bekreft(Nettleser n)
    {
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, erstatt alt", Exact = true }).ClickAsync();
    }
    private static FristDatautveksling Tjeneste(Nettleser n) => new(new DatabaseFactory(n.Mappe), TimeProvider.System);
    private static FristEksport Rad(string tittel) => new()
    {
        Id = Guid.NewGuid(), Tittel = "UX90 " + tittel, Dato = new(2030, 11, 23), Budsjettaar = 2032,
        Kategori = Kategori.Regnskap, Loep = "årsregnskap", Kilde = "R-17/2030", DokumentId = Guid.NewGuid(),
        Status = FristStatus.Godkjent, Opphav = Opphav.Bruker, ForeslaattAv = "syntetisk-innsender",
        Notat = "æøå\nAndre linje", GjentaRegelId = Guid.NewGuid(), SynligFor = ["FA", "POL"]
    };
    private static string Normaliser(FristDatabase database) => JsonSerializer.Serialize(database.Frister.OrderBy(f => f.Id).Select(f => f with { SynligFor = f.SynligFor.Order().ToArray() }), WebJson);
    private static async Task<(FristDatabase Fil, string Sti)> LastNed(Nettleser n, string navn)
    {
        var download = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned frister (JSON)", Exact = true }).ClickAsync());
        var sti = Path.Combine(n.Mappe, navn);
        await download.SaveAsAsync(sti);
        return (JsonSerializer.Deserialize<FristDatabase>(await File.ReadAllTextAsync(sti), WebJson)!, sti);
    }
    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }
}
