using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderTester;

namespace Aarshjul.BrowserTests;

public sealed class KalenderforslagTester
{
    [Theory]
    [InlineData("demo-fa", "FA")]
    [InlineData("demo-finfag", "FIN-FAG")]
    public Task Bidragsyter_starter_forslag_fra_valgt_dato_og_faar_en_godkjent_frist_i_alle_visninger(string persona, string gruppe) => Nettleser.Kjor(async n =>
    {
        var tittel = "UX81 kalenderforslag " + gruppe;
        const string notat = "Hele forslaget med <trygg tekst> og norsk årsskille.";
        await n.LoggInn(persona);
        var skjema = await StartForslag(n);
        await Expect(skjema.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-20");
        var idag = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo"));
        await Expect(skjema.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync((idag.Year + 1).ToString());
        await Expect(skjema.Locator("fieldset.synlighet input:checked")).ToHaveCountAsync(0);
        await Expect(skjema.GetByLabel("Klokkeslett", new() { Exact = false })).ToHaveCountAsync(0);
        await Fyll(skjema, tittel, notat: notat, budsjettaar: 2032);
        // Den valgte dagen kan korrigeres, mens budsjettåret velges uavhengig av kalenderåret.
        await skjema.GetByLabel("Dato", new() { Exact = true }).FillAsync("2030-11-21");
        await skjema.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Gulbok");
        await Gruppe(skjema, gruppe).CheckAsync();
        await Gruppe(skjema, "POL").CheckAsync();
        await SendOgKvitter(n);
        var bidrag = n.Side;
        await n.Side.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Link, new() { Name = "Mine forslag", Exact = true }).ClickAsync();
        await Expect(MittForslag(n, tittel)).ToContainTextAsync("Venter");
        await Expect(MittForslag(n, tittel)).ToHaveCountAsync(1);
        await Expect(MittForslag(n, tittel)).ToContainTextAsync("21.11.2030");
        await Expect(MittForslag(n, tittel)).ToContainTextAsync("2032");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Expect(Maanedskalender(n).GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true })).ToHaveCountAsync(0);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko");
        var kort = ForslagTester.Kort(n.Side, tittel);
        await Expect(kort).ToHaveCountAsync(1);
        foreach (var verdi in new[] { "21.11.2030", "2032", "Gul bok", notat })
            await Expect(kort).ToContainTextAsync(verdi);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(Gruppe(kort, gruppe)).ToBeCheckedAsync();
        // En foreslått POL-gruppe følger aldri stille med inn i publiseringen.
        await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        Assert.True(Guid.TryParse((string?)publisert["id"], out _));
        Assert.Equal("2030-11-21", (string?)publisert["dato"]);
        Assert.Equal(2032, (int)publisert["budsjettaar"]!);
        Assert.Equal(notat, (string?)publisert["notat"]);
        Assert.False((bool)publisert["erTentativ"]!);
        Assert.Equal(new[] { gruppe }, publisert["synlighet"]!.AsArray().Select(g => (string)g!).ToArray());
        n.Side = bidrag;
        await KontrollerVisninger(n, publisert, "21. november 2030", "Gul bok", notat);
        await n.Gaa("/forslag/mine");
        await Expect(MittForslag(n, tittel)).ToContainTextAsync("Godkjent");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Tentativ_maaned_med_alle_kvalifikatorer_bevarer_presisjon_etter_godkjenning() => Nettleser.Kjor(async n =>
    {
        foreach (var kvalifikator in new[] { "", "Primo", "Medio", "Ultimo" })
        {
            var tittel = "UX81 tentativ " + (kvalifikator == "" ? "måned" : kvalifikator);
            await n.LoggInn("demo-fa");
            var skjema = await StartForslag(n);
            await Fyll(skjema, tittel);
            await skjema.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await Expect(skjema.GetByLabel("Dato", new() { Exact = true })).ToHaveCountAsync(0);
            await skjema.GetByLabel("Måned", new() { Exact = true }).FillAsync("2030-12");
            await skjema.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync(kvalifikator);
            await Gruppe(skjema, "FA").CheckAsync();
            await SendOgKvitter(n);
            await n.LoggInn("demo-admin");
            await Godkjenn(n, tittel);
            var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
            Assert.True((bool)publisert["erTentativ"]!);
            Assert.Equal(1, (int)publisert["datopresisjon"]!);
            Assert.Equal(kvalifikator switch { "Primo" => 0, "Medio" => 1, "Ultimo" => 2, _ => (int?)null }, (int?)publisert["datokvalifikator"]);
            await n.LoggInn("demo-fa");
            await KontrollerVisninger(n, publisert, (kvalifikator == "" ? "" : kvalifikator.ToLowerInvariant() + " ") + "desember 2030", "Budsjett");
        }
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Validering_beholder_felter_og_avbryt_og_kvittering_beholder_kalenderkontekst() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        var aarvalg = n.Side.GetByRole(AriaRole.Group, new() { Name = "Budsjettår", Exact = true });
        await aarvalg.GetByRole(AriaRole.Button, new() { Name = "Fjern alle", Exact = true }).ClickAsync();
        await Expect(aarvalg.Locator("input:checked")).ToHaveCountAsync(0);
        await aarvalg.GetByLabel("2027", new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel("Budsjett", new() { Exact = true }).UncheckAsync();
        await n.Side.GetByRole(AriaRole.Searchbox).FillAsync("UX81 kontekst");
        await Dag(n, "20. november 2030").PressAsync("Enter");
        var skjema = n.Side.GetByRole(AriaRole.Dialog);
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true }).PressAsync("Enter");
        await Expect(skjema.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2027");
        await Fyll(skjema, "", notat: "Behold mitt utfylte notat");
        await skjema.GetByLabel("Dato", new() { Exact = true }).FillAsync("2032-01-02");
        await skjema.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Regnskap");
        await Gruppe(skjema, "FIN-FAG").CheckAsync();
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        var feil = skjema.GetByRole(AriaRole.Alert);
        await Expect(feil).ToContainTextAsync("Tittel må fylles ut");
        await Expect(feil).ToBeFocusedAsync();
        await Expect(skjema.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2032-01-02");
        await Expect(skjema.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        await Expect(skjema.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync("Behold mitt utfylte notat");
        await Expect(skjema.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true })).ToHaveValueAsync("Regnskap");
        await Expect(Gruppe(skjema, "FIN-FAG")).ToBeCheckedAsync();
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true }).ClickAsync();
        await Expect(skjema).ToHaveAccessibleNameAsync("Frister 20. november 2030");
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(Dag(n, "20. november 2030")).ToBeFocusedAsync();
        await KontrollerKontekst(n);
        await Dag(n, "20. november 2030").ClickAsync();
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true }).ClickAsync();
        await Fyll(skjema, "UX81 kontekst sendes");
        await SendOgKvitter(n);
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true }).ClickAsync();
        await KontrollerKontekst(n);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Lagringsfeil_gir_trygg_melding_beholder_verdiene_og_tillater_nytt_forsoek() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        var skjema = await StartForslag(n);
        const string tittel = "UX81 lagringsfeil";
        await Fyll(skjema, tittel, notat: "Notatet overlever teknisk feil");
        await Gruppe(skjema, "FA").CheckAsync();
        using var db = await Database(n);
        using var feil = db.CreateCommand();
        // Fremtvinger faktisk feil ved INSERT; observasjonene skjer i UI og offentlig lesing.
        feil.CommandText = "CREATE TRIGGER UX81_lagringsfeil BEFORE INSERT ON Forslag BEGIN SELECT RAISE(ABORT, 'UX81-SQL-hemmelig-detalj'); END;";
        await feil.ExecuteNonQueryAsync();
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(skjema.GetByRole(AriaRole.Alert)).ToContainTextAsync("Prøv igjen");
        await Expect(skjema.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await Expect(skjema).Not.ToContainTextAsync("UX81-SQL-hemmelig-detalj");
        Assert.DoesNotContain("UX81-SQL-hemmelig-detalj", await n.Side.ContentAsync());
        await Expect(skjema.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(tittel);
        await Expect(skjema.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-20");
        await Expect(skjema.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync("2031");
        await Expect(skjema.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync("Notatet overlever teknisk feil");
        await Expect(Gruppe(skjema, "FA")).ToBeCheckedAsync();
        await Expect(skjema.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true })).ToBeEnabledAsync();
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        feil.CommandText = "DROP TRIGGER UX81_lagringsfeil;";
        await feil.ExecuteNonQueryAsync();
        await SendOgKvitter(n);
        await n.Gaa("/forslag/mine");
        await Expect(MittForslag(n, tittel)).ToHaveCountAsync(1);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko");
        await Expect(ForslagTester.Kort(n.Side, tittel)).ToHaveCountAsync(1);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Paagaaende_og_allerede_koede_innsendinger_gir_bare_ett_lagret_forslag() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-fa");
        var skjema = await StartForslag(n);
        const string tittel = "UX81 bare én innsending";
        await Fyll(skjema, tittel);
        await Gruppe(skjema, "FA").CheckAsync();
        using var db = await Database(n);
        using var skriving = db.CreateCommand();
        skriving.CommandText = "BEGIN IMMEDIATE;";
        await skriving.ExecuteNonQueryAsync();
        try
        {
            // Tre allerede køede submit-hendelser, også etter at knappen er deaktivert i renderen.
            await skjema.Locator("form").EvaluateAsync("form => { for (let i = 0; i < 3; i++) form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); }");
            await Expect(skjema.GetByRole(AriaRole.Button, new() { NameRegex = new("Send") })).ToBeDisabledAsync();
            await Expect(skjema.GetByRole(AriaRole.Button, new() { Name = "Lukk", Exact = true })).ToBeDisabledAsync();
            await Expect(skjema.GetByRole(AriaRole.Button, new() { Name = "Avbryt", Exact = true })).ToBeDisabledAsync();
            await n.Side.Keyboard.PressAsync("Escape");
            await Expect(skjema).ToBeVisibleAsync();
        }
        finally
        {
            skriving.CommandText = "COMMIT;";
            await skriving.ExecuteNonQueryAsync();
        }
        await Expect(skjema).ToContainTextAsync("Venter på behandling");
        await n.Gaa("/forslag/mine");
        await Expect(MittForslag(n, tittel)).ToHaveCountAsync(1);
        await Expect(MittForslag(n, tittel)).ToContainTextAsync("Venter");
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko");
        await Expect(ForslagTester.Kort(n.Side, tittel)).ToHaveCountAsync(1);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Leser_og_administrator_har_ingen_forslagshandling_og_direkte_serverinnganger_avvises() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX81 rollegjerde", "FAG");
        var id = (string)(await n.Frister()).Single(f => (string?)f!["tittel"] == "UX81 rollegjerde")!["id"]!;
        foreach (var persona in new[] { "demo-fag", "demo-admin" })
        {
            await n.LoggInn(persona);
            await n.Gaa("/kalender");
            await VelgMaaned(n, "2030-11");
            await Dag(n, "20. november 2030").ClickAsync();
            var dialog = n.Side.GetByRole(AriaRole.Dialog);
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true })).ToHaveCountAsync(0);
            await dialog.GetByRole(AriaRole.Button, new() { Name = "UX81 rollegjerde", Exact = true }).ClickAsync();
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true })).ToHaveCountAsync(0);
            await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Foreslå endring", Exact = true })).ToHaveCountAsync(0);
            await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true })).ToHaveCountAsync(persona == "demo-admin" ? 1 : 0);
            foreach (var sti in new[] { "/forslag/ny", $"/forslag/endre/{id}", $"/forslag/{Guid.NewGuid()}/rediger" })
            {
                var svar = await n.Side.Context.APIRequest.GetAsync(sti, new() { MaxRedirects = 0 });
                Assert.Contains(svar.Status, new[] { 302, 403 });
                Assert.DoesNotContain("Send inn", await svar.TextAsync());
            }
        }
        // Administratorens opprinnelige direkte editor er fortsatt en virksom inngang.
        await n.Gaa($"/admin/frist/{id}");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("UX81 direkte administrert");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(id, (string?)Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "UX81 direkte administrert")!["id"]);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Endringsforslag_fra_synlig_detalj_bevarer_originalens_ID_og_synlighet() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.OpprettFrist("UX81 original", "FA");
        var id = (string)(await n.Frister()).Single(f => (string?)f!["tittel"] == "UX81 original")!["id"]!;
        await n.Gaa($"/admin/frist/{id}");
        await Gruppe(n.Side.Locator("main"), "POL").CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var original = (await n.Frister()).Single(f => (string?)f!["id"] == id)!;
        Assert.Equal(new[] { "FA", "POL" }, original["synlighet"]!.AsArray().Select(g => (string)g!).Order().ToArray());
        await n.LoggInn("demo-fa");
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await AapneFrist(n, "UX81 original");
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true }).ClickAsync();
        await Expect(dialog.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync("UX81 original");
        await Expect(dialog.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await Fyll(dialog, "UX81 korrigert", notat: "Endret innhold, samme publikum");
        await SendOgKvitter(n);
        Assert.Equal("UX81 original", (string?)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["tittel"]);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko");
        var kort = ForslagTester.Kort(n.Side, "UX81 korrigert");
        await Expect(kort).ToContainTextAsync("Endring");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true })).ToBeVisibleAsync();
        await Expect(kort.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var endret = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == "UX81 korrigert")!;
        Assert.Equal(id, (string?)endret["id"]);
        Assert.Equal(original["synlighet"]!.ToJsonString(), endret["synlighet"]!.ToJsonString());
        await n.LoggInn("demo-fa");
        await KontrollerVisninger(n, endret, "20. november 2030", "Budsjett", "Endret innhold, samme publikum");
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Gruppe(ILocator skjema, string gruppe)
        => skjema.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({gruppe}\\)$") });

    private static ILocator MittForslag(Nettleser n, string tittel)
        => n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel });

    private static async Task<ILocator> StartForslag(Nettleser n)
    {
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Dag(n, "20. november 2030").ClickAsync();
        var skjema = n.Side.GetByRole(AriaRole.Dialog);
        await skjema.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true }).ClickAsync();
        await Expect(skjema.GetByLabel("Tittel", new() { Exact = true })).ToBeVisibleAsync();
        return skjema;
    }

    private static async Task Fyll(ILocator skjema, string tittel, string? notat = null, int budsjettaar = 2031)
    {
        await skjema.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await skjema.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync(budsjettaar.ToString());
        if (notat is not null) await skjema.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
    }

    private static async Task SendOgKvitter(Nettleser n)
    {
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(dialog).ToContainTextAsync("Forslaget er sendt inn");
        await Expect(dialog).ToContainTextAsync("Venter på behandling");
        await Expect(dialog.GetByRole(AriaRole.Link, new() { Name = "Mine forslag", Exact = true })).ToHaveAttributeAsync("href", "/forslag/mine");
    }

    private static async Task Godkjenn(Nettleser n, string tittel)
    {
        await n.Gaa("/admin/ko");
        var kort = ForslagTester.Kort(n.Side, tittel);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
    }

    private static async Task KontrollerKontekst(Nettleser n)
    {
        await Expect(n.Side.GetByLabel("Vis måned", new() { Exact = true })).ToHaveValueAsync("2030-11");
        await Expect(n.Side.GetByRole(AriaRole.Searchbox)).ToHaveValueAsync("UX81 kontekst");
        await Expect(n.Side.GetByLabel("2027", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Budsjett", new() { Exact = true })).Not.ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Gul bok", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(n.Side.GetByLabel("Regnskap", new() { Exact = true })).Not.ToBeCheckedAsync();
    }

    private static async Task KontrollerVisninger(Nettleser n, JsonNode publisert, params string[] verdier)
    {
        var id = (string)publisert["id"]!;
        var tittel = (string)publisert["tittel"]!;
        var aar = (int)publisert["budsjettaar"]!;
        var maaned = ((string)publisert["dato"]!)[..7];
        await n.Gaa("/kalender");
        await VelgMaaned(n, maaned);
        await n.Side.GetByLabel(aar.ToString(), new() { Exact = true }).CheckAsync();
        var kalenderknapp = n.Side.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
        await Expect(kalenderknapp).ToHaveCountAsync(1);
        await Expect(kalenderknapp).ToHaveAttributeAsync("data-frist-id", id);
        await KontrollerDetaljer(n, kalenderknapp, tittel, (bool)publisert["erTentativ"]!, verdier.Append(aar.ToString()));
        await n.Gaa("/budsjettaar");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync(aar.ToString());
        foreach (var visning in new[] { "Tidslinje", "Fristliste" })
        {
            var valg = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Budsjettårvisning", Exact = true })
                .GetByRole(AriaRole.Button, new() { Name = visning, Exact = true });
            await valg.ClickAsync();
            await Expect(valg).ToHaveAttributeAsync("aria-pressed", "true");
            var flate = n.Side.GetByRole(AriaRole.Region, new() { Name = visning + " for budsjettår " + aar, Exact = true });
            foreach (var gruppe in await flate.GetByRole(AriaRole.Button, new() { NameRegex = new("^Vis [0-9]+ frister i ") }).AllAsync())
            {
                await gruppe.ClickAsync();
                await Expect(gruppe).ToHaveAttributeAsync("aria-expanded", "true");
            }
            var knapp = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
            await Expect(knapp).ToHaveCountAsync(1);
            await Expect(knapp).ToHaveAttributeAsync("data-frist-id", id);
            await KontrollerDetaljer(n, knapp, tittel, (bool)publisert["erTentativ"]!, verdier.Append(aar.ToString()));
        }
    }

    private static async Task KontrollerDetaljer(Nettleser n, ILocator knapp, string tittel, bool tentativ, IEnumerable<string> verdier)
    {
        await knapp.ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = tittel, Exact = true })).ToBeFocusedAsync();
        foreach (var verdi in verdier) await Expect(dialog).ToContainTextAsync(verdi);
        if (tentativ)
        {
            await Expect(dialog).ToContainTextAsync("Tentativ måned");
            await Expect(dialog).Not.ToContainTextAsync("20.");
            await Expect(dialog).Not.ToContainTextAsync("Heldag");
        }
        await n.Side.Keyboard.PressAsync("Escape");
    }

    private static async Task<SqliteConnection> Database(Nettleser n)
    {
        var db = new SqliteConnection($"Data Source={Path.Combine(n.Mappe, "demo.db")}");
        await db.OpenAsync();
        return db;
    }
}
