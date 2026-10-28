using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using static Aarshjul.BrowserTests.KalenderTester;
using static Aarshjul.BrowserTests.KalenderperspektivTester;

namespace Aarshjul.BrowserTests;

public sealed class AtelierSluttakseptanseTester
{
    [Theory]
    [InlineData("demo-fa", "FA")]
    [InlineData("demo-finfag", "FIN-FAG")]
    public Task Dato_forslag_ko_aktiv_POL_og_endringsforslag_bevarer_en_frist_i_alle_seks_flater(string persona, string gruppe) => Nettleser.Kjor(async n =>
    {
        var tittel = "UX93 samlet forslag " + gruppe;
        const string notat = "Samme lagrede notat med <trygg tekst> gjennom hele publiseringen.";
        await n.LoggInn(persona);
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await Dag(n, "20. november 2030").ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå frist", Exact = true }).ClickAsync();
        await Expect(dialog.GetByLabel("Dato", new() { Exact = true })).ToHaveValueAsync("2030-11-20");
        await dialog.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await dialog.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2032");
        await dialog.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
        await dialog.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Regnskap");
        await Gruppe(dialog, gruppe).CheckAsync();
        await Gruppe(dialog, "POL").CheckAsync();
        await Send(n);
        await dialog.GetByRole(AriaRole.Link, new() { Name = "Mine forslag", Exact = true }).ClickAsync();
        var mitt = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel });
        await Expect(mitt).ToHaveCountAsync(1);
        foreach (var verdi in new[] { "Venter", "20.11.2030", "2032" }) await Expect(mitt).ToContainTextAsync(verdi);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);

        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=2032");
        var kort = ForslagTester.Kort(n.Side, tittel);
        await Expect(kort).ToHaveCountAsync(1);
        foreach (var verdi in new[] { "20.11.2030", "2032", "Regnskap", notat }) await Expect(kort).ToContainTextAsync(verdi);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(Gruppe(kort, gruppe)).ToBeCheckedAsync();
        await Expect(Gruppe(kort, "POL")).Not.ToBeCheckedAsync();
        // POL blir publisert bare etter administratorens eget aktive valg.
        await Gruppe(kort, "POL").CheckAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        var id = (string)publisert["id"]!;
        KontrollerLeseport(publisert, id, tittel, "2030-11-20", 2032, "Regnskap", notat, gruppe, "POL");
        await Register(n, id, tittel, "20. november 2030", 2032, "Regnskap", notat);
        await n.LoggInn(persona);
        await AlleLeseflater(n, id, tittel, "2030-11", "20. november 2030", 2032, "Regnskap", notat);
        await n.Gaa("/forslag/mine");
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel })).ToContainTextAsync("Godkjent");

        // Samme godkjente identitet går gjennom en ny innholdsendring, uten nytt publikum.
        await n.Gaa("/kalender");
        await VelgMaaned(n, "2030-11");
        await n.Side.GetByLabel("Regnskap", new() { Exact = true }).CheckAsync();
        await AapneFrist(n, tittel);
        dialog = n.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Foreslå endring", Exact = true }).ClickAsync();
        await Expect(dialog.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        var endretTittel = tittel + " korrigert";
        const string endretNotat = "Korrigert innhold med opprinnelig FA/FIN-FAG og POL beholdt.";
        await dialog.GetByLabel("Tittel", new() { Exact = true }).FillAsync(endretTittel);
        await dialog.GetByLabel("Notat", new() { Exact = true }).FillAsync(endretNotat);
        await Send(n);
        Assert.Equal(tittel, (string?)(await n.Frister()).Single(f => (string?)f!["id"] == id)!["tittel"]);
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko?aar=2032");
        kort = ForslagTester.Kort(n.Side, endretTittel);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true })).ToBeVisibleAsync();
        await Expect(kort.Locator("fieldset.synlighet")).ToHaveCountAsync(0);
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        publisert = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == endretTittel)!;
        KontrollerLeseport(publisert, id, endretTittel, "2030-11-20", 2032, "Regnskap", endretNotat, gruppe, "POL");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == tittel);
        await Register(n, id, endretTittel, "20. november 2030", 2032, "Regnskap", endretNotat);
        await n.LoggInn(persona);
        await AlleLeseflater(n, id, endretTittel, "2030-11", "20. november 2030", 2032, "Regnskap", endretNotat);
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Administrator_oppretter_og_redigerer_en_frist_med_samme_ID_og_felter_i_alle_seks_flater() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        const string original = "UX93 administrert original";
        await n.OpprettFrist(original, dato: "2030-11-20", budsjettaar: 2032, kategori: "Gulbok", notat: "Før redigering", tid: "08:35");
        var id = (string)Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == original)!["id"]!;
        await Register(n, id, original, "20. november 2030", 2032, "Gul bok", "Før redigering", "08:35");
        await AlleLeseflater(n, id, original, "2030-11", "20. november 2030", 2032, "Gul bok", "Før redigering", "08:35");
        await n.Gaa($"/admin/frist/{id}?aar=2032");
        const string tittel = "UX93 administrert korrigert";
        const string notat = "Endret én gang, lest i alle seks flater.";
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await n.Side.GetByLabel("Dato", new() { Exact = true }).FillAsync("2031-01-23");
        await n.Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync("2033");
        await n.Side.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync("Regnskap");
        await n.Side.GetByLabel("Klokkeslett", new() { Exact = false }).FillAsync("22:15");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var lagret = Assert.Single(await n.Frister(), f => (string?)f!["tittel"] == tittel)!;
        KontrollerLeseport(lagret, id, tittel, "2031-01-23", 2033, "Regnskap", notat, "FA");
        Assert.Equal("22:15:00", (string?)lagret["klokkeslett"]);
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == original);
        await Register(n, id, tittel, "23. januar 2031", 2033, "Regnskap", notat, "22:15");
        await AlleLeseflater(n, id, tittel, "2031-01", "23. januar 2031", 2033, "Regnskap", notat, "22:15");
        n.BekreftIngenKretsfeil();
    });

    private static ILocator Gruppe(ILocator skjema, string gruppe) => skjema.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({gruppe}\\)$") });

    private static async Task Send(Nettleser n)
    {
        var dialog = n.Side.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(dialog).ToContainTextAsync("Forslaget er sendt inn");
        await Expect(dialog).ToContainTextAsync("Venter på behandling");
    }

    private static void KontrollerLeseport(JsonNode frist, string id, string tittel, string dato, int aar, string kategori, string notat, params string[] grupper)
    {
        Assert.Equal(id, (string?)frist["id"]);
        Assert.Equal(tittel, (string?)frist["tittel"]);
        Assert.Equal(dato, (string?)frist["dato"]);
        Assert.Equal(aar, (int?)frist["budsjettaar"]);
        Assert.Equal(kategori == "Regnskap" ? 2 : 1, (int?)frist["kategori"]);
        Assert.Equal(notat, (string?)frist["notat"]);
        Assert.False((bool)frist["erTentativ"]!);
        Assert.Equal(0, (int?)frist["datopresisjon"]);
        Assert.Null(frist["datokvalifikator"]);
        Assert.Equal("manuell", (string?)frist["kilde"]);
        Assert.Null(frist["dokumentId"]);
        Assert.Null(frist["kildeUrl"]);
        Assert.Equal(grupper.Order(), frist["synlighet"]!.AsArray().Select(g => (string)g!).Order());
    }

    private static async Task Register(Nettleser n, string id, string tittel, string dato, int aar, string kategori, string notat, string? tid = null)
    {
        await n.Gaa($"/admin/frister?aar={aar}");
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = tittel });
        await Expect(rad).ToHaveCountAsync(1);
        foreach (var verdi in new[] { dato, aar.ToString(), kategori }) await Expect(rad).ToContainTextAsync(verdi);
        var rediger = rad.GetByRole(AriaRole.Link, new() { Name = "Rediger", Exact = true });
        await Expect(rediger).ToHaveAttributeAsync("href", $"/admin/frist/{id}?aar={aar}");
        await rediger.ClickAsync();
        await Expect(n.Side.GetByLabel("Tittel", new() { Exact = true })).ToHaveValueAsync(tittel);
        await Expect(n.Side.GetByLabel("Budsjettår", new() { Exact = true })).ToHaveValueAsync(aar.ToString());
        await Expect(n.Side.GetByLabel("Notat", new() { Exact = true })).ToHaveValueAsync(notat);
        await Expect(n.Side.GetByLabel("Klokkeslett", new() { Exact = false })).ToHaveValueAsync(tid ?? "");
    }

    private static async Task AlleLeseflater(Nettleser n, string id, string tittel, string maaned, string dato, int aar, string kategori, string notat, string? tid = null)
    {
        await n.Gaa("/kalender");
        await VelgMaaned(n, maaned);
        await n.Side.GetByLabel(aar.ToString(), new() { Exact = true }).CheckAsync();
        await n.Side.GetByLabel(kategori, new() { Exact = true }).CheckAsync();
        await Bytt(n, "Måned");
        // Minikalenderen velger den faktiske dagen før uke/listen leses.
        await Minikalender(n).GetByRole(AriaRole.Button, new() { Name = "Velg " + dato + ", 1 frist", Exact = true }).ClickAsync();
        foreach (var visning in new[] { "Måned", "Uke", "Liste" })
        {
            await Bytt(n, visning);
            var flate = visning == "Måned" ? Maanedskalender(n) : visning == "Uke" ? Ukekalender(n) : Liste(n);
            await Detaljer(n, flate, id, tittel, dato, aar, kategori, notat, tid);
        }
        await n.Gaa("/budsjettaar");
        await n.Side.GetByRole(AriaRole.Combobox, new() { Name = "Velg budsjettår", Exact = true }).SelectOptionAsync(aar.ToString());
        foreach (var visning in new[] { "Tidslinje", "Fristliste" })
        {
            var valg = n.Side.GetByRole(AriaRole.Navigation, new() { Name = "Budsjettårvisning", Exact = true }).GetByRole(AriaRole.Button, new() { Name = visning, Exact = true });
            await valg.ClickAsync();
            await Expect(valg).ToHaveAttributeAsync("aria-pressed", "true");
            var flate = n.Side.GetByRole(AriaRole.Region, new() { Name = visning + " for budsjettår " + aar, Exact = true });
            await Expect(flate).ToBeVisibleAsync();
            await Detaljer(n, flate, id, tittel, dato, aar, kategori, notat, tid);
        }
    }

    private static async Task Detaljer(Nettleser n, ILocator flate, string id, string tittel, string dato, int aar, string kategori, string notat, string? tid)
    {
        var knapp = flate.GetByRole(AriaRole.Button, new() { Name = tittel, Exact = true });
        await Expect(knapp).ToHaveCountAsync(1);
        await Expect(knapp).ToHaveAttributeAsync("data-frist-id", id);
        await knapp.ClickAsync();
        var dialog = n.Side.GetByRole(AriaRole.Dialog, new() { Name = tittel, Exact = true });
        foreach (var verdi in new[] { dato, aar.ToString(), kategori, notat, tid ?? "Heldag" }) await Expect(dialog).ToContainTextAsync(verdi);
        await Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "Kilderundskriv", Exact = true })).ToHaveCountAsync(0);
        await n.Side.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToHaveCountAsync(0);
    }
}
