using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Testkontrakter;
using Aarshjul.Tests;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class MalproevingTester
{
    [Fact]
    public Task Proev_malen_viser_manglende_anker_og_lar_administrator_beregne_uten_forslag() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(new()
        {
            Tittel = "UX86 manuelt navngitt", Loep = "ux86-barn", Regeltype = Regeltype.RelativTilMilepael,
            Regelparametre = "{\"anker_loep\":\"ux86-mangler\",\"offset_dager\":2}"
        });
        await FasedataBevis.LeggTilHistorikkAsync(new DatabaseFactory(n.Mappe));
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            db.Varsler.Add(new() { Id = Guid.NewGuid(), BrukerId = "ux86-innsender", Tekst = "Bevar også varselet", Begrunnelse = "Tidligere behandling", Lest = true });
            await db.SaveChangesAsync();
        }
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Prøv malen", Exact = true })).ToBeVisibleAsync();
        await n.Side.GetByLabel("Prøvebudsjettår", new() { Exact = true }).FillAsync("2032");
        await n.Side.GetByLabel("Prøvebudsjettår", new() { Exact = true }).PressAsync("Tab");
        var rad = n.Side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX86 manuelt navngitt" });
        await Expect(rad).ToContainTextAsync("Kan ikke beregnes");
        var dato = n.Side.GetByLabel("Midlertidig dato for ux86-mangler", new() { Exact = true });
        await dato.FillAsync("2032-03-01");
        await dato.PressAsync("Tab");
        await Expect(rad.Locator("time")).ToHaveAttributeAsync("datetime", "2032-03-03");
        await Dato(n, "ux86-mangler", "2032-04-08");
        await Expect(rad.Locator("time")).ToHaveAttributeAsync("datetime", "2032-04-10");
        await Dato(n, "ux86-mangler", "");
        await Expect(rad).ToContainTextAsync("Kan ikke beregnes");
        Assert.Equal(foer, await Snapshot(n));
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("fast", "2032-03-23")]
    [InlineData("forekomst", "2032-03-15")]
    [InlineData("fra-dag", "2032-03-22")]
    [InlineData("relativ", "2032-03-22")]
    public Task Ny_og_faktisk_redigert_kodefri_regel_brukes_i_hel_proeve_og_neste_generering(string type, string forventet) => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 skjult anker", "ux86-anker", "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/ny");
        await Felt(n, "Funksjon / løp").FillAsync("ux86-ny-" + type);
        await Felt(n, "Tittel").FillAsync("UX86 opprettet " + type);
        await Felt(n, "Kategori").SelectOptionAsync("Gulbok");
        if (type == "relativ")
        {
            await n.Side.GetByRole(AriaRole.Radio, new() { Name = "Før eller etter en milepæl", Exact = true }).CheckAsync();
            await Felt(n, "Ankerløp").FillAsync("ux86-anker");
            await Felt(n, "Retning").SelectOptionAsync("etter");
            await Felt(n, "Antall kalenderdager").FillAsync("1");
        }
        else
        {
            if (type != "fast")
            {
                await n.Side.GetByRole(AriaRole.Radio, new() { Name = type == "forekomst" ? "Ukedag i måneden" : "Ukedag fra og med en dato", Exact = true }).CheckAsync();
                await Felt(n, "Ukedag").SelectOptionAsync("man");
                if (type == "forekomst") await Felt(n, "Forekomst").SelectOptionAsync("2");
                else await Felt(n, "Fra og med dag").FillAsync("10");
            }
            else await Felt(n, "Dag").FillAsync("10");
            await Felt(n, "Måned").SelectOptionAsync("3");
        }
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 opprettet " + type)).ToBeVisibleAsync();
        var id = Assert.Single(await mal.HentAlleAsync(), r => r.Tittel == "UX86 opprettet " + type).Id;
        await Felt(n, "Søk i regler").FillAsync("UX86 opprettet");
        await Felt(n, "Sortering").SelectOptionAsync("Senest");
        await Rad(n, "UX86 opprettet " + type).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Tittel")).ToHaveValueAsync("UX86 opprettet " + type);
        await Felt(n, "Tittel").FillAsync("UX86 lagret " + type);
        if (type == "fast") await Felt(n, "Dag").FillAsync("23");
        else if (type == "forekomst") await Felt(n, "Forekomst").SelectOptionAsync("3");
        else if (type == "fra-dag") await Felt(n, "Fra og med dag").FillAsync("20");
        else await Felt(n, "Antall kalenderdager").FillAsync("3");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Søk i regler")).ToHaveValueAsync("UX86 opprettet");
        await Expect(Felt(n, "Sortering")).ToHaveValueAsync("Senest");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Prøv malen", Exact = true }).ClickAsync();
        await Aar(n, "2032");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(2);
        await Expect(Rad(n, "UX86 lagret " + type).Locator("time")).ToHaveAttributeAsync("datetime", forventet);
        await Expect(Rad(n, "UX86 skjult anker")).ToBeVisibleAsync();
        var foer = await Snapshot(n);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Målbudsjettår")).ToHaveValueAsync("2032");
        Assert.Equal(foer, await Snapshot(n));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 lagret " + type)).ToContainTextAsync("Gul bok");
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        var forslag = await db.Forslag.Where(f => f.Budsjettaar == 2032 && f.ForslagType == ForslagType.Generert).ToListAsync();
        Assert.Equal(2, forslag.Count);
        var endret = Assert.Single(forslag, f => f.Tittel == "UX86 lagret " + type);
        Assert.Equal(DateOnly.Parse(forventet, CultureInfo.InvariantCulture), endret.Dato);
        Assert.Equal(Kategori.Gulbok, endret.Kategori);
        Assert.Equal(id, Assert.Single(await mal.HentAlleAsync(), r => r.Tittel == endret.Tittel).Id);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == endret.Tittel);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Manuelle_datoer_settes_endres_og_fjernes_gjennom_hele_kjeden_med_uavhengige_regler() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        // Barn først gjør dette til et bevis på rekursiv kjede, ikke listeorden.
        await mal.OpprettAsync(Regel("UX86 C", "ux86-c", "{\"anker_loep\":\"ux86-b\",\"offset_dager\":2}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("UX86 B", "ux86-b", "{\"anker_loep\":\"ux86-a\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("UX86 A", "ux86-a", "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("UX86 D", "ux86-d", "{\"maaned\":4,\"dag\":12}"));
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2032");
        await ForventDato(n, "UX86 A", "2032-03-19");
        await ForventDato(n, "UX86 B", "2032-03-20");
        await ForventDato(n, "UX86 C", "2032-03-22");
        await Dato(n, "ux86-a", "2032-04-01");
        await ForventDato(n, "UX86 B", "2032-04-02");
        await ForventDato(n, "UX86 C", "2032-04-04");
        await Dato(n, "ux86-b", "2032-05-10");
        await ForventDato(n, "UX86 A", "2032-04-01");
        await ForventDato(n, "UX86 B", "2032-05-10");
        await ForventDato(n, "UX86 C", "2032-05-12");
        await ForventDato(n, "UX86 D", "2032-04-12");
        await Dato(n, "ux86-b", "");
        await ForventDato(n, "UX86 C", "2032-04-04");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fjern alle datoer for 2032", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Midlertidig dato for ux86-a")).ToHaveValueAsync("");
        await ForventDato(n, "UX86 C", "2032-03-22");
        Assert.Equal(foer, await Snapshot(n));
        await Dato(n, "ux86-a", "2032-04-01");
        await ForventDato(n, "UX86 C", "2032-04-04");
        await Bilder(n, "kjede");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Målbudsjettår")).ToHaveValueAsync("2032");
        await Expect(Felt(n, "Midlertidig dato for ux86-a")).ToHaveValueAsync("2032-04-01");
        Assert.Equal(foer, await Snapshot(n));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 C")).ToContainTextAsync("04.04.2032");
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(4);
        var etterGenerering = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2032");
        await Expect(Felt(n, "Midlertidig dato for ux86-a")).ToHaveValueAsync("");
        await ForventDato(n, "UX86 C", "2032-03-22");
        Assert.Equal(etterGenerering, await Snapshot(n));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Faktisk_valgaar_presisjon_og_aarsskifte_arves_uten_oppdiktet_dag_og_manuell_dato_kan_fjernes() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 sensitiv uten barn", "ux86-enslig", "{\"maaned\":11,\"dag\":20,\"aar_forskyvning\":-1}", sensitiv: true));
        await mal.OpprettAsync(Regel("UX86 sensitiv desember", "ux86-desember", "{\"maaned\":12,\"dag\":31,\"aar_forskyvning\":-1}", sensitiv: true));
        await mal.OpprettAsync(Regel("UX86 arvet januar", "ux86-januar", "{\"anker_loep\":\"ux86-desember\",\"offset_dager\":2}", Regeltype.RelativTilMilepael));
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2026");
        await ForventDato(n, "UX86 sensitiv uten barn", "2025-11");
        await ForventDato(n, "UX86 sensitiv desember", "2025-12");
        await ForventDato(n, "UX86 arvet januar", "2026-01");
        foreach (var navn in new[] { "UX86 sensitiv uten barn", "UX86 sensitiv desember", "UX86 arvet januar" })
        {
            await Expect(Rad(n, navn).Locator("time")).ToContainTextAsync("tentativ");
            await Expect(Rad(n, navn).Locator("time")).Not.ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("[0-9]+\\."));
            Assert.DoesNotMatch(@"\b(20|31|02)\.", await Rad(n, navn).Locator("time").InnerTextAsync());
        }
        await Bilder(n, "valgaar-maaneder");
        await Dato(n, "ux86-desember", "2025-12-30");
        await ForventDato(n, "UX86 sensitiv desember", "2025-12-30");
        await ForventDato(n, "UX86 arvet januar", "2026-01-01");
        await Dato(n, "ux86-enslig", "2025-11-25");
        await ForventDato(n, "UX86 sensitiv uten barn", "2025-11-25");
        await Dato(n, "ux86-desember", "");
        await ForventDato(n, "UX86 arvet januar", "2026-01");
        await Aar(n, "2028");
        await ForventDato(n, "UX86 sensitiv desember", "2027-12-31");
        await ForventDato(n, "UX86 arvet januar", "2028-01-02");
        await Expect(Rad(n, "UX86 sensitiv desember")).ToContainTextAsync("Valgår");
        await Expect(Rad(n, "UX86 sensitiv desember").Locator("time")).Not.ToContainTextAsync("tentativ");
        await Expect(Felt(n, "Midlertidig dato for ux86-enslig")).ToHaveValueAsync("");
        await Aar(n, "2026");
        await Expect(Felt(n, "Midlertidig dato for ux86-enslig")).ToHaveValueAsync("2025-11-25");
        await ForventDato(n, "UX86 sensitiv uten barn", "2025-11-25");
        Assert.Equal(foer, await Snapshot(n));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Duplikate_loep_beholder_begge_rader_og_samme_ankerrepresentant_som_faktisk_generering() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 duplikat Z", "ux86-duplikat", "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("UX86 duplikat A", "UX86-DUPLIKAT", "{\"maaned\":4,\"dag\":12}"));
        await mal.OpprettAsync(Regel("UX86 duplikat barn", "ux86-duplikat-barn", "{\"anker_loep\":\"ux86-duplikat\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Felt(n, "Søk i regler").FillAsync("UX86 duplikat A");
        await Felt(n, "Sortering").SelectOptionAsync("Tittel");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Prøv malen", Exact = true }).ClickAsync();
        await Aar(n, "2026");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(3);
        var dato = await Rad(n, "UX86 duplikat barn").Locator("time").GetAttributeAsync("datetime");
        Assert.Contains(dato, new[] { "2026-03-21", "2026-04-14" });
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(3);
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        var forslag = await db.Forslag.Where(f => f.Budsjettaar == 2026 && f.ForslagType == ForslagType.Generert && f.Status == FristStatus.Forslag).ToListAsync();
        Assert.Equal(3, forslag.Count);
        Assert.Equal(DateOnly.Parse(dato!, CultureInfo.InvariantCulture), Assert.Single(forslag, f => f.Tittel == "UX86 duplikat barn").Dato);
        Assert.Equal(2, forslag.Count(f => string.Equals(f.Loep, "ux86-duplikat", StringComparison.OrdinalIgnoreCase)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Alle_beregningsfeil_vises_paa_tilsvarende_rader_og_bare_uavhengig_gyldig_delmengde_genereres() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 gyldig", "ux86-gyldig", "{\"maaned\":3,\"dag\":20}"));
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            foreach (var (tittel, loep, type, json) in new[]
            {
                ("UX86 barnebarn", "ux86-c", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-b\",\"offset_dager\":1}"),
                ("UX86 barn feil", "ux86-b", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-json\",\"offset_dager\":1}"),
                ("UX86 JSON", "ux86-json", Regeltype.FastDato, "ikke JSON"),
                ("UX86 ukjent type", "ux86-enum", (Regeltype)99, "{}"),
                ("UX86 skuddag", "ux86-skudd", Regeltype.FastDato, "{\"maaned\":2,\"dag\":29}"),
                ("UX86 aarsoverflyt", "ux86-aar", Regeltype.FastDato, "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":2147483647}"),
                ("UX86 offset maks", "ux86-maks", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-gyldig\",\"offset_dager\":2147483647}"),
                ("UX86 offset min", "ux86-min", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-gyldig\",\"offset_dager\":-2147483648}"),
                ("UX86 sirkel A", "ux86-sirkel-a", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-sirkel-b\",\"offset_dager\":1}"),
                ("UX86 sirkel B", "ux86-sirkel-b", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-sirkel-a\",\"offset_dager\":1}"),
                ("UX86 mangler", "ux86-mangler-barn", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"ux86-ukjent\",\"offset_dager\":1}")
            }) db.Gjentaksregler.Add(new() { Id = Guid.NewGuid(), Tittel = tittel, Loep = loep, Regeltype = type, Regelparametre = json });
            await db.SaveChangesAsync();
        }
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2026");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(12);
        await ForventDato(n, "UX86 gyldig", "2026-03-20");
        foreach (var r in (await mal.HentAlleAsync()).Where(r => r.Tittel != "UX86 gyldig"))
        {
            await Expect(Rad(n, r.Tittel)).ToContainTextAsync("Kan ikke beregnes");
            await Expect(Rad(n, r.Tittel).Locator("time")).ToHaveCountAsync(0);
            await Expect(Rad(n, r.Tittel).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToBeVisibleAsync();
        }
        Assert.Equal(foer, await Snapshot(n));
        await Bilder(n, "alle-feil");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(1);
        await Expect(Rad(n, "UX86 gyldig")).ToBeVisibleAsync();
        foreach (var r in (await mal.HentAlleAsync()).Where(r => r.Tittel != "UX86 gyldig"))
            await Expect(n.Side.Locator(".feil").Filter(new() { HasText = r.Tittel })).ToBeVisibleAsync();
        await using var kontroll = new DatabaseFactory(n.Mappe).CreateDbContext();
        Assert.Equal("UX86 gyldig", Assert.Single(await kontroll.Forslag.Where(f => f.Budsjettaar == 2026 && f.ForslagType == ForslagType.Generert).ToListAsync()).Tittel);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("10000")]
    [InlineData("tekst")]
    public Task Ugyldig_proeveaar_vises_aerlig_og_kan_ikke_beregnes_eller_foeres_til_generering(string ugyldig) => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 aar", "ux86-aar", "{\"maaned\":3,\"dag\":20}"));
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2026");
        await ForventDato(n, "UX86 aar", "2026-03-20");
        await Aar(n, ugyldig);
        await Expect(Felt(n, "Prøvebudsjettår")).ToHaveValueAsync(ugyldig);
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("1 til 9999");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt", Exact = true })).ToBeDisabledAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true })).ToBeDisabledAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Resultat for budsjettår 2026", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(foer, await Snapshot(n));
        if (ugyldig == "tekst") await Bilder(n, "ugyldig-aar");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Proeveaar_1_og_9999_og_store_representerbare_offset_beholder_domenets_grenser() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 grense", "ux86-grense", "{\"maaned\":1,\"dag\":1}"));
        await mal.OpprettAsync(Regel("UX86 stor gyldig offset", "ux86-stor", "{\"anker_loep\":\"ux86-grense\",\"offset_dager\":3652058}", Regeltype.RelativTilMilepael));
        var foer = await Snapshot(n);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=1");
        await ForventDato(n, "UX86 grense", "0001-01-01");
        await ForventDato(n, "UX86 stor gyldig offset", "9999-12-31");
        await Aar(n, "9999");
        await ForventDato(n, "UX86 grense", "9999-01-01");
        await Expect(Rad(n, "UX86 stor gyldig offset")).ToContainTextAsync("Kan ikke beregnes");
        await Expect(Rad(n, "UX86 stor gyldig offset").Locator("time")).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Resultat for budsjettår 9999", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(foer, await Snapshot(n));
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Helt_ugyldig_eller_tom_mal_erstatter_maalaar_med_tomt_sett_bare_etter_bekreftelse(bool tom) => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 erstattes", "ux86-erstattes", "{\"maaned\":3,\"dag\":20}"));
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            db.Forslag.AddRange(
                new Forslag { Id = Guid.NewGuid(), Tittel = "UX86 annet aar", Budsjettaar = 2033, Dato = new(2033, 3, 20), ForslagType = ForslagType.Generert },
                new Forslag { Id = Guid.NewGuid(), Tittel = "UX86 behandlet", Budsjettaar = 2032, Dato = new(2032, 3, 20), ForslagType = ForslagType.Generert, Status = FristStatus.Avvist },
                new Forslag { Id = Guid.NewGuid(), Tittel = "UX86 loepende", Budsjettaar = 2032, Dato = new(2032, 3, 20), ForslagType = ForslagType.NyFrist, Opphav = Opphav.Bruker });
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/generer?aar=2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 erstattes")).ToBeVisibleAsync();
        var gamle = await Aapne(n, 2032);
        Assert.Single(gamle);
        if (tom) foreach (var regel in await mal.HentAlleAsync()) await mal.SlettAsync(regel.Id);
        else
        {
            await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
            (await db.Gjentaksregler.SingleAsync()).Regelparametre = "ikke JSON";
            await db.SaveChangesAsync();
        }
        var foer = await Snapshot(n);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync("åpne genererte forslag");
        await Bilder(n, tom ? "tom-erstatt-dialog" : "ugyldig-erstatt-dialog");
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeFocusedAsync();
        Assert.Equal(foer, await Snapshot(n));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        Assert.Equal(foer, await Snapshot(n));
        Assert.Equal(gamle, await Aapne(n, 2032));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("Genererte 0 forslag for budsjettår 2032.", new() { Exact = false })).ToBeVisibleAsync();
        Assert.Empty(await Aapne(n, 2032));
        Assert.Equal(UtenAapne(foer, 2032), UtenAapne(await Snapshot(n), 2032));
        await Bilder(n, tom ? "tom-erstattet" : "ugyldig-erstattet");
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Ett_generert_forslag_publiseres_en_gang_med_aktiv_POL_og_samme_ID_kategori_og_presisjon_i_alle_visninger(bool maaned) => Nettleser.Kjor(async n =>
    {
        const string navn = "UX86 eneste publiserte";
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel(navn, "ux86-publiser", maaned ? "{\"maaned\":11,\"dag\":20,\"aar_forskyvning\":-1}" : "{\"maaned\":3,\"dag\":20}", kategori: maaned ? Kategori.Gulbok : Kategori.Budsjett, sensitiv: maaned));
        await mal.OpprettAsync(Regel("UX86 skal fortsatt vente", "ux86-venter", "{\"maaned\":4,\"dag\":12}"));
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = "UX86-INAKTIV", Navn = "Historisk gruppe", Aktiv = false });
            var fjor = new Frist { Id = Guid.NewGuid(), Tittel = "UX86 faktisk fjoraar", Loep = "UX86-PUBLISER", Budsjettaar = 2025, Dato = new(2024, 11, 15), Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio, Status = FristStatus.Godkjent };
            foreach (var kode in new[] { "FA", "POL", "UX86-INAKTIV" }) fjor.Synlighet.Add(new() { GruppeKode = kode });
            db.Frister.Add(fjor);
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        var foer = (await n.Frister()).Select(f => (string)f!["id"]!).ToHashSet();
        await n.Gaa("/admin/generer?aar=2026");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        var kort = Rad(n, navn);
        await Expect(kort).ToContainTextAsync("medio november 2024");
        await Expect(kort).Not.ToContainTextAsync("15.11.2024");
        await Expect(kort).ToContainTextAsync("UX86-INAKTIV");
        var forslagId = Guid.Parse((await kort.GetAttributeAsync("data-forslag-id"))!);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(Gruppe(kort, "FA")).ToBeCheckedAsync();
        await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
        await Expect(Gruppe(kort, "UX86-INAKTIV")).ToHaveCountAsync(0);
        await Gruppe(kort, "POL").CheckAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
        if (maaned)
        {
            await Expect(kort.Locator("time").First).ToHaveAttributeAsync("datetime", "2025-11");
            await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true })).ToBeDisabledAsync();
            await kort.GetByRole(AriaRole.Checkbox, new() { Name = "Jeg bekrefter å godkjenne fristen uten en fastsatt dag (tentativ).", Exact = true }).CheckAsync();
        }
        await Gruppe(kort, "POL").CheckAsync();
        await Bilder(n, maaned ? "godkjenn-tentativ" : "godkjenn-dag");
        // Allerede køede klikk må heller ikke publisere samme forslag to ganger.
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == navn)!;
        var id = (string)publisert["id"]!;
        Assert.DoesNotContain(id, foer);
        Assert.NotEqual(forslagId.ToString(), id);
        Assert.Equal(foer.Count + 1, (await n.Frister()).Count);
        Assert.Equal((int)(maaned ? Kategori.Gulbok : Kategori.Budsjett), (int)publisert["kategori"]!);
        Assert.Equal((int)(maaned ? Datopresisjon.Maaned : Datopresisjon.Dag), (int)publisert["datopresisjon"]!);
        Assert.Equal(new[] { "FA", "POL" }, publisert["synlighet"]!.AsArray().Select(g => (string)g!).Order().ToArray());
        Assert.Single(await Aapne(n, 2026));
        await KalenderforslagTester.KontrollerVisninger(n, publisert, maaned ? "november 2025" : "20. mars 2026", maaned ? "Gul bok" : "Budsjett");
        await n.LoggInn("demo-fa");
        Assert.Equal(id, (string?)Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == navn)!["id"]);
        await n.LoggInn("demo-fag");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == navn);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Regenerering_avviser_gammelt_aapent_kort_med_faktisk_forslag_ID_uten_publisering() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 foreldet", "ux86-foreldet", "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/generer?aar=2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 foreldet")).ToBeVisibleAsync();
        var gammeltId = Assert.Single(await Aapne(n, 2032));
        var gammelSide = n.Side;
        await Rad(n, "UX86 foreldet").GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/generer?aar=2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 foreldet")).ToBeVisibleAsync();
        await Expect(Rad(n, "UX86 foreldet")).Not.ToHaveAttributeAsync("data-forslag-id", gammeltId.ToString());
        var nyttId = Assert.Single(await Aapne(n, 2032));
        Assert.NotEqual(gammeltId, nyttId);
        n.Side = gammelSide;
        var kort = Rad(n, "UX86 foreldet");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Alert)).ToContainTextAsync("behandlet");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "UX86 foreldet");
        Assert.Equal(new[] { nyttId }, await Aapne(n, 2032));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Samme_arbeidsaar_og_midlertidige_datoer_beholdes_ved_justerretur_mens_aarsbytte_lukker_POL_kort() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 aarskontekst", "ux86-kontekst", "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/frister?aar=2032");
        await n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon", Exact = true }).GetByRole(AriaRole.Link, new() { Name = "Generer fra årsmal", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Generér budsjettår", Exact = true })).ToBeVisibleAsync();
        await Expect(Felt(n, "Målbudsjettår")).ToHaveValueAsync("2032");
        await Dato(n, "ux86-kontekst", "2032-04-05");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        var kort = Rad(n, "UX86 aarskontekst");
        await Expect(kort).ToContainTextAsync("05.04.2032");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Gruppe(kort, "POL").CheckAsync();
        await Aar(n, "2033", proev: false);
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2033", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.Locator("article.kort")).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("forslag for budsjettår 2032", new() { Exact = false })).ToHaveCountAsync(0);
        await Expect(Felt(n, "Midlertidig dato for ux86-kontekst")).ToHaveValueAsync("");
        await Aar(n, "2032", proev: false);
        await Expect(kort).ToContainTextAsync("05.04.2032");
        await Expect(Felt(n, "Midlertidig dato for ux86-kontekst")).ToHaveValueAsync("2032-04-05");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Juster", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Juster forslag", Exact = true })).ToBeVisibleAsync();
        await Expect(Felt(n, "Budsjettår")).ToHaveValueAsync("2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(n.Side).ToHaveURLAsync(n.Adresse + "/admin/generer?aar=2032");
        await Expect(Felt(n, "Midlertidig dato for ux86-kontekst")).ToHaveValueAsync("2032-04-05");
        await Expect(kort).ToContainTextAsync("05.04.2032");
        await Aar(n, "tekst", proev: false);
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Resultatene gjelder fortsatt budsjettår 2032");
        await Expect(kort).ToContainTextAsync("05.04.2032");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeDisabledAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Regelredigering_fra_proeve_beholder_aar_ankre_og_listens_filterkontekst_uten_ekstra_lagring() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 retur A", "ux86-retur-a", "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("UX86 retur B", "ux86-retur-b", "{\"anker_loep\":\"ux86-retur-a\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Felt(n, "Søk i regler").FillAsync("UX86 retur B");
        await Felt(n, "Sortering").SelectOptionAsync("Senest");
        await Felt(n, "Sammenheng").SelectOptionAsync("Avhengig");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Prøv malen", Exact = true }).ClickAsync();
        await Aar(n, "2032");
        await Dato(n, "ux86-retur-a", "2032-04-05");
        await ForventDato(n, "UX86 retur B", "2032-04-06");
        var foer = await Snapshot(n);
        await Rad(n, "UX86 retur B").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Tittel")).ToHaveValueAsync("UX86 retur B");
        await Felt(n, "Antall kalenderdager").FillAsync("2");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Prøv malen", Exact = true })).ToBeVisibleAsync();
        await Expect(Felt(n, "Prøvebudsjettår")).ToHaveValueAsync("2032");
        await Expect(Felt(n, "Midlertidig dato for ux86-retur-a")).ToHaveValueAsync("2032-04-05");
        await ForventDato(n, "UX86 retur B", "2032-04-07");
        await Rad(n, "UX86 retur A").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Tittel")).ToHaveValueAsync("UX86 retur A");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Prøvebudsjettår")).ToHaveValueAsync("2032");
        var etter = await Snapshot(n);
        Assert.Equal(UtenRegler(foer), UtenRegler(etter));
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Eksisterende gjentaksregler", Exact = true }).ClickAsync();
        await Expect(Felt(n, "Søk i regler")).ToHaveValueAsync("UX86 retur B");
        await Expect(Felt(n, "Sortering")).ToHaveValueAsync("Senest");
        await Expect(Felt(n, "Sammenheng")).ToHaveValueAsync("Avhengig");
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Proeverute_og_genereringsrute_avvises_paa_server_for_leser_bidrag_og_anonym() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 hemmelig mal", "ux86-hemmelig", "{\"maaned\":3,\"dag\":20}"));
        var foer = await Snapshot(n);
        foreach (var persona in new[] { null, "demo-fag", "demo-fa", "demo-finfag" })
        {
            await n.LoggInn(persona);
            foreach (var sti in new[] { "/admin/mal/proev?aar=2032", "/admin/generer?aar=2032" })
            {
                var svar = await n.Side.Context.APIRequest.GetAsync(sti, new() { MaxRedirects = 0 });
                Assert.Contains(svar.Status, new[] { 302, 403 });
                Assert.DoesNotContain("UX86 hemmelig mal", await svar.TextAsync());
                await n.Side.GotoAsync(sti);
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være", Exact = true })).ToBeVisibleAsync();
                await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
            }
        }
        Assert.Equal(foer, await Snapshot(n));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Teknisk_genereringsfeil_beholder_forslag_og_anker_og_gir_trygt_nytt_forsoek() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 lagringsfeil", "ux86-feil", "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/generer?aar=2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 lagringsfeil")).ToBeVisibleAsync();
        var gammelId = Assert.Single(await Aapne(n, 2032));
        var foer = await Snapshot(n);
        await Dato(n, "ux86-feil", "2032-04-05");
        await using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        await using var kommando = db.CreateCommand();
        kommando.CommandText = "CREATE TRIGGER UX86_generering AFTER INSERT ON Forslag WHEN NEW.Budsjettaar = 2032 BEGIN SELECT RAISE(ABORT, 'UX86-hemmelig-SQL-detalj'); END;";
        await kommando.ExecuteNonQueryAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Prøv igjen");
        await Expect(n.Side.GetByText("Genererte 1 forslag for budsjettår 2032.", new() { Exact = false })).ToHaveCountAsync(0);
        await Expect(Felt(n, "Midlertidig dato for ux86-feil")).ToHaveValueAsync("2032-04-05");
        Assert.DoesNotContain("UX86-hemmelig-SQL-detalj", await n.Side.ContentAsync());
        Assert.Equal(foer, await Snapshot(n));
        Assert.Equal(new[] { gammelId }, await Aapne(n, 2032));
        await Bilder(n, "teknisk-feil");
        kommando.CommandText = "DROP TRIGGER UX86_generering;";
        await kommando.ExecuteNonQueryAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Beregn og erstatt", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX86 lagringsfeil")).ToContainTextAsync("05.04.2032");
        Assert.NotEqual(gammelId, Assert.Single(await Aapne(n, 2032)));
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "UX86 lagringsfeil");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public async Task Paagaaende_generering_og_godkjenning_deaktiverer_handlinger_og_allerede_koede_klikk_gir_ett_resultat()
    {
        await using var app = new SkriveportApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var mal = scope.ServiceProvider.GetRequiredService<IMaltjeneste>();
        foreach (var regel in await mal.HentAlleAsync()) await mal.SlettAsync(regel.Id);
        await mal.OpprettAsync(Regel("UX86 venter", "ux86-vent", "{\"maaned\":3,\"dag\":20}"));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, e) => feil.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") feil.Enqueue(e.Text); };
        await side.GotoAsync("/admin/generer?aar=2032");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeEnabledAsync();
        // SQLite har synkron låseventing. En kontrollert async databaseport lar den
        // reelle Blazor-handleren rendre ventetilstanden før det faktiske EF-skrivet.
        app.Port.ForsinkNeste();
        try
        {
            await side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Beregner …", Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Midlertidig dato for ux86-vent", new() { Exact = true })).ToBeDisabledAsync();
        }
        finally { app.Port.Fortsett.TrySetResult(); }
        var kort = side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX86 venter" });
        await Expect(kort).ToBeVisibleAsync();
        var generering = scope.ServiceProvider.GetRequiredService<IGenereringstjeneste>();
        Assert.Single(await generering.HentGenererteAsync(2032));
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        app.Port.ForsinkNeste();
        try
        {
            await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenner …", Exact = true })).ToBeDisabledAsync();
            await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(Gruppe(kort, "POL")).ToBeDisabledAsync();
        }
        finally { app.Port.Fortsett.TrySetResult(); }
        await Expect(kort).ToHaveCountAsync(0);
        var svar = await context.APIRequest.GetAsync("/api/frister?historikk=true");
        Assert.Equal(200, svar.Status);
        Assert.Single(JsonNode.Parse(await svar.TextAsync())!.AsArray(), f => (string?)f!["tittel"] == "UX86 venter");
        Assert.Empty(await generering.HentGenererteAsync(2032));
        Assert.Empty(feil);
    }

    [Theory]
    [InlineData("generer", "aar")]
    [InlineData("generer", "tilbake")]
    [InlineData("generer", "rute")]
    [InlineData("godkjenn", "aar")]
    [InlineData("godkjenn", "tilbake")]
    [InlineData("godkjenn", "rute")]
    [InlineData("avvis", "aar")]
    [InlineData("avvis", "tilbake")]
    [InlineData("avvis", "rute")]
    public async Task Aarsvalg_koede_handlinger_og_navigasjon_venter_paa_paagaaende_skriving(string handling, string navigasjon)
    {
        await using var app = new SkriveportApplikasjon();
        using var klient = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var mal = scope.ServiceProvider.GetRequiredService<IMaltjeneste>();
        foreach (var regel in await mal.HentAlleAsync()) await mal.SlettAsync(regel.Id);
        await mal.OpprettAsync(Regel("UX86 aarsvern", "ux86-aarsvern", "{\"maaned\":3,\"dag\":20}"));
        var generering = scope.ServiceProvider.GetRequiredService<IGenereringstjeneste>();
        if (handling != "generer")
        {
            await mal.OpprettAsync(Regel("UX86 behold", "ux86-behold", "{\"maaned\":4,\"dag\":5}"));
            await generering.GenererAsync(new(2032, new Dictionary<string, DateOnly>()));
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = klient.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
            ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
        });
        var side = await context.NewPageAsync();
        var feil = new ConcurrentQueue<string>();
        side.PageError += (_, e) => feil.Enqueue(e);
        side.Console += (_, e) => { if (e.Type == "error") feil.Enqueue(e.Text); };
        await side.GotoAsync("/admin/generer?aar=2033");
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await NavigerGenerering(side, "/admin/generer?aar=2032");
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeEnabledAsync();
        var kort = side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX86 aarsvern" });
        var behold = side.GetByRole(AriaRole.Article).Filter(new() { HasText = "UX86 behold" });
        var foer = await generering.HentGenererteAsync(2032);
        if (handling != "generer")
            await kort.GetByRole(AriaRole.Button, new() { Name = handling == "godkjenn" ? "Godkjenn" : "Avvis", Exact = true }).ClickAsync();
        var start = handling switch { "godkjenn" => "Bekreft godkjenning", "avvis" => "Bekreft avvisning", _ => "Generér" };
        app.Port.ForsinkNeste();
        try
        {
            await side.GetByRole(AriaRole.Button, new() { Name = start, Exact = true }).EvaluateAsync("button => { button.click(); button.click(); }");
            await app.Port.Startet.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var aarvalg = side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true });
            var annet = side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true });
            var aapne = side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true });
            await Expect(aarvalg).ToBeDisabledAsync();
            await Expect(annet).ToBeDisabledAsync();
            await Expect(aapne).ToBeDisabledAsync();
            await Expect(side.GetByLabel("Målbudsjettår", new() { Exact = true })).ToBeDisabledAsync();

            // Også allerede køede hendelser må avvises på serveren. Send dem
            // forbi HTML-vernet; navigasjonsmeldingen nedenfor kvitterer for kretsen.
            await aarvalg.EvaluateAsync("el => { el.value = 'alle'; el.dispatchEvent(new Event('change', { bubbles: true })); }");
            await annet.EvaluateAsync("el => { el.value = '2033'; el.dispatchEvent(new Event('change', { bubbles: true })); }");
            await aapne.EvaluateAsync("el => el.form.requestSubmit()");
            var generer = side.GetByRole(AriaRole.Button, new() { Name = handling == "generer" ? "Beregner …" : "Generér", Exact = true });
            await generer.EvaluateAsync("el => { el.disabled = false; el.click(); el.disabled = true; }");
            if (handling != "generer")
            {
                foreach (var navn in new[] { "Godkjenn", "Avvis", "Juster" })
                {
                    var knapp = behold.GetByRole(AriaRole.Button, new() { Name = navn, Exact = true });
                    await Expect(knapp).ToBeDisabledAsync();
                    await knapp.EvaluateAsync("el => { el.disabled = false; el.click(); el.disabled = true; }");
                }
            }
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/generer\\?aar=2032$"));
            if (navigasjon == "tilbake") await side.EvaluateAsync("history.back()");
            else await NavigerGenerering(side, navigasjon == "aar" ? "/admin/generer?aar=2033" : "/admin/mal/proev");
            await Expect(side.GetByRole(AriaRole.Status).Filter(new() { HasText = "Vent til handlingen er fullført" })).ToBeVisibleAsync();
            await Expect(side).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/generer\\?aar=2032$"));
            await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2032", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(JsonSerializer.Serialize(foer), JsonSerializer.Serialize(await generering.HentGenererteAsync(2032)));
            Assert.Empty(await generering.HentGenererteAsync(2033));
            await Genereringsbilder(side, $"venter-{handling}-{navigasjon}");
        }
        finally { app.Port.Fortsett.TrySetResult(); }

        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeEnabledAsync();
        if (handling == "generer") await Expect(kort).ToBeVisibleAsync();
        else await Expect(kort).ToHaveCountAsync(0);
        var etter = await generering.HentGenererteAsync(2032);
        Assert.Single(etter);
        if (handling != "generer") Assert.Equal(JsonSerializer.Serialize(foer.Single(f => f.Tittel == "UX86 behold")), JsonSerializer.Serialize(etter.Single()));
        var svar = await context.APIRequest.GetAsync("/api/frister?historikk=true");
        Assert.Equal(200, svar.Status);
        Assert.Equal(handling == "godkjenn" ? 1 : 0, JsonNode.Parse(await svar.TextAsync())!.AsArray().Count(f => (string?)f!["tittel"] == "UX86 aarsvern"));
        await Expect(side.GetByRole(AriaRole.Combobox, new() { Name = "Arbeidsår", Exact = true })).ToBeEnabledAsync();
        await Expect(side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true })).ToBeEnabledAsync();
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true })).ToBeEnabledAsync();

        // Etter det opprinnelige skrivet kan samme krets bytte rute og komme
        // tilbake. En sen finally skal verken låse flaten eller fylle feil år.
        await NavigerGenerering(side, "/admin/mal/proev");
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Prøv malen", Exact = true })).ToBeVisibleAsync();
        await side.GoBackAsync();
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeEnabledAsync();
        await side.GetByLabel("Åpne annet arbeidsår", new() { Exact = true }).FillAsync("2033");
        await side.GetByRole(AriaRole.Button, new() { Name = "Åpne år", Exact = true }).ClickAsync();
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Budsjettår 2033", Exact = true })).ToBeVisibleAsync();
        await Expect(side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        await side.GoBackAsync();
        await Expect(side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true })).ToBeEnabledAsync();
        await Expect(side.GetByRole(AriaRole.Article)).ToHaveCountAsync(1);
        Assert.Equal(JsonSerializer.Serialize(etter), JsonSerializer.Serialize(await generering.HentGenererteAsync(2032)));
        Assert.Empty(await generering.HentGenererteAsync(2033));
        Assert.Empty(feil);
    }

    private static Task NavigerGenerering(IPage side, string uri) => side.EvaluateAsync("uri => { const a = document.createElement('a'); a.href = uri; document.body.append(a); a.click(); a.remove(); }", uri);

    private static async Task Genereringsbilder(IPage side, string navn)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "generering-aarsvern");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{navn}-{bredde}.png"), FullPage = true });
        }
        await side.SetViewportSizeAsync(1440, 1000);
    }

    [Fact]
    public Task Teknisk_proevefeil_beholder_datoer_gir_trygg_retry_og_tom_mal_er_aerlig_uten_skriving() => Nettleser.Kjor(async n =>
    {
        var mal = await TomMal(n);
        await mal.OpprettAsync(Regel("UX86 proevefeil", "ux86-proevefeil", "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/proev?aar=2032");
        await ForventDato(n, "UX86 proevefeil", "2032-03-19");
        await Dato(n, "ux86-proevefeil", "2032-04-05");
        await ForventDato(n, "UX86 proevefeil", "2032-04-05");
        var foer = await Snapshot(n);
        await using var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        await using var kommando = db.CreateCommand();
        // Disponibel databases reelle lesefeil; beregningstjenesten og Blazor er uendret.
        kommando.CommandText = "ALTER TABLE Gjentaksregler RENAME TO UX86_midlertidig_utilgjengelig;";
        await kommando.ExecuteNonQueryAsync();
        try
        {
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt", Exact = true }).ClickAsync();
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Ingen forslag er opprettet");
            await Expect(Felt(n, "Midlertidig dato for ux86-proevefeil")).ToHaveValueAsync("2032-04-05");
            Assert.DoesNotContain("no such table", await n.Side.ContentAsync());
            await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Fortsett til Generer fra årsmal", Exact = true })).ToBeDisabledAsync();
            await Bilder(n, "proeve-teknisk-feil");
        }
        finally
        {
            kommando.CommandText = "ALTER TABLE UX86_midlertidig_utilgjengelig RENAME TO Gjentaksregler;";
            await kommando.ExecuteNonQueryAsync();
        }
        Assert.Equal(foer, await Snapshot(n));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Prøv igjen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
        await ForventDato(n, "UX86 proevefeil", "2032-04-05");
        foreach (var regel in await mal.HentAlleAsync()) await mal.SlettAsync(regel.Id);
        var tom = await Snapshot(n);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Beregn på nytt", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Ingen regler i malen", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
        Assert.Equal(tom, await Snapshot(n));
        await Bilder(n, "proeve-tom");
        n.BekreftIngenKretsfeil();
    });

    private static IMaltjeneste Mal(Nettleser n) => new Maltjeneste(new DatabaseFactory(n.Mappe));
    private static async Task<IMaltjeneste> TomMal(Nettleser n)
    {
        var mal = Mal(n);
        foreach (var r in await mal.HentAlleAsync()) await mal.SlettAsync(r.Id);
        return mal;
    }
    private static MalRegelInndata Regel(string tittel, string loep, string json, Regeltype type = Regeltype.FastDato, Kategori kategori = Kategori.Budsjett, bool sensitiv = false)
        => new() { Tittel = tittel, Loep = loep, Regelparametre = json, Regeltype = type, Kategori = kategori, Valgaarssensitiv = sensitiv };
    private static ILocator Rad(Nettleser n, string navn) => n.Side.GetByRole(AriaRole.Article).Filter(new() { Has = n.Side.GetByRole(AriaRole.Heading, new() { Name = navn, Exact = true }) });
    private static ILocator Felt(Nettleser n, string navn) => n.Side.GetByLabel(navn, new() { Exact = true });
    private static ILocator Gruppe(ILocator kort, string kode) => kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({kode}\\)$") });
    private static async Task Aar(Nettleser n, string aar, bool proev = true)
    {
        var felt = Felt(n, proev ? "Prøvebudsjettår" : "Målbudsjettår");
        await felt.FillAsync(aar);
        await felt.PressAsync("Tab");
    }
    private static async Task Dato(Nettleser n, string loep, string dato)
    {
        var felt = Felt(n, "Midlertidig dato for " + loep);
        await felt.FillAsync(dato);
        await felt.PressAsync("Tab");
    }
    private static Task ForventDato(Nettleser n, string navn, string dato) => Expect(Rad(n, navn).Locator("time")).ToHaveAttributeAsync("datetime", dato);
    private static async Task<Guid[]> Aapne(Nettleser n, int aar)
    {
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        return await db.Forslag.Where(f => f.Budsjettaar == aar && f.ForslagType == ForslagType.Generert && f.Status == FristStatus.Forslag).OrderBy(f => f.Id).Select(f => f.Id).ToArrayAsync();
    }
    private static string UtenAapne(string json, int aar)
    {
        var snapshot = JsonNode.Parse(json)!.AsObject();
        var innhold = JsonNode.Parse(snapshot["Innhold"]!.GetValue<string>())!.AsObject();
        var behold = innhold["Forslag"]!.AsArray().Select(r => r!.GetValue<string>()).Where(r =>
        {
            var f = JsonNode.Parse(r)!;
            return f["Budsjettaar"]!.GetValue<int>() != aar || f["ForslagType"]!.GetValue<int>() != (int)ForslagType.Generert || f["Status"]!.GetValue<int>() != (int)FristStatus.Forslag;
        }).ToArray();
        innhold["Forslag"] = JsonSerializer.SerializeToNode(behold);
        snapshot["Innhold"] = innhold.ToJsonString();
        return snapshot.ToJsonString();
    }
    private static string UtenRegler(string json)
    {
        var snapshot = JsonNode.Parse(json)!.AsObject();
        var innhold = JsonNode.Parse(snapshot["Innhold"]!.GetValue<string>())!.AsObject();
        innhold.Remove("Regler");
        snapshot["Innhold"] = innhold.ToJsonString();
        return snapshot.ToJsonString();
    }
    private static async Task<string> Snapshot(Nettleser n)
    {
        var fabrikk = new DatabaseFactory(n.Mappe);
        await using var db = fabrikk.CreateDbContext();
        async Task<string[]> Rader<T>() where T : class => (await db.Set<T>().ToListAsync())
            .Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)))
            .Order(StringComparer.Ordinal).ToArray();
        return JsonSerializer.Serialize(new { Innhold = await FasedataBevis.HentAsync(fabrikk), Varsler = await Rader<Varsel>(), Grupper = await Rader<Synlighetsgruppe>() });
    }
    private static async Task Bilder(Nettleser n, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "malproeving");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await n.Side.SetViewportSizeAsync(1440, 1000);
    }

    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }
    private sealed class SkriveportApplikasjon() : TestApplikasjon(brukKestrel: true)
    {
        private Skriveport? _port;
        public Skriveport Port => _port ??= new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            var port = Port;
            builder.ConfigureServices(services => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(port)));
        }
    }
    private sealed class Skriveport : SaveChangesInterceptor
    {
        private int _forsink;
        public TaskCompletionSource Startet { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Fortsett { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void ForsinkNeste()
        {
            Startet = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _forsink, 1);
        }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _forsink, 0) == 1)
            {
                Startet.TrySetResult();
                await Fortsett.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
