using System.Text.Json;
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

public sealed class MalregelbyggerTester
{
    [Fact]
    public Task Mandag_fra_20_juli_viser_fem_aar_og_aapnes_igjen_uten_JSON() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK juli fra dato");
        await n.Side.GetByRole(AriaRole.Radio, new() { Name = "Ukedag fra og med en dato", Exact = true }).CheckAsync();
        await n.Side.GetByLabel("Fra og med dag", new() { Exact = true }).FillAsync("20");
        await n.Side.GetByLabel("Måned", new() { Exact = true }).SelectOptionAsync("7");
        await n.Side.GetByLabel("Ukedag", new() { Exact = true }).SelectOptionAsync("man");
        await n.Side.GetByLabel("Første budsjettår", new() { Exact = true }).FillAsync("2026");
        await n.Side.GetByLabel("Første budsjettår", new() { Exact = true }).PressAsync("Tab");
        var preview = n.Side.GetByRole(AriaRole.Table, new() { Name = "Forhåndsvisning for fem budsjettår" });
        foreach (var dato in new[] { "mandag 20. juli 2026", "mandag 26. juli 2027", "mandag 24. juli 2028", "mandag 23. juli 2029", "mandag 22. juli 2030" })
            await Expect(preview).ToContainTextAsync(dato);
        await Expect(n.Side.GetByLabel("Regelparametre (JSON)")).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        var rad = n.Side.GetByRole(AriaRole.Article).Filter(new() { Has = n.Side.GetByRole(AriaRole.Heading, new() { Name = "AK juli fra dato", Exact = true }) });
        await Expect(rad).ToBeVisibleAsync();
        await rad.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByLabel("Fra og med dag", new() { Exact = true })).ToHaveValueAsync("20");
        await Expect(n.Side.GetByLabel("Måned", new() { Exact = true })).ToHaveValueAsync("7");
        await Expect(n.Side.GetByLabel("Ukedag", new() { Exact = true })).ToHaveValueAsync("man");
        var lagret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Tittel == "AK juli fra dato");
        var p = Regelparser.RelativUkedag(lagret.Regelparametre);
        Assert.Equal(20, p.FraDag);
        Assert.Equal(7, p.Maaned);
        Assert.Equal("man", p.Ukedag);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Fast_dato_andre_og_femte_ukedag_beholder_alle_felt_ved_redigering_og_ny_sesjon() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var (navn, type, kategori, aar, maaned, dag, uke) in new[]
        {
            ("AK fast felt", "Fast dato", "Regnskap", "-2", "11", "20", ""),
            ("AK andre mandag", "Ukedag i måneden", "Gulbok", "2", "3", "", "2"),
            ("AK femte mandag", "Ukedag i måneden", "Budsjett", "0", "2", "", "5")
        })
        {
            await Ny(n, navn);
            await Velg(n.Side, type);
            await Felt(n.Side, "Kategori").SelectOptionAsync(kategori);
            await Felt(n.Side, "Måned").SelectOptionAsync(maaned);
            if (dag != "") await Felt(n.Side, "Dag").FillAsync(dag);
            else
            {
                await Felt(n.Side, "Forekomst").SelectOptionAsync(uke);
                await Expect(Felt(n.Side, "Ukedag").GetByRole(AriaRole.Option)).ToHaveCountAsync(7);
                await Felt(n.Side, "Ukedag").SelectOptionAsync("man");
            }
            await Felt(n.Side, "Kalenderår i forhold til budsjettåret").FillAsync(aar);
            await Felt(n.Side, "Valgårssensitiv").CheckAsync();
            await Aar(n.Side, "2026");
            if (uke == "5") await Expect(Preview(n.Side)).ToContainTextAsync("mandag 23. februar 2026");
            await Lagre(n.Side);
            var lagret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Tittel == navn);
            var url = $"/admin/mal/{lagret.Id}";
            await n.LoggInn("demo-admin");
            await n.Gaa(url);
            await Expect(n.Side.GetByRole(AriaRole.Radio, new() { Name = type, Exact = true })).ToBeCheckedAsync();
            await Expect(Felt(n.Side, "Kategori")).ToHaveValueAsync(kategori);
            await Expect(Felt(n.Side, "Kalenderår i forhold til budsjettåret")).ToHaveValueAsync(aar);
            await Expect(Felt(n.Side, "Valgårssensitiv")).ToBeCheckedAsync();
            await Expect(Felt(n.Side, "Måned")).ToHaveValueAsync(maaned);
            if (dag != "") await Expect(Felt(n.Side, "Dag")).ToHaveValueAsync(dag);
            else await Expect(Felt(n.Side, "Forekomst")).ToHaveValueAsync(uke);
            await Felt(n.Side, "Tittel").FillAsync(navn + " redigert");
            await Lagre(n.Side);
            var endret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Id == lagret.Id);
            Assert.Equal(lagret.Regelparametre, endret.Regelparametre);
            Assert.Equal(lagret.Loep, endret.Loep);
            Assert.Equal(lagret.Kategori, endret.Kategori);
            Assert.True(endret.Valgaarssensitiv);
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Skuddag_lagres_med_synlige_aarfeil_mens_umulige_og_ufullstendige_datoer_beholder_feltene() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await Ny(n, "AK skuddag");
        await Felt(n.Side, "Måned").SelectOptionAsync("2");
        await Felt(n.Side, "Dag").FillAsync("29");
        await Aar(n.Side, "2026");
        await Expect(Preview(n.Side)).ToContainTextAsync("tirsdag 29. februar 2028");
        await Expect(Preview(n.Side).GetByRole(AriaRole.Cell).Filter(new() { HasText = "Ugyldig måned/dag" })).ToHaveCountAsync(4);
        await Lagre(n.Side);
        var lagret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Tittel == "AK skuddag");
        await Kort(n.Side, "AK skuddag").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        foreach (var (maaned, dag) in new[] { ("2", "30"), ("4", "31"), ("4", "") })
        {
            await Felt(n.Side, "Måned").SelectOptionAsync(maaned);
            await Felt(n.Side, "Dag").FillAsync(dag);
            await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
            var feil = n.Side.GetByRole(AriaRole.Alert);
            await Expect(feil).ToContainTextAsync("Dagen finnes ikke");
            await Expect(feil).ToBeFocusedAsync();
            await Expect(Felt(n.Side, "Dag")).ToHaveValueAsync(dag);
            await Expect(Felt(n.Side, "Dag")).ToHaveAttributeAsync("aria-invalid", "true");
            Assert.Equal(lagret, Assert.Single(await Mal(n).HentAlleAsync(), r => r.Id == lagret.Id));
            await Expect(Preview(n.Side)).ToHaveCountAsync(0);
            await feil.GetByRole(AriaRole.Link).First.ClickAsync();
            await Expect(Felt(n.Side, "Dag")).ToBeFocusedAsync();
        }
        await Felt(n.Side, "Dag").FillAsync("29");
        await Felt(n.Side, "Måned").SelectOptionAsync("2");
        await Lagre(n.Side);
        Assert.Equal(lagret, Assert.Single(await Mal(n).HentAlleAsync(), r => r.Id == lagret.Id));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Alle_fire_valg_beholder_MinValue_MaxValue_ved_lagring_setning_og_gjenaapning() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        foreach (var type in new[] { "Fast dato", "Ukedag i måneden", "Ukedag fra og med en dato", "Før eller etter en milepæl" })
        foreach (var tall in new[] { int.MinValue, int.MaxValue })
        {
            var navn = $"AK {type} {tall}";
            await Ny(n, navn);
            await Velg(n.Side, type);
            if (type == "Før eller etter en milepæl")
            {
                await Felt(n.Side, "Ankerløp").FillAsync("manuelt-grenseanker");
                await Felt(n.Side, "Retning").SelectOptionAsync(tall < 0 ? "for" : "etter");
                await Felt(n.Side, "Antall kalenderdager").FillAsync(tall < 0 ? "2147483648" : "2147483647");
                await Expect(n.Side.GetByRole(AriaRole.Complementary, new() { Name = "Slik blir regelen" })).ToContainTextAsync(tall < 0 ? "2147483648" : "2147483647");
            }
            else
            {
                await Felt(n.Side, "Kalenderår i forhold til budsjettåret").FillAsync(tall.ToString());
                // Vent på serverens setning, ikke de fem radene fra forrige feltverdi.
                await Expect(n.Side.GetByRole(AriaRole.Complementary, new() { Name = "Slik blir regelen" })).ToContainTextAsync(tall < 0 ? "2147483648 år før" : "2147483647 år etter");
            }
            await Expect(Preview(n.Side).GetByRole(AriaRole.Cell)).ToHaveCountAsync(5);
            await Lagre(n.Side);
            var lagret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Tittel == navn);
            using (var parametre = JsonDocument.Parse(lagret.Regelparametre))
                Assert.Equal(tall, parametre.RootElement.GetProperty(type == "Før eller etter en milepæl" ? "offset_dager" : "aar_forskyvning").GetInt32());
            await n.Gaa($"/admin/mal/{lagret.Id}");
            if (type == "Før eller etter en milepæl")
            {
                await Expect(Felt(n.Side, "Retning")).ToHaveValueAsync(tall < 0 ? "for" : "etter");
                await Expect(Felt(n.Side, "Antall kalenderdager")).ToHaveValueAsync(tall < 0 ? "2147483648" : "2147483647");
            }
            else await Expect(Felt(n.Side, "Kalenderår i forhold til budsjettåret")).ToHaveValueAsync(tall.ToString());
            await Lagre(n.Side);
            Assert.Equal(lagret, Assert.Single(await Mal(n).HentAlleAsync(), r => r.Id == lagret.Id));
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Relative_felt_beholder_foer_etter_null_helg_manuelt_anker_og_kjedefeil() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        await mal.OpprettAsync(new() { Tittel = "AK anker", Loep = "ak-anker", Regelparametre = """{"maaned":3,"dag":20}""" });
        await mal.OpprettAsync(new() { Tittel = "AK mellomledd", Loep = "ak-mellom", Regeltype = Regeltype.RelativTilMilepael, Regelparametre = """{"anker_loep":"ak-anker","offset_dager":1}""" });
        await mal.OpprettAsync(new() { Tittel = "AK sirkel a", Loep = "ak-sirkel-a", Regeltype = Regeltype.RelativTilMilepael, Regelparametre = """{"anker_loep":"ak-sirkel-b","offset_dager":0}""" });
        await n.LoggInn("demo-admin");
        foreach (var (retning, antall, dato) in new[] { ("for", "7", "fredag 13. mars 2026"), ("etter", "1", "lørdag 21. mars 2026"), ("samme", "0", "fredag 20. mars 2026") })
        {
            var navn = "AK relativ " + retning;
            await Ny(n, navn);
            await Velg(n.Side, "Før eller etter en milepæl");
            await Felt(n.Side, "Ankerløp").FillAsync("ak-anker");
            await Felt(n.Side, "Retning").SelectOptionAsync(retning);
            if (retning != "samme") await Felt(n.Side, "Antall kalenderdager").FillAsync(antall);
            await Aar(n.Side, "2026");
            await Expect(Preview(n.Side)).ToContainTextAsync(dato);
            await Lagre(n.Side);
            var regel = Assert.Single(await mal.HentAlleAsync(), r => r.Tittel == navn);
            Assert.Equal(retning == "for" ? -7 : retning == "etter" ? 1 : 0, Regelparser.RelativTilMilepael(regel.Regelparametre).OffsetDager);
            await n.Gaa($"/admin/mal/{regel.Id}");
            await Expect(Felt(n.Side, "Ankerløp")).ToHaveValueAsync("ak-anker");
            await Expect(Felt(n.Side, "Retning")).ToHaveValueAsync(retning);
            await Lagre(n.Side);
        }
        await Ny(n, "AK kjede");
        await Velg(n.Side, "Før eller etter en milepæl");
        await Felt(n.Side, "Ankerløp").FillAsync("ak-mellom");
        await Felt(n.Side, "Retning").SelectOptionAsync("samme");
        await Aar(n.Side, "2026");
        await Expect(Preview(n.Side)).ToContainTextAsync("lørdag 21. mars 2026");
        await Felt(n.Side, "Ankerløp").FillAsync("fastsettes-manuelt");
        await Expect(Preview(n.Side)).ToContainTextAsync("Mangler anker-løp");
        await Expect(n.Side.GetByText("Dato fastsettes manuelt", new() { Exact = false })).ToBeVisibleAsync();
        await Lagre(n.Side);
        await Ny(n, "AK sirkel b");
        await Felt(n.Side, "Funksjon / løp").FillAsync("ak-sirkel-b");
        await Velg(n.Side, "Før eller etter en milepæl");
        await Felt(n.Side, "Ankerløp").FillAsync("ak-sirkel-a");
        await Expect(Preview(n.Side)).ToContainTextAsync("Sirkulær");
        await Lagre(n.Side);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Preview_med_valgaarskjede_skriver_ingenting_og_tegnar_maaned_uten_fastsatt_dag() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var mal = Mal(n);
        await mal.OpprettAsync(new() { Tittel = "AK valganker", Loep = "ak-valganker", Valgaarssensitiv = true, Regelparametre = """{"maaned":9,"dag":20,"aar_forskyvning":-1}""" });
        await mal.OpprettAsync(new() { Tittel = "AK valgmellom", Loep = "ak-valgmellom", Regeltype = Regeltype.RelativTilMilepael, Regelparametre = """{"anker_loep":"ak-valganker","offset_dager":0}""" });
        var foer = await FasedataBevis.HentAsync(fabrikk);
        await n.LoggInn("demo-admin");
        await Ny(n, "AK valgbarn");
        await Velg(n.Side, "Før eller etter en milepæl");
        await Felt(n.Side, "Ankerløp").FillAsync("ak-valgmellom");
        await Felt(n.Side, "Retning").SelectOptionAsync("samme");
        await Aar(n.Side, "2026");
        await Expect(Preview(n.Side).GetByRole(AriaRole.Row).Filter(new() { Has = n.Side.GetByRole(AriaRole.Rowheader, new() { Name = "2026", Exact = true }) })).ToContainTextAsync("september 2025 (tentativ)");
        await Expect(Preview(n.Side)).ToContainTextAsync("mandag 20. september 2027");
        Assert.Equal(foer, await FasedataBevis.HentAsync(fabrikk));
        await Lagre(n.Side);
        Assert.Equal(UtenRegler(foer), UtenRegler(await FasedataBevis.HentAsync(fabrikk)));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tittel_beholder_stabilt_loep_eksplisitt_endring_forklarer_koblinger_kopi_og_slett_er_avgrenset() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        var id = await mal.OpprettAsync(new() { Tittel = "AK stabil original", Loep = "stabil-original", Kategori = Kategori.Gulbok, Valgaarssensitiv = true, Regelparametre = """{"maaned":11,"dag":20,"aar_forskyvning":-2}""" });
        var barnId = await mal.OpprettAsync(new() { Tittel = "AK behold kobling", Loep = "barn", Regeltype = Regeltype.RelativTilMilepael, Regelparametre = """{"anker_loep":"stabil-original","offset_dager":0}""" });
        var foer = await mal.HentForRedigeringAsync(id);
        var barn = await mal.HentForRedigeringAsync(barnId);
        await n.LoggInn("demo-admin");
        await n.Gaa($"/admin/mal/{id}");
        await Felt(n.Side, "Tittel").FillAsync("AK ny tittel");
        await Expect(Felt(n.Side, "Funksjon / løp")).ToHaveValueAsync("stabil-original");
        await Felt(n.Side, "Funksjon / løp").FillAsync("eksplisitt-nytt");
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("1 ankerkobling(er)");
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("Koblingene omskrives ikke");
        await Lagre(n.Side);
        Assert.Equal(barn!.Regelparametre, (await mal.HentForRedigeringAsync(barnId))!.Regelparametre);
        await Kort(n.Side, "AK ny tittel").GetByRole(AriaRole.Link, new() { Name = "Kopier", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Kopier gjentaksregel", Exact = true })).ToBeVisibleAsync();
        await Expect(Felt(n.Side, "Kategori")).ToHaveValueAsync("Gulbok");
        await Expect(Felt(n.Side, "Valgårssensitiv")).ToBeCheckedAsync();
        await Expect(Felt(n.Side, "Kalenderår i forhold til budsjettåret")).ToHaveValueAsync("-2");
        await Felt(n.Side, "Tittel").FillAsync("AK lagret kopi");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre kopi", Exact = true }).ClickAsync();
        await Expect(Kort(n.Side, "AK lagret kopi")).ToBeVisibleAsync();
        var original = Assert.Single(await mal.HentAlleAsync(), r => r.Id == id);
        var kopi = Assert.Single(await mal.HentAlleAsync(), r => r.Tittel == "AK lagret kopi");
        Assert.NotEqual(id, kopi.Id);
        Assert.Equal(original.Regelparametre, kopi.Regelparametre);
        Assert.Equal("AK ny tittel", original.Tittel);
        await Kort(n.Side, "AK behold kobling").GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await Felt(n.Side, "Ankerløp").FillAsync("eksplisitt-nytt");
        await Lagre(n.Side);
        var slett = Kort(n.Side, "AK ny tittel").GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true });
        await slett.PressAsync("Enter");
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Slett gjentaksregel?", Exact = true });
        await Expect(dialog).ToContainTextAsync("AK ny tittel");
        await Expect(dialog).ToContainTextAsync("1 ankerkobling(er)");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeFocusedAsync();
        await dialog.PressAsync("Escape");
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(slett).ToBeFocusedAsync();
        Assert.NotNull(await mal.HentForRedigeringAsync(id));
        await slett.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Slett regelen", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status).Filter(new() { HasText = "er slettet" })).ToContainTextAsync("AK ny tittel");
        Assert.Null(await mal.HentForRedigeringAsync(id));
        Assert.NotNull(await mal.HentForRedigeringAsync(barnId));
        Assert.NotNull(await mal.HentForRedigeringAsync(kopi.Id));
        Assert.Equal(foer!.Regelparametre, kopi.Regelparametre);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Ulagret_utkast_bevares_ved_typbytte_returfane_hovednavigasjon_back_og_reload() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/mal");
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Ny regel", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Nye gjentaksregler", Exact = true })).ToBeVisibleAsync();
        await Felt(n.Side, "Tittel").FillAsync("AK behold utkast");
        await Felt(n.Side, "Dag").FillAsync("23");
        await Felt(n.Side, "Måned").SelectOptionAsync("11");
        await Velg(n.Side, "Ukedag fra og med en dato");
        await Felt(n.Side, "Fra og med dag").FillAsync("27");
        await Velg(n.Side, "Fast dato");
        await Expect(Felt(n.Side, "Dag")).ToHaveValueAsync("23");
        await Expect(Felt(n.Side, "Måned")).ToHaveValueAsync("11");
        await Velg(n.Side, "Ukedag fra og med en dato");
        await Expect(Felt(n.Side, "Fra og med dag")).ToHaveValueAsync("27");
        await Velg(n.Side, "Fast dato");
        foreach (var lenke in new[]
        {
            n.Side.GetByRole(AriaRole.Link, new() { Name = "Tilbake til regellisten", Exact = true }),
            n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Områdenavigasjon" }).GetByRole(AriaRole.Link, new() { Name = "Eksisterende gjentaksregler", Exact = true }),
            n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Hovedmeny" }).GetByRole(AriaRole.Link, new() { Name = "Kalender", Exact = true })
        })
        {
            await lenke.PressAsync("Enter");
            var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Ulagrede endringer", Exact = true });
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Fortsett å redigere", Exact = true })).ToBeFocusedAsync();
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Fortsett å redigere", Exact = true }).ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(lenke).ToBeFocusedAsync();
            await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("AK behold utkast");
            await Expect(Felt(n.Side, "Dag")).ToHaveValueAsync("23");
        }
        await n.Side.EvaluateAsync("history.back()");
        var forlat = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Ulagrede endringer", Exact = true });
        await Expect(forlat).ToBeVisibleAsync();
        await forlat.PressAsync("Escape");
        await Expect(forlat).Not.ToBeVisibleAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("AK behold utkast");
        var foerReload = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IDialog> avvis = async (_, d) => { foerReload.TrySetResult(d.Type); await d.DismissAsync(); };
        n.Side.Dialog += avvis;
        try
        {
            // Avvist beforeunload gir ingen load-hendelse som ReloadAsync kan vente på.
            await n.Side.EvaluateAsync("window.location.reload()");
            Assert.Equal("beforeunload", await foerReload.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("AK behold utkast");
        }
        finally { n.Side.Dialog -= avvis; }
        await n.Side.GetByRole(AriaRole.Link, new() { Name = "Tilbake til regellisten", Exact = true }).ClickAsync();
        await forlat.GetByRole(AriaRole.Button, new() { Name = "Forkast endringene", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true })).ToBeVisibleAsync();
        Assert.DoesNotContain(await Mal(n).HentAlleAsync(), r => r.Tittel == "AK behold utkast");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Lagrings_og_slettefeil_beholder_utkast_original_og_avhengige_til_nytt_forsok() => Nettleser.Kjor(async n =>
    {
        var fabrikk = Fabrikk(n);
        await FasedataBevis.LeggTilHistorikkAsync(fabrikk);
        var foer = await FasedataBevis.HentAsync(fabrikk);
        await n.LoggInn("demo-admin");
        await Ny(n, "AK behold ved lagringsfeil");
        await Felt(n.Side, "Dag").FillAsync("23");
        await Felt(n.Side, "Måned").SelectOptionAsync("11");
        await Felt(n.Side, "Kategori").SelectOptionAsync("Regnskap");
        await Felt(n.Side, "Valgårssensitiv").CheckAsync();
        await using (var db = fabrikk.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER ux84_lagringsfeil BEFORE INSERT ON Gjentaksregler BEGIN SELECT RAISE(ABORT, 'kontrollert testfeil'); END;");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Alle feltene er beholdt");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(Felt(n.Side, "Tittel")).ToHaveValueAsync("AK behold ved lagringsfeil");
        await Expect(Felt(n.Side, "Dag")).ToHaveValueAsync("23");
        await Expect(Felt(n.Side, "Måned")).ToHaveValueAsync("11");
        await Expect(Felt(n.Side, "Kategori")).ToHaveValueAsync("Regnskap");
        await Expect(Felt(n.Side, "Valgårssensitiv")).ToBeCheckedAsync();
        Assert.Equal(foer, await FasedataBevis.HentAsync(fabrikk));
        await using (var db = fabrikk.CreateDbContext()) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER ux84_lagringsfeil;");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).EvaluateAsync("el => { el.click(); el.click(); }");
        await Expect(Kort(n.Side, "AK behold ved lagringsfeil")).ToBeVisibleAsync();
        var lagret = Assert.Single(await Mal(n).HentAlleAsync(), r => r.Tittel == "AK behold ved lagringsfeil");
        Assert.Equal(UtenRegler(foer), UtenRegler(await FasedataBevis.HentAsync(fabrikk)));
        await using (var db = fabrikk.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER ux84_slettefeil BEFORE DELETE ON Gjentaksregler BEGIN SELECT RAISE(ABORT, 'kontrollert testfeil'); END;");
        await Kort(n.Side, lagret.Tittel).GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = "Slett gjentaksregel?", Exact = true });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Slett regelen", Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke slette");
        Assert.Equal(lagret, Assert.Single(await Mal(n).HentAlleAsync(), r => r.Id == lagret.Id));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
        await using (var db = fabrikk.CreateDbContext()) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER ux84_slettefeil;");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Eldre_fra_dag_med_inaktiv_uke_og_uleselig_regel_bevares_til_eksplisitt_korreksjon() => Nettleser.Kjor(async n =>
    {
        var mal = Mal(n);
        var json = """{"maaned":7,"ukedag":" Mandag ","fra_dag":20,"uke":99,"aar_forskyvning":-2}""";
        var id = await mal.OpprettAsync(new() { Tittel = "AK eldre datovindu", Loep = "eldre", Regeltype = Regeltype.RelativUkedag, Regelparametre = json });
        var ugyldig = Guid.NewGuid();
        await using (var db = Fabrikk(n).CreateDbContext())
        {
            db.Gjentaksregler.Add(new() { Id = ugyldig, Tittel = "AK uleselig regel", Loep = "uleselig", Regelparametre = "{" });
            await db.SaveChangesAsync();
        }
        await n.LoggInn("demo-admin");
        await n.Gaa($"/admin/mal/{id}");
        await Expect(n.Side.GetByRole(AriaRole.Radio, new() { Name = "Ukedag fra og med en dato", Exact = true })).ToBeCheckedAsync();
        await Expect(Felt(n.Side, "Fra og med dag")).ToHaveValueAsync("20");
        await Expect(Felt(n.Side, "Ukedag")).ToHaveValueAsync("man");
        await Felt(n.Side, "Tittel").FillAsync("AK eldre redigert");
        await Lagre(n.Side);
        Assert.Equal(json, (await mal.HentForRedigeringAsync(id))!.Regelparametre);
        await n.Gaa($"/admin/mal/{ugyldig}");
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Denne eldre regelen kan ikke leses", Exact = true })).ToBeVisibleAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true })).ToBeDisabledAsync();
        await Expect(Preview(n.Side)).ToHaveCountAsync(0);
        Assert.Equal("{", (await mal.HentForRedigeringAsync(ugyldig))!.Regelparametre);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Korriger regelen med felter", Exact = true }).ClickAsync();
        await Felt(n.Side, "Dag").FillAsync("20");
        await Felt(n.Side, "Måned").SelectOptionAsync("11");
        await Lagre(n.Side);
        var rettet = Regelparser.FastDato((await mal.HentForRedigeringAsync(ugyldig))!.Regelparametre);
        Assert.Equal(20, rettet.Dag);
        Assert.Equal(11, rettet.Maaned);
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Felt(IPage side, string navn) => side.GetByLabel(navn, new() { Exact = true });
    private static ILocator Preview(IPage side) => side.GetByRole(AriaRole.Table, new() { Name = "Forhåndsvisning for fem budsjettår", Exact = true });
    private static ILocator Kort(IPage side, string navn) => side.GetByRole(AriaRole.Article).Filter(new() { Has = side.GetByRole(AriaRole.Heading, new() { Name = navn, Exact = true }) });
    private static async Task Velg(IPage side, string navn)
    {
        await side.GetByRole(AriaRole.Radio, new() { Name = navn, Exact = true }).CheckAsync();
        // Felles års-/månedsfelt kan fortsatt høre til forrige valg frem til serverrender.
        await Expect(Felt(side, navn switch { "Fast dato" => "Dag", "Ukedag i måneden" => "Forekomst", "Ukedag fra og med en dato" => "Fra og med dag", _ => "Ankerløp" })).ToBeVisibleAsync();
    }
    private static async Task Ny(Nettleser n, string navn) { await n.Gaa("/admin/mal/ny"); await Felt(n.Side, "Tittel").FillAsync(navn); }
    private static async Task Aar(IPage side, string aar) { await Felt(side, "Første budsjettår").FillAsync(aar); await Felt(side, "Første budsjettår").PressAsync("Tab"); }
    private static async Task Lagre(IPage side)
    {
        var tittel = await Felt(side, "Tittel").InputValueAsync();
        await side.GetByRole(AriaRole.Button, new() { Name = "Lagre regel", Exact = true }).ClickAsync();
        var overskrift = side.GetByRole(AriaRole.Heading, new() { Name = "Eksisterende gjentaksregler", Exact = true });
        await Expect(overskrift).ToBeVisibleAsync();
        // UX10 returnerer til den synlige målregelen; skjult rad har egen fokusprøve.
        await Expect(Kort(side, tittel).GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToBeFocusedAsync();
    }
    private static string UtenRegler(string json) { var data = JsonNode.Parse(json)!.AsObject(); data.Remove("Regler"); return data.ToJsonString(); }
    private static IMaltjeneste Mal(Nettleser n) => new Maltjeneste(Fabrikk(n));
    private static IDbContextFactory<AppDbContext> Fabrikk(Nettleser n) => new Filfabrikk(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(n.Mappe, "demo.db")}").Options);
    private sealed class Filfabrikk(DbContextOptions<AppDbContext> opsjoner) : IDbContextFactory<AppDbContext> { public AppDbContext CreateDbContext() => new(opsjoner); }
}
