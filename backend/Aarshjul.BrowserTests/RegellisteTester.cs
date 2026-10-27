using System.Text.Json.Nodes;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Testkontrakter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class RegellisteTester
{
    [Fact]
    public Task Soek_kategoriunion_og_sammenheng_er_snitt_over_hele_malen() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        await mal.OpprettAsync(Regel("Prosess A", "prosess-a", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("Prosess B", "prosess-b", Kategori.Gulbok, "{\"anker_loep\":\"prosess-a\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Prosess C", "prosess-c", Kategori.Regnskap, "{\"anker_loep\":\"prosess-b\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Prosess selvstendig", "egen-ukedag", Kategori.Budsjett, "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}", Regeltype.RelativUkedag));
        var foer = await mal.HentAlleAsync();
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("  pRoSeSs  ");
        await Expect(Rader(n.Side)).ToHaveCountAsync(4);
        await Expect(Kort(n.Side, "Prosess A")).ToContainTextAsync("Milepæl");
        await Expect(Kort(n.Side, "Prosess B")).ToContainTextAsync("Avhengig");
        await Expect(Kort(n.Side, "Prosess B")).Not.ToContainTextAsync("Milepæl");
        await Expect(Kort(n.Side, "Prosess C")).ToContainTextAsync("Avhengig");
        await Expect(Kort(n.Side, "Prosess selvstendig")).ToContainTextAsync("Selvstendig");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Gul bok", Exact = true }).UncheckAsync();
        await Expect(Rader(n.Side)).ToHaveCountAsync(3);
        await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Avhengig");
        await Expect(Rader(n.Side)).ToHaveCountAsync(1);
        await Expect(Kort(n.Side, "Prosess C")).ToContainTextAsync("22. mars 2026");
        await Bilder(n, "kombinasjonsfilter");
        await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Milepael");
        await Expect(Rader(n.Side)).ToHaveCountAsync(1);
        await Expect(Kort(n.Side, "Prosess A")).ToContainTextAsync("Milepæl");
        await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Alle");
        await Felt(n.Side, "mal-soek").FillAsync("prosess-c");
        await Expect(Rader(n.Side)).ToHaveCountAsync(1);
        await Expect(Kort(n.Side, "Prosess C")).ToBeVisibleAsync();
        await Felt(n.Side, "mal-soek").FillAsync("fra og med");
        await Expect(Rader(n.Side)).ToHaveCountAsync(1);
        await Expect(Kort(n.Side, "Prosess selvstendig")).ToBeVisibleAsync();
        Assert.Equal(foer, await mal.HentAlleAsync());
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tomt_kategoriutvalg_gir_null_og_nullstilling_beholder_beregningsaar_og_sortering() => Nettleser.Kjor(async n =>
    {
        await Mal(n).OpprettAsync(Regel("Utvalg prøve", "utvalg", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2028");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Senest");
        await Felt(n.Side, "mal-soek").FillAsync("Utvalg");
        await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Selvstendig");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        foreach (var kategori in new[] { "Budsjett", "Gul bok", "Regnskap" })
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = kategori, Exact = true })).Not.ToBeCheckedAsync();
        await Expect(Rader(n.Side)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByText("Ingen kategorier er valgt", new() { Exact = true })).ToBeVisibleAsync();
        await Bilder(n, "ingen-kategorier");
        await Aar(n.Side, "2027");
        await Felt(n.Side, "mal-soek").FillAsync("");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Tittel");
        await Expect(Rader(n.Side)).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre", Exact = true }).ClickAsync();
        await Expect(Felt(n.Side, "mal-soek")).ToHaveValueAsync("");
        await Expect(Felt(n.Side, "mal-sammenheng")).ToHaveValueAsync("Alle");
        await Expect(Felt(n.Side, "mal-sortering")).ToHaveValueAsync("Tittel");
        await Expect(Felt(n.Side, "mal-aar")).ToHaveValueAsync("2027");
        foreach (var kategori in new[] { "Budsjett", "Gul bok", "Regnskap" })
            await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = kategori, Exact = true })).ToBeCheckedAsync();
        await Expect(Kort(n.Side, "Utvalg prøve")).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Kronologi_bruker_hele_kalenderaaret_og_norsk_tittel_ID_ved_like_datoer() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        foreach (var (navn, json) in new[]
        {
            ("Sortering Før", "{\"maaned\":12,\"dag\":20,\"aar_forskyvning\":-2}"),
            ("Sortering Mellom", "{\"maaned\":1,\"dag\":20,\"aar_forskyvning\":-1}"),
            ("Sortering Årsskifte", "{\"maaned\":12,\"dag\":31,\"aar_forskyvning\":-1}"),
            ("Sortering Etter", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":1}"),
            ("Sortering Å", "{\"maaned\":9,\"dag\":2}"), ("Sortering Ø", "{\"maaned\":9,\"dag\":2}"),
            ("Sortering Æ", "{\"maaned\":9,\"dag\":2}")
        }) await mal.OpprettAsync(Regel(navn, navn, Kategori.Budsjett, json));
        await mal.OpprettAsync(Regel("Sortering Nyttår", "nyttaar", Kategori.Regnskap, "{\"anker_loep\":\"Sortering Årsskifte\",\"offset_dager\":2}", Regeltype.RelativTilMilepael));
        var lav = Guid.Parse("00000000-0000-0000-0000-000000000085");
        var mellom = Guid.Parse("88888888-8888-8888-8888-888888888885");
        var hoey = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffff0085");
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            foreach (var id in new[] { hoey, mellom, lav }) db.Gjentaksregler.Add(new() { Id = id, Tittel = "Sortering Z", Loep = "tie-" + id, Regelparametre = "{\"maaned\":9,\"dag\":2}" });
            db.Gjentaksregler.Add(new() { Id = Guid.NewGuid(), Tittel = "", Loep = "Sortering Legacy", Regelparametre = "{\"maaned\":9,\"dag\":2}" });
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("Sortering");
        var midten = new[] { "Sortering Legacy", "Sortering Z", "Sortering Z", "Sortering Z", "Sortering Æ", "Sortering Ø", "Sortering Å" };
        await Rekkefolge(n.Side, ["Sortering Før", "Sortering Mellom", "Sortering Årsskifte", "Sortering Nyttår", .. midten, "Sortering Etter"]);
        await Expect(Kort(n.Side, "Sortering Før").Locator("time")).ToHaveAttributeAsync("datetime", "2024-12-20");
        await Expect(Kort(n.Side, "Sortering Mellom").Locator("time")).ToHaveAttributeAsync("datetime", "2025-01-20");
        await Expect(Kort(n.Side, "Sortering Etter").Locator("time")).ToHaveAttributeAsync("datetime", "2027-03-02");
        await Expect(Kort(n.Side, "Sortering Nyttår").Locator("time")).ToHaveAttributeAsync("datetime", "2026-01-02");
        await Bilder(n, "hele-kalenderaar");
        var forventetTies = new[] { lav.ToString("D"), mellom.ToString("D"), hoey.ToString("D") };
        Assert.Equal(forventetTies, await Kort(n.Side, "Sortering Z").EvaluateAllAsync<string[]>("rows => rows.map(row => row.dataset.regelId)"));
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Senest");
        await Rekkefolge(n.Side, ["Sortering Etter", .. midten, "Sortering Nyttår", "Sortering Årsskifte", "Sortering Mellom", "Sortering Før"]);
        Assert.Equal(forventetTies, await Kort(n.Side, "Sortering Z").EvaluateAllAsync<string[]>("rows => rows.map(row => row.dataset.regelId)"));
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Tittel");
        await Rekkefolge(n.Side, ["Sortering Etter", "Sortering Før", "Sortering Legacy", "Sortering Mellom", "Sortering Nyttår", "Sortering Z", "Sortering Z", "Sortering Z", "Sortering Æ", "Sortering Ø", "Sortering Å", "Sortering Årsskifte"]);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("Tidligst")]
    [InlineData("Senest")]
    public Task Tentative_maaneder_sorteres_paa_maaned_uten_oppdiktet_dag_og_arves_i_kjeder(string sortering) => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        await mal.OpprettAsync(Regel("Måned Z tidlig referanse", "maaned-z", Kategori.Budsjett, "{\"maaned\":9,\"dag\":2,\"aar_forskyvning\":-1}", sensitiv: true));
        await mal.OpprettAsync(Regel("Måned A sen referanse", "maaned-a", Kategori.Gulbok, "{\"maaned\":9,\"dag\":20,\"aar_forskyvning\":-1}", sensitiv: true));
        await mal.OpprettAsync(Regel("Måned B arvet", "maaned-b", Kategori.Regnskap, "{\"anker_loep\":\"maaned-a\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Måned D fast dag", "maaned-d", Kategori.Budsjett, "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":-1}"));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("Måned");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync(sortering);
        var tentative = new[] { "Måned A sen referanse", "Måned B arvet", "Måned Z tidlig referanse" };
        await Rekkefolge(n.Side, sortering == "Tidligst" ? [.. tentative, "Måned D fast dag"] : ["Måned D fast dag", .. tentative]);
        foreach (var navn in tentative)
        {
            var tid = Kort(n.Side, navn).Locator("time");
            await Expect(tid).ToHaveAttributeAsync("datetime", "2025-09");
            await Expect(tid).ToHaveTextAsync("september 2025 · tentativ");
            await Expect(tid).Not.ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("[0-9]+\\."));
        }
        await Bilder(n, "tentativ-" + sortering);
        await Aar(n.Side, "2028");
        await Expect(Kort(n.Side, "Måned A sen referanse").Locator("time")).ToHaveAttributeAsync("datetime", "2027-09-20");
        await Expect(Kort(n.Side, "Måned B arvet").Locator("time")).ToHaveAttributeAsync("datetime", "2027-09-21");
        await Expect(Kort(n.Side, "Måned A sen referanse")).ToContainTextAsync("Valgår");
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("Tidligst")]
    [InlineData("Senest")]
    public Task Uberegnbare_regler_og_kjedefeil_beholdes_sist_med_redigerbar_forklaring(string sortering) => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        await mal.OpprettAsync(Regel("Feil Z gyldig", "feil-gyldig", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("Feil C mangler", "feil-mangler", Kategori.Budsjett, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":0}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Feil D avhengig", "feil-barn", Kategori.Gulbok, "{\"anker_loep\":\"feil-mangler\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Feil E sirkel", "feil-sirkel", Kategori.Regnskap, "{\"anker_loep\":\"feil-sirkel\",\"offset_dager\":0}", Regeltype.RelativTilMilepael));
        await mal.OpprettAsync(Regel("Feil B skuddag", "feil-skuddag", Kategori.Budsjett, "{\"maaned\":2,\"dag\":29}"));
        await mal.OpprettAsync(Regel("Feil F overflyt", "feil-overflyt", Kategori.Budsjett, "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":2147483647}"));
        await using (var db = new DatabaseFactory(n.Mappe).CreateDbContext())
        {
            db.Gjentaksregler.Add(new() { Id = Guid.NewGuid(), Tittel = "Feil A JSON", Loep = "feil-json", Regelparametre = "{" });
            db.Gjentaksregler.Add(new() { Id = Guid.NewGuid(), Tittel = "Feil G type", Loep = "feil-type", Regeltype = (Regeltype)999, Regelparametre = "{}" });
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("Feil ");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync(sortering);
        var feilnavn = new[] { "Feil A JSON", "Feil B skuddag", "Feil C mangler", "Feil D avhengig", "Feil E sirkel", "Feil F overflyt", "Feil G type" };
        await Rekkefolge(n.Side, ["Feil Z gyldig", .. feilnavn]);
        foreach (var navn in feilnavn)
        {
            await Expect(Kort(n.Side, navn).Locator("time")).ToHaveCountAsync(0);
            await Expect(Kort(n.Side, navn).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToBeVisibleAsync();
        }
        await Expect(Kort(n.Side, "Feil C mangler")).ToContainTextAsync("Mangler anker");
        await Expect(Kort(n.Side, "Feil D avhengig")).ToContainTextAsync("Mangler anker");
        await Expect(Kort(n.Side, "Feil E sirkel")).ToContainTextAsync("Sirkulær");
        await Bilder(n, "beregningsfeil-" + sortering);
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Tittel");
        await Rekkefolge(n.Side, [.. feilnavn, "Feil Z gyldig"]);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(1, "0001-03-20")]
    [InlineData(9999, "9999-03-19")]
    public Task Enkeltaar_stoetter_datogrenser_og_ugyldig_felt_beholder_tydelig_siste_gyldige_resultat(int aar, string dato) => Nettleser.Kjor(async n =>
    {
        await Mal(n).OpprettAsync(Regel("Årsgrense", "aarsgrense", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        var foer = await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Felt(n.Side, "mal-soek").FillAsync("Årsgrense");
        await Aar(n.Side, aar.ToString());
        await Expect(Kort(n.Side, "Årsgrense").Locator("time")).ToHaveAttributeAsync("datetime", dato);
        foreach (var ugyldig in new[] { "", "0", "10000", "abc" })
        {
            await Aar(n.Side, ugyldig);
            await Expect(Felt(n.Side, "mal-aar")).ToHaveAttributeAsync("aria-invalid", "true");
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("heltall fra 1 til 9999");
            await Expect(Kort(n.Side, "Årsgrense").Locator("time")).ToHaveAttributeAsync("datetime", dato);
            await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync($"gjelder fortsatt budsjettår {aar}");
        }
        await Bilder(n, "ugyldig-aar-" + aar);
        Assert.Equal(foer, await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Duplikate_loep_beholder_faktisk_genereringsrepresentant_i_alle_visningssorteringer() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        await mal.OpprettAsync(Regel("Duplikat Z første", "duplikat-anker", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("Duplikat A andre", "DUPLIKAT-ANKER", Kategori.Gulbok, "{\"maaned\":4,\"dag\":17}"));
        await mal.OpprettAsync(Regel("Duplikat barn", "duplikat-barn", Kategori.Regnskap, "{\"anker_loep\":\"duplikat-anker\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        var foer = await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe));
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("Duplikat");
        foreach (var sortering in new[] { "Tidligst", "Senest", "Tittel" })
        {
            await Felt(n.Side, "mal-sortering").SelectOptionAsync(sortering);
            await Expect(Rader(n.Side)).ToHaveCountAsync(3);
            await Expect(Kort(n.Side, "Duplikat Z første")).ToContainTextAsync("Milepæl");
            await Expect(Kort(n.Side, "Duplikat A andre")).ToContainTextAsync("Selvstendig");
            await Expect(Kort(n.Side, "Duplikat barn").Locator("time")).ToHaveAttributeAsync("datetime", "2026-03-21");
            await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Avhengig");
            await Expect(Rader(n.Side)).ToHaveCountAsync(1);
            await Expect(Kort(n.Side, "Duplikat barn").Locator("time")).ToHaveAttributeAsync("datetime", "2026-03-21");
            await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Alle");
        }
        Assert.Equal(foer, await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Redigering_dialogretur_browser_back_og_skjult_lagret_rad_beholder_hele_listekonteksten() => Nettleser.Kjor(async n =>
    {
        var fabrikk = new DatabaseFactory(n.Mappe);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var mal = Mal(n);
        var id = await mal.OpprettAsync(Regel("Retur mål", "retur-maal", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        var foer = await FasedataBevis.HentAsync(fabrikk);
        var reglerFoer = await mal.HentAlleAsync();
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2028");
        await Felt(n.Side, "mal-soek").FillAsync("Retur mål");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Senest");
        await Felt(n.Side, "mal-sammenheng").SelectOptionAsync("Selvstendig");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Gul bok", Exact = true }).UncheckAsync();
        await Expect(Rader(n.Side)).ToHaveCountAsync(1);
        foreach (var (aapner, dialognavn) in new[] { ("Se datoer", "Datoer for regelen"), ("Se sammenhengen", "Regelens sammenheng") })
        {
            var knapp = Kort(n.Side, "Retur mål").GetByRole(AriaRole.Button, new() { Name = aapner, Exact = true });
            await knapp.FocusAsync();
            await knapp.PressAsync("Enter");
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = dialognavn, Exact = true });
            await Expect(dialog).ToBeVisibleAsync();
            if (aapner == "Se datoer") await Expect(dialog.GetByRole(AriaRole.Table)).ToContainTextAsync("2032");
            await Bilder(n, aapner == "Se datoer" ? "datodialog" : "sammenhengdialog");
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(knapp).ToBeFocusedAsync();
            await Returkontekst(n.Side);
        }
        var rediger = Kort(n.Side, "Retur mål").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true });
        await rediger.ClickAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("Retur mål");
        await n.Side.GoBackAsync();
        await Returkontekst(n.Side);
        await Expect(rediger).ToBeFocusedAsync();
        await rediger.ClickAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("Retur mål");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Returkontekst(n.Side);
        await Expect(rediger).ToBeFocusedAsync();
        await rediger.ClickAsync();
        await Felt(n.Side, "Tittel").FillAsync("Forkast dette");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        var forlat = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Ulagrede endringer", Exact = true });
        await Expect(forlat).ToBeVisibleAsync();
        await forlat.GetByRole(AriaRole.Button, new() { Name = "Fortsett å redigere", Exact = true }).ClickAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("Forkast dette");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await forlat.GetByRole(AriaRole.Button, new() { Name = "Forkast endringene", Exact = true }).ClickAsync();
        await Returkontekst(n.Side);
        Assert.Equal(foer, await FasedataBevis.HentAsync(fabrikk));
        await rediger.ClickAsync();
        await Felt(n.Side, "Tittel").FillAsync("Nytt navn uten treff");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Returkontekst(n.Side);
        await Expect(Rader(n.Side)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Status).Filter(new() { HasText = "skjult av de aktive filtrene" })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeFocusedAsync();
        await Bilder(n, "lagret-utenfor-filter");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Nullstill filtre for å finne regelen", Exact = true }).ClickAsync();
        await Expect(Kort(n.Side, "Nytt navn uten treff")).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status).Filter(new() { HasText = "skjult av de aktive filtrene" })).ToHaveCountAsync(0);
        var reglerEtter = await mal.HentAlleAsync();
        Assert.Equal(reglerFoer.Where(r => r.Id != id), reglerEtter.Where(r => r.Id != id));
        var original = Assert.Single(reglerFoer, r => r.Id == id);
        Assert.Equal(original with { Tittel = "Nytt navn uten treff" }, Assert.Single(reglerEtter, r => r.Id == id));
        Assert.Equal(UtenRegler(foer), UtenRegler(await FasedataBevis.HentAsync(fabrikk)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Direkte_regellisteruter_avviser_leser_og_bidragsytere_paa_serveren() => Nettleser.Kjor(async n =>
    {
        var id = await Mal(n).OpprettAsync(Regel("Beskyttet regel", "beskyttet", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        var foer = await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe));
        foreach (var persona in new[] { "demo-fag", "demo-fa", "demo-finfag" })
        {
            await n.LoggInn(persona);
            foreach (var sti in new[] { "/admin/mal", "/admin/mal/ny", $"/admin/mal/{id}" })
            {
                var svar = await n.Side.Context.APIRequest.GetAsync(sti, new() { MaxRedirects = 0 });
                Assert.Contains(svar.Status, new[] { 302, 403 });
                Assert.DoesNotContain("Beskyttet regel", await svar.TextAsync());
                await n.Side.GotoAsync(sti);
                await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være", Exact = true })).ToBeVisibleAsync();
                await Expect(Rader(n.Side)).ToHaveCountAsync(0);
            }
        }
        Assert.Equal(foer, await FasedataBevis.HentAsync(new DatabaseFactory(n.Mappe)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Lagret_dato_beregnes_paa_nytt_for_barn_og_bare_maalregelen_endres() => Nettleser.Kjor(async n =>
    {
        var fabrikk = new DatabaseFactory(n.Mappe);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var mal = Mal(n);
        var id = await mal.OpprettAsync(Regel("Oppdater A", "oppdater-a", Kategori.Budsjett, "{\"maaned\":3,\"dag\":20}"));
        await mal.OpprettAsync(Regel("Oppdater B", "oppdater-b", Kategori.Regnskap, "{\"anker_loep\":\"oppdater-a\",\"offset_dager\":1}", Regeltype.RelativTilMilepael));
        var reglerFoer = await mal.HentAlleAsync();
        var foer = await FasedataBevis.HentAsync(fabrikk);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Aar(n.Side, "2026");
        await Felt(n.Side, "mal-soek").FillAsync("Oppdater");
        await Felt(n.Side, "mal-sortering").SelectOptionAsync("Senest");
        await Expect(Kort(n.Side, "Oppdater B").Locator("time")).ToHaveAttributeAsync("datetime", "2026-03-21");
        await Kort(n.Side, "Oppdater A").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("Oppdater A");
        await Felt(n.Side, "Dag").FillAsync("23");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(Kort(n.Side, "Oppdater A").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToBeFocusedAsync();
        await Expect(Felt(n.Side, "mal-soek")).ToHaveValueAsync("Oppdater");
        await Expect(Felt(n.Side, "mal-sortering")).ToHaveValueAsync("Senest");
        await Expect(Felt(n.Side, "mal-aar")).ToHaveValueAsync("2026");
        await Rekkefolge(n.Side, ["Oppdater B", "Oppdater A"]);
        await Expect(Kort(n.Side, "Oppdater A").Locator("time")).ToHaveAttributeAsync("datetime", "2026-03-23");
        await Expect(Kort(n.Side, "Oppdater B").Locator("time")).ToHaveAttributeAsync("datetime", "2026-03-24");
        var reglerEtter = await mal.HentAlleAsync();
        Assert.Equal(reglerFoer.Where(r => r.Id != id), reglerEtter.Where(r => r.Id != id));
        var endret = Assert.Single(reglerEtter, r => r.Id == id);
        Assert.Equal(23, Regelparser.FastDato(endret.Regelparametre).Dag);
        Assert.Equal(3, Regelparser.FastDato(endret.Regelparametre).Maaned);
        Assert.Equal(UtenRegler(foer), UtenRegler(await FasedataBevis.HentAsync(fabrikk)));
        await Bilder(n, "lagret-dato");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tom_mal_og_vanlig_nulltreff_har_ulike_forklaringer_og_virksomme_innganger() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await Felt(n.Side, "mal-soek").FillAsync("finnes-ikke-85");
        await Expect(Rader(n.Side)).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Ingen regler passer til valgene", Exact = true })).ToBeVisibleAsync();
        await Bilder(n, "ingen-treff");
        foreach (var regel in await Mal(n).HentAlleAsync()) await Mal(n).SlettAsync(regel.Id);
        await n.Gaa("/admin/mal");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Ingen regler i malen ennå", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Link, new() { Name = "Opprett gjentaksregel", Exact = true })).ToHaveAttributeAsync("href", "/admin/mal/ny");
        await Bilder(n, "tom-mal");
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Felt(IPage side, string id) => side.GetByLabel(id switch
    {
        "mal-soek" => "Søk i regler", "mal-sammenheng" => "Sammenheng", "mal-sortering" => "Sortering", "mal-aar" => "Beregn datoer for budsjettår", _ => id
    }, new() { Exact = true });
    private static ILocator Rader(IPage side) => side.GetByRole(AriaRole.Article);
    private static ILocator Kort(IPage side, string navn) => Rader(side).Filter(new() { Has = side.GetByRole(AriaRole.Heading, new() { Name = navn, Exact = true }) });
    private static MalRegelInndata Regel(string tittel, string loep, Kategori kategori, string json, Regeltype type = Regeltype.FastDato, bool sensitiv = false)
        => new() { Tittel = tittel, Loep = loep, Kategori = kategori, Regeltype = type, Regelparametre = json, Valgaarssensitiv = sensitiv };
    private static IMaltjeneste Mal(Nettleser n) => new Maltjeneste(new DatabaseFactory(n.Mappe));
    private static async Task Aar(IPage side, string aar) { await Felt(side, "mal-aar").FillAsync(aar); await Felt(side, "mal-aar").PressAsync("Tab"); }
    private static Task Rekkefolge(IPage side, string[] navn) => Expect(Rader(side).GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(navn);
    private static async Task Returkontekst(IPage side)
    {
        await Expect(side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
        await Expect(Felt(side, "mal-soek")).ToHaveValueAsync("Retur mål");
        await Expect(Felt(side, "mal-aar")).ToHaveValueAsync("2028");
        await Expect(Felt(side, "mal-sortering")).ToHaveValueAsync("Senest");
        await Expect(Felt(side, "mal-sammenheng")).ToHaveValueAsync("Selvstendig");
        await Expect(side.GetByRole(AriaRole.Checkbox, new() { Name = "Gul bok", Exact = true })).Not.ToBeCheckedAsync();
        await Expect(side.GetByRole(AriaRole.Checkbox, new() { Name = "Budsjett", Exact = true })).ToBeCheckedAsync();
        await Expect(side.GetByRole(AriaRole.Checkbox, new() { Name = "Regnskap", Exact = true })).ToBeCheckedAsync();
    }
    private static async Task Bilder(Nettleser n, string tilstand)
    {
        if (Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE") is not { } rot) return;
        var mappe = Path.Combine(rot, "regelliste");
        Directory.CreateDirectory(mappe);
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, bredde == 390 ? 844 : 1000);
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(mappe, $"{tilstand}-{bredde}.png"), FullPage = true });
        }
        await n.Side.SetViewportSizeAsync(1440, 1000);
    }
    private static string UtenRegler(string json) { var data = JsonNode.Parse(json)!.AsObject(); data.Remove("Regler"); return data.ToJsonString(); }
    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }
}
