using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Faser;
using Aarshjul.Infrastructure.Generering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class MaloverfoeringTester
{
    [Fact]
    public Task Overfoer_mal_er_en_egen_aarmalflate_med_bevart_gammel_inngang() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Overfør mal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Overfør mal", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByLabel("Fil (JSON)", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned årsmal (JSON)", Exact = true }))
            .ToHaveAttributeAsync("href", "/api/eksport/maler-json");
        await n.Gaa("/admin/data");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Importer frister (erstatt alt)", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Overfør mal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Overfør mal", Exact = true })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Faktisk_nedlastet_hel_mal_roundtrip_bevarer_alle_felt_fleraarsberegning_bygger_og_generering() => Nettleser.Kjor(async n =>
    {
        var fabrikk = new DatabaseFactory(n.Mappe);
        IMalDatautveksling utveksling = new MalDatautveksling(fabrikk, TimeProvider.System);
        IMaltjeneste mal = new Maltjeneste(fabrikk);
        var sett = new MalDatabase { Regler = [
            Regel("fast", "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":-1}"),
            Regel("uke", "{\"maaned\":2,\"uke\":5,\"ukedag\":\"mandag\",\"aar_forskyvning\":1}", Regeltype.RelativUkedag, Kategori.Regnskap),
            Regel("fra", "{\"maaned\":7,\"fra_dag\":20,\"uke\":0,\"ukedag\":\"man\"}", Regeltype.RelativUkedag, Kategori.Gulbok),
            Regel("valg", "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":-1}", sensitiv: true),
            Regel("kjede", "{\"anker_loep\":\"fast\",\"offset_dager\":-2}", Regeltype.RelativTilMilepael),
            Regel("barn", "{\"anker_loep\":\"kjede\",\"offset_dager\":0}", Regeltype.RelativTilMilepael),
            Regel("valg-barn", "{\"anker_loep\":\"valg\",\"offset_dager\":2}", Regeltype.RelativTilMilepael),
            Regel("skudd", "{\"maaned\":2,\"dag\":29}") ] };
        await utveksling.ImporterAsync(sett);
        var beregninger = new Dictionary<int, string>();
        foreach (var aar in new[] { 2028, 2029, 2030, 2032 }) beregninger[aar] = await Beregning(mal, aar);
        var proeve2028 = await mal.ForhaandsvisAlleAsync(2028);
        var forventet2028 = new Dictionary<string, DateOnly>
        {
            ["fast"] = new(2027, 3, 19), ["uke"] = new(2029, 2, 26), ["fra"] = new(2028, 7, 24),
            ["valg"] = new(2027, 9, 15), ["kjede"] = new(2027, 3, 17), ["barn"] = new(2027, 3, 17),
            ["valg-barn"] = new(2027, 9, 17), ["skudd"] = new(2028, 2, 29)
        };
        for (var i = 0; i < proeve2028.Regler.Count; i++)
        {
            Assert.Equal(forventet2028[proeve2028.Regler[i].Loep], proeve2028.Beregninger[i].Dato);
            Assert.Null(proeve2028.Beregninger[i].Feil);
        }
        var proeve2030 = await mal.ForhaandsvisAlleAsync(2030);
        var valgBarn = proeve2030.Beregninger[proeve2030.Regler.ToList().FindIndex(r => r.Loep == "valg-barn")];
        Assert.True(valgBarn.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, valgBarn.Presisjon);
        Assert.True(proeve2030.Beregninger[proeve2030.Regler.ToList().FindIndex(r => r.Loep == "valg")].Valgaarsflagg);
        Assert.NotNull(proeve2030.Beregninger[proeve2030.Regler.ToList().FindIndex(r => r.Loep == "skudd")].Feil);
        await n.LoggInn("demo-admin");
        var frister = (await n.Frister()).ToJsonString();
        var faser = JsonSerializer.Serialize(await new FasemalTjeneste(fabrikk).HentAsync());
        await n.Gaa("/admin/mal");
        await n.Side.GetByLabel("Søk i regler", new() { Exact = true }).FillAsync("UX87 fast");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Overfør mal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Overfør mal", Exact = true })).ToBeVisibleAsync();
        var (foer, sti) = await LastNed(n, "original.json");
        Assert.Equal(8, foer.Regler.Count);
        Assert.Equal(1, foer.Versjon);
        Assert.NotEqual(default, foer.EksportertTid);
        Assert.DoesNotContain("faser", await File.ReadAllTextAsync(sti), StringComparison.OrdinalIgnoreCase);
        // Gjenopprett faktisk nedlastede bytes over en annen hel mal, ikke syntetisk kopi.
        await utveksling.ImporterAsync(new() { Regler = [Regel("erstattes", "{\"maaned\":1,\"dag\":2}")] });
        var gammel = (await utveksling.EksporterAsync()).Regler;
        await Fil(n).SetInputFilesAsync(sti);
        await Expect(Preview(n)).ToContainTextAsync("8 regler tas inn");
        Assert.Equal(gammel, (await utveksling.EksporterAsync()).Regler);
        await Bekreft(n, 1);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 8");
        var (etter, _) = await LastNed(n, "etter.json");
        Assert.Equal(foer.Regler.OrderBy(r => r.Id), etter.Regler.OrderBy(r => r.Id));
        foreach (var aar in beregninger.Keys) Assert.Equal(beregninger[aar], await Beregning(mal, aar));
        Assert.Equal(faser, JsonSerializer.Serialize(await new FasemalTjeneste(fabrikk).HentAsync()));
        Assert.Equal(frister, (await n.Frister()).ToJsonString());
        foreach (var (loep, valg) in new[] { ("fast", "Fast dato"), ("uke", "Ukedag i måneden"), ("fra", "Ukedag fra og med en dato"), ("kjede", "Før eller etter en milepæl") })
        {
            var regel = Assert.Single(etter.Regler, r => r.Loep == loep);
            await n.Gaa($"/admin/mal/{regel.Id}");
            await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(regel.Tittel);
            await Expect(n.Side.GetByRole(AriaRole.Radio, new() { Name = valg, Exact = true })).ToBeCheckedAsync();
        }
        await n.Gaa("/admin/mal/proev?aar=2028");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(8);
        await Expect(n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX87 fast" }).Locator("time")).ToHaveAttributeAsync("datetime", "2027-03-19");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToHaveValueAsync("2028");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Genererte 8 forslag");
        await Expect(n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX87 fast" })).ToContainTextAsync("19.03.2027");
        Assert.Equal(frister, (await n.Frister()).ToJsonString());
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Blandet_preview_er_sideeffektfri_og_gyldig_delmengde_erstatter_hele_malen() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/overfor");
        var (foer, _) = await LastNed(n, "gammel.json");
        var gyldig = Regel("ny", "{\"maaned\":5,\"dag\":2}");
        var fil = new MalDatabase { Regler = [gyldig, Regel("ugyldig", "{}"), Regel("", "{}") with { Tittel = "Uten løp" }] };
        await Fil(n).SetInputFilesAsync(JsonFil("blandet.json", JsonSerializer.Serialize(fil, WebJson)));
        await Expect(Preview(n)).ToContainTextAsync("1 regler tas inn");
        await Expect(Preview(n)).ToContainTextAsync("2 regler utelates");
        await Expect(Preview(n)).ToContainTextAsync($"Alle {foer.Regler.Count} eksisterende regler erstattes");
        await Expect(Preview(n)).ToContainTextAsync("UX87 ugyldig");
        await Expect(Preview(n)).ToContainTextAsync("Uten løp");
        Assert.Equal(foer.Regler, (await LastNed(n, "foer-bekreft.json")).Fil.Regler);
        await Bekreft(n, foer.Regler.Count);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 1");
        Assert.Equal(new[] { gyldig }, (await LastNed(n, "etter-blandet.json")).Fil.Regler);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task Null_gyldige_og_tom_liste_krever_egen_toemmingsbekreftelse_og_avbryt_bevarer_alt(bool tomListe, bool bekreft) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/overfor");
        var (foer, _) = await LastNed(n, "gammel.json");
        var fil = new MalDatabase { Regler = tomListe ? [] : [Regel("ugyldig", "{}")] };
        await Fil(n).SetInputFilesAsync(JsonFil("tom.json", JsonSerializer.Serialize(fil, WebJson)));
        await Expect(Preview(n)).ToContainTextAsync("Ingen regler kan tas inn");
        await Expect(Preview(n)).ToContainTextAsync($"{foer.Regler.Count}");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Tøm årsmalen", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, tøm årsmalen", Exact = true })).ToBeEnabledAsync();
        IMalDatautveksling utveksling = new MalDatautveksling(new DatabaseFactory(n.Mappe), TimeProvider.System);
        Assert.Equal(foer.Regler, (await utveksling.EksporterAsync()).Regler);
        await dialog.GetByRole(AriaRole.Button, new() { Name = bekreft ? "Ja, tøm årsmalen" : "Avbryt", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        if (bekreft)
        {
            await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 0");
            Assert.Empty((await LastNed(n, "toemt.json")).Fil.Regler);
        }
        else
        {
            await Expect(Preview(n)).ToHaveCountAsync(0);
            Assert.Equal(foer.Regler, (await LastNed(n, "avbrutt.json")).Fil.Regler);
        }
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("uten")]
    [InlineData("null")]
    [InlineData("blank")]
    public Task Eldre_JSON_uten_tittel_og_med_tidligere_parametre_forhaandsvises_og_importeres(string tittel) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/overfor");
        var (foer, _) = await LastNed(n, "foer-eldre.json");
        var id = Guid.NewGuid();
        var rad = JsonSerializer.SerializeToNode(Regel("eldre", "{\"maaned\":7,\"ukedag\":\"mandag\",\"fra_dag\":20,\"uke\":0}", Regeltype.RelativUkedag) with { Id = id }, WebJson)!.AsObject();
        if (tittel == "uten") rad.Remove("tittel"); else rad["tittel"] = tittel == "null" ? null : "  ";
        // Manglende versjon skal fortsatt bety versjon 1.
        var fil = new JsonObject { ["regler"] = new JsonArray(rad) };
        await Fil(n).SetInputFilesAsync(JsonFil("eldre.json", fil.ToJsonString()));
        await Expect(Preview(n)).ToContainTextAsync("eldre");
        await Bekreft(n, foer.Regler.Count);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 1");
        var lagret = Assert.Single((await LastNed(n, "etter-eldre.json")).Fil.Regler);
        Assert.Equal(id, lagret.Id);
        Assert.Equal("", lagret.Tittel);
        Assert.Equal(rad["regelparametre"]!.GetValue<string>(), lagret.Regelparametre);
        var beregning = await new Maltjeneste(new DatabaseFactory(n.Mappe)).ForhaandsvisAlleAsync(2028);
        Assert.Equal(new DateOnly(2028, 7, 24), Assert.Single(beregning.Beregninger).Dato);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("versjon0")]
    [InlineData("versjon2")]
    [InlineData("null-liste")]
    [InlineData("null-rad")]
    [InlineData("duplikat")]
    [InlineData("mangler-regler")]
    [InlineData("json")]
    [InlineData("toppnivaa")]
    [InlineData("stor")]
    [InlineData("filtype")]
    public Task Filfeil_fjerner_gammel_preview_og_bevarer_hele_eksporten(string feil) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/overfor");
        var (foer, _) = await LastNed(n, "gammel.json");
        var gyldig = JsonSerializer.Serialize(new MalDatabase { Regler = [Regel("stale", "{\"maaned\":5,\"dag\":2}")] }, WebJson);
        await Fil(n).SetInputFilesAsync(JsonFil("gyldig.json", gyldig));
        await Expect(Preview(n)).ToBeVisibleAsync();
        var node = JsonNode.Parse(gyldig)!.AsObject();
        var innhold = feil switch
        {
            "versjon0" => Endre("versjon", JsonValue.Create(0)),
            "versjon2" => Endre("versjon", JsonValue.Create(2)),
            "null-liste" => Endre("regler", null),
            "null-rad" => Endre("regler", new JsonArray((JsonNode?)null)),
            "duplikat" => Endre("regler", new JsonArray(node["regler"]![0]!.DeepClone(), node["regler"]![0]!.DeepClone())),
            "mangler-regler" => "{\"versjon\":1}",
            "toppnivaa" => "[]",
            "json" => "{ikke JSON",
            "stor" => new string(' ', 25 * 1024 * 1024 + 1),
            _ => gyldig
        };
        string Endre(string felt, JsonNode? verdi) { node[felt] = verdi; return node.ToJsonString(); }
        await Fil(n).SetInputFilesAsync(JsonFil(feil == "filtype" ? "mal.txt" : "mal.json", innhold));
        await Expect(n.Side.Locator("main .feil")).ToBeVisibleAsync();
        await Expect(Preview(n)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Erstatt hele årsmalen", Exact = true })).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        Assert.Equal(foer.Regler, (await LastNed(n, "etter-avvisning.json")).Fil.Regler);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Lagringsfeil_bevarer_alle_gamle_felt_og_nytt_forsoek_lykkes_i_samme_krets() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/overfor");
        var (foer, _) = await LastNed(n, "foer-feil.json");
        var ny = Regel("ny-etter-feil", "{\"maaned\":5,\"dag\":2}");
        await Fil(n).SetInputFilesAsync(JsonFil("ny.json", JsonSerializer.Serialize(new MalDatabase { Regler = [ny] }, WebJson)));
        await Expect(Preview(n)).ToBeVisibleAsync();
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER avvis_ux87 AFTER DELETE ON Gjentaksregler BEGIN SELECT RAISE(ABORT, 'Hemmelige databasedetaljer'); END;");
        await Bekreft(n, foer.Regler.Count);
        await Expect(n.Side.Locator("main .feil")).ToContainTextAsync("Den tidligere årsmalen er beholdt");
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("Hemmelige databasedetaljer");
        Assert.Equal(foer.Regler, (await LastNed(n, "etter-feil.json")).Fil.Regler);
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_ux87;");
        await Bekreft(n, foer.Regler.Count);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Importerte 1");
        Assert.Equal(new[] { ny }, (await LastNed(n, "etter-retry.json")).Fil.Regler);
        n.BekreftIngenKretsfeil();
    });

    private static readonly JsonSerializerOptions WebJson = Aarshjul.Web.Api.FristEndepunkter.DatabaseJson;
    private static FilePayload JsonFil(string navn, string json) => new() { Name = navn, MimeType = "application/json", Buffer = Encoding.UTF8.GetBytes(json) };
    private static ILocator Fil(Nettleser n) => n.Side.GetByLabel("Fil (JSON)", new() { Exact = true });
    private static ILocator Preview(Nettleser n) => n.Side.GetByRole(AriaRole.Region, new() { Name = "Importforhåndsvisning", Exact = true });
    private static async Task Bekreft(Nettleser n, int antall)
    {
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Erstatt hele årsmalen", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = $"Ja, erstatt alle {antall} regler", Exact = true }).ClickAsync();
    }
    private static async Task<(MalDatabase Fil, string Sti)> LastNed(Nettleser n, string navn)
    {
        var download = await n.Side.RunAndWaitForDownloadAsync(() => n.Side.GetByRole(AriaRole.Link, new() { Name = "Last ned årsmal (JSON)", Exact = true }).ClickAsync());
        var sti = Path.Combine(n.Mappe, navn);
        await download.SaveAsAsync(sti);
        return (JsonSerializer.Deserialize<MalDatabase>(await File.ReadAllTextAsync(sti), WebJson)!, sti);
    }
    private static MalRegelEksport Regel(string loep, string json, Regeltype type = Regeltype.FastDato, Kategori kategori = Kategori.Budsjett, bool sensitiv = false) => new()
    {
        Id = Guid.NewGuid(), Loep = loep, Tittel = "UX87 " + loep, Regelparametre = json,
        Regeltype = type, Kategori = kategori, Valgaarssensitiv = sensitiv
    };
    private static async Task<string> Beregning(IMaltjeneste mal, int aar)
    {
        var r = await mal.ForhaandsvisAlleAsync(aar);
        return JsonSerializer.Serialize(r.Regler.Select((regel, i) => new { regel.Id, Verdi = r.Beregninger[i] }).OrderBy(r => r.Id));
    }
    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }
}

