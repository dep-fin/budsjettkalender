using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class InternkalenderAtelierTester
{
    [Theory]
    [InlineData("Marsrunden", "Marsrunden 2036", "2035-02-15")]
    [InlineData("Augustrunden", "Augustrunden 2036", "2035-02-15")]
    [InlineData("Rnb", "RNB (revidert nasjonalbudsjett) 2036", "2036-02-15")]
    [InlineData("Nysaldering", "Nysalderingen 2036", "2036-02-15")]
    [InlineData("Regnskap", "Regnskap 2036", "2036-02-15")]
    public Task Alle_rundetyper_har_tom_og_generert_plan_aar_duplikat_aapning_og_avbryt_slett(string type, string etikett, string dato) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var regler = Regler(n);
        await Toem(n);
        await regler.OpprettAsync(new() { Tittel = "UX16 generert år", Rundetyper = [Enum.Parse<Rundetype>(type)],
            Tidfestingstype = Tidfestingstype.KonkretDato, Maaned = 2, Dag = 15 });
        await n.Gaa("/internkalender");
        await Bilder(n, "oversikt-tom-" + type);
        await OpprettRunde(n, type, "2035", false);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen aktive gjøremål");
        await n.Gaa("/internkalender");
        await OpprettRunde(n, type, "2036", true);
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(etikett);
        var id = Guid.Parse(new Uri(n.Side.Url).Segments.Last());
        var lagret = Assert.Single(await Kalender(n).HentGjoeremaalAsync(id, null));
        Assert.Equal(DateOnly.Parse(dato), lagret.Sorteringsdag);
        await Expect(Rad(n, "UX16 generert år")).ToBeVisibleAsync();
        await Bilder(n, "runde-generert-" + type);
        await n.Gaa("/internkalender");
        await OpprettRunde(n, type, "2036", true, forventNavigasjon: false);
        await Expect(n.Side.Locator("main")).ToContainTextAsync("finnes allerede");
        await Bilder(n, "runde-duplikat-" + type);
        await n.Side.GetByRole(AriaRole.Link, new() { Name = etikett, Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync(etikett);
        await n.Gaa("/internkalender");
        var slett = n.Side.GetByRole(AriaRole.Button, new() { Name = "Slett " + etikett, Exact = true });
        await slett.ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToContainTextAsync("gjøremål");
        await Bilder(n, "runde-slett-dialog-" + type);
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Expect(slett).ToBeFocusedAsync();
        Assert.NotNull(await Kalender(n).HentRundeAsync(id));
        await slett.ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, slett", Exact = true }).ClickAsync();
        await Expect(slett).ToHaveCountAsync(0);
        Assert.Null(await Kalender(n).HentRundeAsync(id));
        Assert.Empty(await Kalender(n).HentGjoeremaalAsync(id, null));
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("Ingen", "", "Uten dato")]
    [InlineData("KonkretDato", "", "15.02.2036")]
    [InlineData("TentativMaaned", "Primo", "primo februar 2036")]
    [InlineData("TentativMaaned", "Medio", "medio februar 2036")]
    [InlineData("TentativMaaned", "Ultimo", "ultimo februar 2036")]
    [InlineData("AnkerRelativ", "", "Venter på ankerdato")]
    [InlineData("Rundeposisjon", "Start", "Start")]
    [InlineData("Rundeposisjon", "Tidlig", "Tidlig")]
    [InlineData("Rundeposisjon", "Midt", "Midt")]
    [InlineData("Rundeposisjon", "Sent", "Sent")]
    [InlineData("Rundeposisjon", "Slutt", "Slutt")]
    public Task Fullt_gjoeremaal_bevarer_alle_tidfestingstyper_kvalifikatorer_posisjoner_og_ansvar(string type, string valg, string tekst) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var kalender = Kalender(n);
        var runde = await kalender.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        await n.Gaa($"/internkalender/gjoeremaal/ny/{runde}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 fullskjema " + type + valg);
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Internt notat <behold> & ansvar");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true }).SelectOptionAsync(type);
        if (type == "KonkretDato") await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2036-02-15");
        if (type == "TentativMaaned")
        {
            await n.Side.GetByLabel("Måned (velg en dato i måneden)", new() { Exact = true }).FillAsync("2036-02-15");
            await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync(valg);
        }
        if (type == "AnkerRelativ")
        {
            await n.Side.GetByLabel("Anker-løp (publisert frist)", new() { Exact = true }).FillAsync("ux16-mangler");
            await n.Side.GetByLabel("Dager forskyvning", new() { Exact = false }).FillAsync("-3");
        }
        if (type == "Rundeposisjon") await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Posisjon", Exact = true }).SelectOptionAsync(valg);
        await LeggTilAnsvarlig(n, "demo-sbr");
        await LeggTilAnsvarlig(n, "demo-admin");
        await n.Side.GetByLabel("Legg til som fritekst", new() { Exact = true }).FillAsync("Sivert SBR");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Legg til navn", Exact = true }).ClickAsync();
        await Bilder(n, "gjoeremaal-editor-" + type + valg);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        var rad = Rad(n, "UX16 fullskjema " + type + valg);
        await Expect(rad).ToContainTextAsync(tekst, new() { IgnoreCase = true });
        var g = Assert.Single(await kalender.HentGjoeremaalAsync(runde, null));
        Assert.Equal(Enum.Parse<Tidfestingstype>(type), g.Tidfestingstype);
        Assert.Equal(3, g.Ansvarlige.Count);
        Assert.Contains(g.Ansvarlige, a => a.BrukerId == "demo-sbr");
        Assert.Contains(g.Ansvarlige, a => a.BrukerId == "demo-admin");
        Assert.Contains(g.Ansvarlige, a => a.BrukerId is null && a.Navn == "Sivert SBR");
        if (type is "Ingen" or "AnkerRelativ") Assert.Null(g.Sorteringsdag);
        if (type == "AnkerRelativ") Assert.True(g.VenterPaaAnker);
        if (type == "TentativMaaned") await Expect(rad).Not.ToContainTextAsync("15.02.2036");
        await Bilder(n, "gjoeremaal-lagret-" + type + valg);
        await rad.GetByRole(AriaRole.Link, new() { Name = "Rediger " + g.Tittel, Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true })).ToHaveValueAsync(type);
        if (type == "TentativMaaned") await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true })).ToHaveValueAsync(valg);
        if (type == "Rundeposisjon") await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Posisjon", Exact = true })).ToHaveValueAsync(valg);
        await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Fjern Dag Admin") }).ClickAsync();
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 lagret redigering");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 lagret redigering")).ToBeVisibleAsync();
        var etter = Assert.Single(await kalender.HentGjoeremaalAsync(runde, null));
        Assert.Equal(g.Id, etter.Id);
        Assert.Equal(2, etter.Ansvarlige.Count);
        await n.Gaa($"/internkalender/gjoeremaal/{g.Id}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 forkastes");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 lagret redigering")).ToBeVisibleAsync();
        Assert.Equal("UX16 lagret redigering", (await kalender.HentForRedigeringAsync(g.Id))!.Tittel);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData("Ingen")]
    [InlineData("KonkretDato")]
    [InlineData("TentativMaaned")]
    [InlineData("AnkerRelativ")]
    [InlineData("Rundeposisjon")]
    public Task Gjoeremaalsregel_bevarer_fem_tidfestinger_alle_runder_ansvar_og_bekreftet_sletting(string type) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await Toem(n);
        await n.Gaa("/internkalender/regler");
        await Bilder(n, "regler-tomt-" + type);
        await n.Gaa("/internkalender/regler/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 regel " + type);
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Kun intern regel <behold>");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Regelen må gjelde minst én rundetype.");
        await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX16 regel " + type);
        await Bilder(n, "regel-validering-" + type);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Hver runde (alle)", Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true }).SelectOptionAsync(type);
        if (type is "KonkretDato" or "TentativMaaned")
        {
            await n.Side.GetByLabel("Måned (1–12)", new() { Exact = true }).FillAsync("2");
            await n.Side.GetByLabel("Årsjustering", new() { Exact = true }).FillAsync("1");
        }
        if (type == "KonkretDato") await n.Side.GetByLabel("Dag (1–31)", new() { Exact = true }).FillAsync("29");
        if (type == "TentativMaaned") await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync("Ultimo");
        if (type == "AnkerRelativ")
        {
            await n.Side.GetByLabel("Anker-løp (publisert frist)", new() { Exact = true }).FillAsync("ux16-mangler");
            await n.Side.GetByLabel("Dager forskyvning", new() { Exact = false }).FillAsync("3");
        }
        if (type == "Rundeposisjon") await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Posisjon", Exact = true }).SelectOptionAsync("Sent");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Fra listen", Exact = true }).SelectOptionAsync("demo-sbr");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Legg til ansvarlig", Exact = true }).ClickAsync();
        await n.Side.GetByLabel("Fritekst", new() { Exact = true }).FillAsync("Ekstern person");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Legg til navn", Exact = true }).ClickAsync();
        await Bilder(n, "regel-editor-" + type);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 regel " + type)).ToBeVisibleAsync();
        var regel = Assert.Single(await Regler(n).HentAlleAsync());
        Assert.Equal(5, regel.Rundetyper.Count);
        Assert.Equal(2, regel.Ansvarlige.Count);
        Assert.Equal(Enum.Parse<Tidfestingstype>(type), regel.Tidfestingstype);
        var lagret = (await Regler(n).HentForRedigeringAsync(regel.Id))!;
        if (type is "KonkretDato" or "TentativMaaned") { Assert.Equal(2, lagret.Maaned); Assert.Equal(1, lagret.AarforskyvningJustering); }
        if (type == "KonkretDato") Assert.Equal(29, lagret.Dag);
        if (type == "TentativMaaned") Assert.Equal(Datokvalifikator.Ultimo, lagret.Datokvalifikator);
        if (type == "AnkerRelativ") { Assert.Equal("ux16-mangler", lagret.AnkerLoep); Assert.Equal(3, lagret.AnkerOffsetDager); }
        if (type == "Rundeposisjon") Assert.Equal(Rundeposisjon.Sent, lagret.Rundeposisjon);
        await Bilder(n, "regler-lagret-" + type);
        await Rad(n, regel.Tittel).GetByRole(AriaRole.Link, new() { Name = "Rediger " + regel.Tittel, Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Tidfesting", Exact = true })).ToHaveValueAsync(type);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Fjern Ekstern person", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Hver runde (alle)", Exact = true }).UncheckAsync();
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Regnskap", Exact = true })).Not.ToBeCheckedAsync();
        await n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Regnskap", Exact = true }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, regel.Tittel)).ToBeVisibleAsync();
        var endret = (await Regler(n).HentForRedigeringAsync(regel.Id))!;
        Assert.Equal([Rundetype.Regnskap], endret.Rundetyper);
        Assert.Single(endret.Ansvarlige);
        var slett = Rad(n, regel.Tittel).GetByRole(AriaRole.Button, new() { Name = "Slett " + regel.Tittel, Exact = true });
        await slett.ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Bilder(n, "regel-slett-dialog-" + type);
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(n.Side.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
        await Expect(slett).ToBeFocusedAsync();
        Assert.NotNull(await Regler(n).HentForRedigeringAsync(regel.Id));
        await slett.ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, slett", Exact = true }).ClickAsync();
        await Expect(slett).ToHaveCountAsync(0);
        Assert.Null(await Regler(n).HentForRedigeringAsync(regel.Id));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Synkronisering_viser_leggtil_oppdater_fjern_delutvalg_avbryt_og_lagret_resultat() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await Toem(n);
        var k = Kalender(n); var r = Regler(n);
        var oppdater = await r.OpprettAsync(Regel("UX16 oppdater før"));
        var fjern = await r.OpprettAsync(Regel("UX16 fjern"));
        var runde = await k.GenererRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var foer = await k.HentGjoeremaalAsync(runde, null);
        await r.OppdaterAsync(oppdater, Regel("UX16 oppdater etter"));
        await r.SlettAsync(fjern);
        await r.OpprettAsync(Regel("UX16 legg til"));
        await n.Gaa($"/internkalender/runde/{runde}/synk");
        await Expect(Rad(n, "UX16 oppdater etter")).ToContainTextAsync("Oppdater");
        await Expect(Rad(n, "UX16 fjern")).ToContainTextAsync("Fjern");
        await Expect(Rad(n, "UX16 legg til")).ToContainTextAsync("Legg til");
        await Bilder(n, "synk-tre-handlinger");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(foer), System.Text.Json.JsonSerializer.Serialize(await k.HentGjoeremaalAsync(runde, null)));
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Utfør valgte", Exact = true }).ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Bilder(n, "synk-bekreftelse");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        Assert.Equal(2, (await k.HentGjoeremaalAsync(runde, null)).Count);
        await Rad(n, "UX16 fjern").GetByRole(AriaRole.Checkbox).UncheckAsync();
        await Rad(n, "UX16 legg til").GetByRole(AriaRole.Checkbox).UncheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Utfør valgte", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, synkroniser", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 oppdater etter")).ToHaveCountAsync(0);
        var del = await k.HentGjoeremaalAsync(runde, null);
        Assert.Equal(2, del.Count);
        Assert.Contains(del, g => g.Id == foer.Single(g => g.Tittel == "UX16 oppdater før").Id && g.Tittel == "UX16 oppdater etter");
        Assert.Contains(del, g => g.Tittel == "UX16 fjern");
        Assert.DoesNotContain(del, g => g.Tittel == "UX16 legg til");
        await Bilder(n, "synk-delutvalg-kvittering");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Utfør valgte", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, synkroniser", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen endringer å synkronisere");
        var etter = await k.HentGjoeremaalAsync(runde, null);
        Assert.Equal(2, etter.Count);
        Assert.Contains(etter, g => g.Tittel == "UX16 legg til");
        Assert.DoesNotContain(etter, g => g.Tittel == "UX16 fjern");
        Assert.NotNull((await k.HentRundeAsync(runde))!.SistSynkronisert);
        await Bilder(n, "synk-ingen-endringer");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Gammel_synkvisning_bevarer_senere_fullfoert_og_manuelt_endret_arbeid() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await Toem(n);
        var k = Kalender(n); var r = Regler(n);
        var full = await r.OpprettAsync(Regel("UX16 fullført vern"));
        var manuell = await r.OpprettAsync(Regel("UX16 manuell vern"));
        var runde = await k.GenererRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var oppgaver = await k.HentGjoeremaalAsync(runde, null);
        await r.OppdaterAsync(full, Regel("UX16 fullført overskrives ikke"));
        await r.SlettAsync(manuell);
        await n.Gaa($"/internkalender/runde/{runde}/synk");
        await Expect(Rad(n, "UX16 fullført overskrives ikke")).ToBeVisibleAsync();
        await Expect(Rad(n, "UX16 manuell vern")).ToBeVisibleAsync();
        // En annen offentlig tjenesteoperasjon etter forhåndsvisningen representerer en annen sesjon.
        var fullId = oppgaver.Single(g => g.Tittel == "UX16 fullført vern").Id;
        await k.FullfoerAsync(fullId, "demo-admin", "Dag Admin");
        var manuellId = oppgaver.Single(g => g.Tittel == "UX16 manuell vern").Id;
        var endret = (await k.HentForRedigeringAsync(manuellId))!;
        endret.Notat = "UX16 nyere manuelt arbeid";
        await k.OppdaterGjoeremaalAsync(manuellId, endret);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Utfør valgte", Exact = true }).ClickAsync();
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Ja, synkroniser", Exact = true }).ClickAsync();
        await Expect(n.Side.Locator("main")).ToContainTextAsync("forhåndsvisning");
        await Bilder(n, "synk-utdatert-beskyttet");
        var etter = await k.HentGjoeremaalAsync(runde, null);
        Assert.Contains(etter, g => g.Id == fullId && g.Status == GjoeremaalStatus.Fullfoert && g.Tittel == "UX16 fullført vern" && g.FullfoertAvNavn == "Dag Admin");
        Assert.Contains(etter, g => g.Id == manuellId && g.ManueltEndret && g.Notat == "UX16 nyere manuelt arbeid");
        Assert.Empty(await k.ForberedSynkAsync(runde));
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Alle_direkteruter_avviser_andre_roller_og_interne_mutasjoner_publiserer_ingen_frister() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var k = Kalender(n); var r = Regler(n);
        var foerPublisering = (await n.Frister()).ToJsonString();
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "UX16 hemmelig oppretter");
        var g = await k.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16HEMMELIGTITTEL", Notat = "UX16HEMMELIGNOTAT", Ansvarlige = [new(null, "UX16HEMMELIGANSVAR")] });
        var regel = await r.OpprettAsync(Regel("UX16HEMMELIGREGEL"));
        await k.FullfoerAsync(g, "demo-sbr", "UX16HEMMELIGFULLFOERT");
        foreach (var persona in new string?[] { null, "demo-fag", "demo-fa", "demo-finfag", "demo-admin", "demo-sbr" })
        {
            await n.LoggInn(persona);
            foreach (var sti in Stier(runde, g, regel))
            {
                await n.Side.GotoAsync(sti);
                if (persona is "demo-admin" or "demo-sbr")
                    await Expect(n.Side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
                else
                {
                    await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være", Exact = true })).ToBeVisibleAsync();
                    Assert.DoesNotContain("UX16HEMMELIG", await n.Side.ContentAsync());
                }
            }
        }
        await n.LoggInn("demo-admin");
        var publisert = await n.Frister();
        Assert.Equal(foerPublisering, publisert.ToJsonString());
        Assert.DoesNotContain("UX16HEMMELIG", publisert.ToJsonString());
        foreach (var sti in new[] { "/kalender", "/budsjettaar", "/aarshjul", "/admin/ko" })
        {
            await n.Gaa(sti);
            await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX16HEMMELIG");
        }
        foreach (var sti in new[] { "/api/eksport/ics?gruppe=FA", "/api/eksport/frister-json" })
        {
            var svar = await n.Side.Context.APIRequest.GetAsync(sti);
            Assert.Equal(200, svar.Status);
            Assert.DoesNotContain("UX16HEMMELIG", await svar.TextAsync());
        }
        var wordSvar = await n.Side.Context.APIRequest.GetAsync("/api/eksport/word?gruppe=FA");
        Assert.Equal(200, wordSvar.Status);
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(await wordSvar.BodyAsync())))
        using (var les = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            Assert.DoesNotContain("UX16HEMMELIG", await les.ReadToEndAsync());
        await k.GjenaapneAsync(g);
        await k.SlettGjoeremaalAsync(g);
        await k.SlettRundeAsync(runde);
        Assert.Equal(publisert.ToJsonString(), (await n.Frister()).ToJsonString());
        n.BekreftIngenKretsfeil();
    });

    internal static string[] Stier(Guid runde, Guid g, Guid regel) => ["/internkalender", "/internkalender/mine", "/internkalender/regler",
        "/internkalender/regler/ny", $"/internkalender/regler/{regel}", $"/internkalender/runde/{runde}",
        $"/internkalender/runde/{runde}/synk", $"/internkalender/gjoeremaal/ny/{runde}", $"/internkalender/gjoeremaal/{g}"];

    private static RegelInndata Regel(string tittel) => new() { Tittel = tittel, Rundetyper = [Rundetype.Regnskap] };
    private static ILocator Rad(Nettleser n, string tekst) => n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tekst });
    private static async Task LeggTilAnsvarlig(Nettleser n, string id)
    {
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Legg til fra listen", Exact = true }).SelectOptionAsync(id);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Legg til ansvarlig", Exact = true }).ClickAsync();
    }
    private static async Task OpprettRunde(Nettleser n, string type, string aar, bool generert, bool forventNavigasjon = true)
    {
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Rundetype", Exact = true }).SelectOptionAsync(type);
        await n.Side.GetByLabel("År", new() { Exact = true }).FillAsync(aar);
        await n.Side.GetByLabel("Fyll fra generelle regler", new() { Exact = true }).SetCheckedAsync(generert);
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Opprett runde", Exact = true }).ClickAsync();
        if (forventNavigasjon) await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToContainTextAsync(aar);
    }
    private static async Task Toem(Nettleser n)
    {
        foreach (var r in await Kalender(n).HentRunderAsync()) await Kalender(n).SlettRundeAsync(r.Id);
        foreach (var r in await Regler(n).HentAlleAsync()) await Regler(n).SlettAsync(r.Id);
    }
    internal static IInternkalender Kalender(Nettleser n) => new InternkalenderTjeneste(new DatabaseFactory(n.Mappe), TimeProvider.System);
    internal static IGjoeremaalRegler Regler(Nettleser n) => new GjoeremaalRegelTjeneste(new DatabaseFactory(n.Mappe));
    private sealed class DatabaseFactory(string mappe) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(mappe, "demo.db")}").Options);
    }

    [Fact]
    public Task Mine_og_ansvarligfilter_matcher_brukerID_over_runder_og_Oevrig_aldri_fritekstnavn() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var k = Kalender(n);
        var a = await k.OpprettRundeAsync(Rundetype.Marsrunden, 2036, "Test");
        var b = await k.OpprettRundeAsync(Rundetype.Regnskap, 2035, "Test");
        var c = await k.HentEllerOpprettOvrigAsync("Test");
        foreach (var id in new[] { a, b, c })
        {
            await k.OpprettGjoeremaalAsync(id, new() { Tittel = "UX16 min " + id, Ansvarlige = [new("demo-sbr", "Sivert SBR"), new("demo-admin", "Administrator")] });
            await k.OpprettGjoeremaalAsync(id, new() { Tittel = "UX16 fritekst " + id, Ansvarlige = [new(null, "Sivert SBR")] });
        }
        await n.Gaa("/internkalender/mine");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX16 min" })).ToHaveCountAsync(3);
        await Expect(n.Side.Locator("main")).Not.ToContainTextAsync("UX16 fritekst");
        await Bilder(n, "mine-tverr-runder");
        await n.Gaa($"/internkalender/runde/{a}");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Vis ansvarlig", Exact = true }).SelectOptionAsync("demo-sbr");
        await Expect(Rad(n, "UX16 min")).ToHaveCountAsync(1);
        await Expect(Rad(n, "UX16 fritekst")).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("^Fullfør UX16 min") }).CheckAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("^Gjenåpne UX16 min") })).ToBeVisibleAsync();
        await n.Gaa("/internkalender/mine");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX16 min" })).ToHaveCountAsync(2);
        await n.LoggInn("demo-admin");
        await n.Gaa("/internkalender/mine");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX16 min" })).ToHaveCountAsync(2);
        await n.Gaa($"/internkalender/runde/{c}");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Synkroniser mot regler", Exact = true })).ToHaveCountAsync(0);
        n.BekreftIngenKretsfeil();
    });

    [Theory]
    [InlineData(-3, "12.02.2036")]
    [InlineData(0, "15.02.2036")]
    [InlineData(3, "18.02.2036")]
    public Task Ankerrelativt_gjoeremaal_venter_til_bevisst_redigering_etter_faktisk_publisering(int offset, string forventet) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var k = Kalender(n);
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var oppgave = await k.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16 venter på faktisk anker", Tidfestingstype = Tidfestingstype.AnkerRelativ, AnkerLoep = "ux16-anker", AnkerOffsetDager = offset });
        await n.Gaa($"/internkalender/runde/{runde}");
        await Expect(Rad(n, "UX16 venter på faktisk anker")).ToContainTextAsync("Venter på ankerdato");
        await n.Gaa("/admin/frist/ny");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 publisert anker");
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2036-02-15");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2036");
        await n.Side.GetByLabel("Løp", new() { Exact = true }).FillAsync("ux16-anker");
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new(@"\(FA\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        Assert.Contains(await n.Frister(), x => (string?)x!["tittel"] == "UX16 publisert anker");
        await n.Gaa($"/internkalender/runde/{runde}");
        await Expect(Rad(n, "UX16 venter på faktisk anker")).ToContainTextAsync("Venter på ankerdato");
        await n.Gaa($"/internkalender/gjoeremaal/{oppgave}");
        await Expect(n.Side.GetByLabel("Dager forskyvning", new() { Exact = false })).ToHaveValueAsync(offset.ToString());
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 venter på faktisk anker")).ToContainTextAsync(forventet);
        Assert.Equal(DateOnly.ParseExact(forventet, "dd.MM.yyyy"), Assert.Single(await k.HentGjoeremaalAsync(runde, null)).Sorteringsdag);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Manglende_ID_tomme_personutvalg_og_valideringsfeil_har_forstaaelige_beholdte_tilstander() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await Toem(n);
        await n.Gaa("/internkalender/mine");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("ingen aktive gjøremål");
        await Bilder(n, "mine-tomt");
        var mangler = Guid.NewGuid();
        foreach (var sti in new[] { $"/internkalender/runde/{mangler}", $"/internkalender/gjoeremaal/{mangler}", $"/internkalender/regler/{mangler}", $"/internkalender/runde/{mangler}/synk" })
        {
            await n.Gaa(sti);
            await Expect(n.Side.Locator("main")).ToContainTextAsync("finnes ikke");
            await Bilder(n, "manglende-" + (sti.EndsWith("/synk", StringComparison.Ordinal) ? "synk" : sti.Split('/')[2]));
        }
        var runde = await Kalender(n).OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        await n.Gaa($"/internkalender/gjoeremaal/ny/{runde}");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("UX16 behold meg ved feil");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Tittel må fylles ut.");
        await Expect(n.Side.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync("UX16 behold meg ved feil");
        await Bilder(n, "gjoeremaal-validering");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 vellykket etter feil");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 vellykket etter feil")).ToBeVisibleAsync();
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Vis ansvarlig", Exact = true }).SelectOptionAsync("demo-sbr");
        await Expect(n.Side.Locator("main")).ToContainTextAsync("Ingen aktive gjøremål for valgt ansvarlig");
        await Bilder(n, "runde-ansvarlig-tomt");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Faktisk_lagringsfeil_bevarer_skjema_og_ansvar_uten_raa_feil_og_samme_krets_kan_proeve_igjen() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        var k = Kalender(n);
        var runde = await k.OpprettRundeAsync(Rundetype.Regnskap, 2036, "Test");
        var id = await k.OpprettGjoeremaalAsync(runde, new() { Tittel = "UX16 før lagringsfeil", Notat = "Før" });
        var foer = System.Text.Json.JsonSerializer.Serialize(await k.HentForRedigeringAsync(id));
        await n.Gaa($"/internkalender/gjoeremaal/{id}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX16 etter lagringsfeil");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync("Behold dette feltet");
        await LeggTilAnsvarlig(n, "demo-sbr");
        await using var db = new DatabaseFactory(n.Mappe).CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER avvis_ux16_update AFTER UPDATE ON InterneGjoeremaal BEGIN SELECT RAISE(ABORT, 'UX16PRIVATE_DATABASE_DETAILS'); END;");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kunne ikke lagre gjøremålet");
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX16 etter lagringsfeil");
        await Expect(n.Side.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync("Behold dette feltet");
        Assert.DoesNotContain("UX16PRIVATE_DATABASE_DETAILS", await n.Side.ContentAsync());
        Assert.Equal(foer, System.Text.Json.JsonSerializer.Serialize(await k.HentForRedigeringAsync(id)));
        await Bilder(n, "gjoeremaal-serverfeil-beholdte-felt");
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_ux16_update;");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Rad(n, "UX16 etter lagringsfeil")).ToBeVisibleAsync();
        var lagret = (await k.HentForRedigeringAsync(id))!;
        Assert.Equal("Behold dette feltet", lagret.Notat);
        Assert.Contains(lagret.Ansvarlige, x => x.BrukerId == "demo-sbr");
        await Bilder(n, "gjoeremaal-serverfeil-rettet");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Fullfoering_av_annens_oppgave_har_navngitt_dialog_Escape_fokus_og_lagret_hvem_naar() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-sbr");
        await n.Gaa("/internkalender");
        await n.Side.GetByRole(AriaRole.Button, new() { NameRegex = new("Åpne.*Øvrig") }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Øvrig");
        var sti = new Uri(n.Side.Url).AbsolutePath;
        await n.Side.GetByLabel("Hurtiginnlegging", new() { Exact = false }).FillAsync("UX16 annens oppgave");
        await n.Side.GetByLabel("Hurtiginnlegging", new() { Exact = false }).PressAsync("Enter");
        var fullfoer = n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Fullfør UX16 annens oppgave", Exact = true });
        await fullfoer.CheckAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync("ikke ansvarlig");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeFocusedAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).PressAsync("Tab");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, meld fullført", Exact = true })).ToBeFocusedAsync();
        await Bilder(n, "fullfoer-dialog");
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(fullfoer).Not.ToBeCheckedAsync();
        await Expect(fullfoer).ToBeFocusedAsync();
        await fullfoer.CheckAsync();
        var tidligst = DateTimeOffset.UtcNow.AddSeconds(-1);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, meld fullført", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Gjenåpne UX16 annens oppgave", Exact = true })).ToBeVisibleAsync();
        await n.Gaa(sti);
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "UX16 annens oppgave" });
        await Expect(rad).ToContainTextAsync("Sivert SBR");
        var rundeId = Guid.Parse(new Uri(n.Side.Url).Segments.Last());
        var lagret = Assert.Single(await Kalender(n).HentGjoeremaalAsync(rundeId, null), x => x.Tittel == "UX16 annens oppgave");
        Assert.InRange(lagret.FullfoertTid!.Value, tidligst, DateTimeOffset.UtcNow);
        var oslotid = TimeZoneInfo.ConvertTime(lagret.FullfoertTid.Value, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        await Expect(rad).ToContainTextAsync(oslotid.ToString("dd.MM.yyyy 'kl.' HH:mm") + " (Oslo)");
        await Bilder(n, "fullfoert");
        await rad.GetByRole(AriaRole.Button, new() { Name = "Gjenåpne UX16 annens oppgave", Exact = true }).ClickAsync();
        await Expect(fullfoer).ToBeVisibleAsync();
        await n.Gaa(sti);
        await Expect(fullfoer).ToBeVisibleAsync();
        await Expect(rad).Not.ToContainTextAsync("Fullført av");
        var gjen = Assert.Single(await Kalender(n).HentGjoeremaalAsync(rundeId, null), x => x.Id == lagret.Id);
        Assert.Null(gjen.FullfoertAvNavn);
        Assert.Null(gjen.FullfoertTid);
        n.BekreftIngenKretsfeil();
    });

    internal static async Task Bilder(Nettleser n, string navn)
    {
        if (navn.StartsWith("oversikt-tom-", StringComparison.Ordinal) || navn.StartsWith("runde-duplikat-", StringComparison.Ordinal) || navn.StartsWith("runde-slett-dialog-", StringComparison.Ordinal))
        { if (!navn.EndsWith("Regnskap", StringComparison.Ordinal)) return; }
        if (navn.StartsWith("gjoeremaal-editor-", StringComparison.Ordinal) || navn.StartsWith("gjoeremaal-lagret-", StringComparison.Ordinal))
        { if (!new[] { "Ingen", "KonkretDato", "TentativMaanedMedio", "AnkerRelativ", "RundeposisjonMidt" }.Any(x => navn.EndsWith(x, StringComparison.Ordinal))) return; }
        if (navn.StartsWith("regler-tomt-", StringComparison.Ordinal) || navn.StartsWith("regel-validering-", StringComparison.Ordinal) || navn.StartsWith("regler-lagret-", StringComparison.Ordinal) || navn.StartsWith("regel-slett-dialog-", StringComparison.Ordinal))
        { if (!navn.EndsWith("Ingen", StringComparison.Ordinal)) return; }
        foreach (var bredde in new[] { 1440, 390 })
        {
            await n.Side.SetViewportSizeAsync(bredde, 1000);
            await n.Side.EvaluateAsync("window.scrollTo(0, 0)");
            var fokus = await n.Side.EvaluateAsync<string>("JSON.stringify({tag: document.activeElement?.tagName, id: document.activeElement?.id, klasse: document.activeElement?.className, navn: document.activeElement?.getAttribute('aria-label'), scrollY})");
            await File.AppendAllTextAsync(Path.Combine(n.Mappe, "91-screenshot-focus.txt"), navn + "-" + bredde + " " + fokus + Environment.NewLine);
            await n.Side.ScreenshotAsync(new() { Path = Path.Combine(n.Mappe, $"91-{navn}-{bredde}.png"), FullPage = await n.Side.GetByRole(AriaRole.Dialog).CountAsync() == 0 });
            Assert.True(await n.Side.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Siden skal ikke kreve vannrett rulling.");
        }
        await n.Side.SetViewportSizeAsync(1440, 1000);
    }
}
