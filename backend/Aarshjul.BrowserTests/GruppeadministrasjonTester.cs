using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class GruppeadministrasjonTester
{
    private static async Task OpprettSkok(Nettleser n)
    {
        await n.Gaa("/admin/grupper");
        var ny = n.Side.GetByRole(AriaRole.Form, new() { Name = "Ny synlighetsgruppe", Exact = true });
        await ny.GetByLabel("Kode", new() { Exact = true }).FillAsync("SKOK");
        await ny.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Skatteøkonomisk avdeling");
        await ny.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("opprettet");
    }

    private static async Task DeaktiverSkok(Nettleser n)
    {
        await n.Gaa("/admin/grupper");
        await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" })
            .GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true }).ClickAsync();
        var rediger = n.Side.GetByRole(AriaRole.Form, new() { Name = "Rediger synlighetsgruppe", Exact = true });
        await rediger.GetByLabel("Aktiv", new() { Exact = true }).UncheckAsync();
        await rediger.GetByRole(AriaRole.Button, new() { Name = "Lagre endringer", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" })).ToContainTextAsync("Inaktiv");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Inaktivt_forslag_far_bare_aktive_synlighetsvalg_ved_godkjenning(bool juster) => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await OpprettSkok(n);
        await n.LoggInn("demo-fa");
        await ForslagTester.Send(n, "AK gammelt SKOK-forslag", "SKOK");
        await n.LoggInn("demo-admin");
        await DeaktiverSkok(n);
        await n.Gaa("/admin/ko");
        var kort = ForslagTester.Kort(n.Side, "AK gammelt SKOK-forslag");
        await Expect(kort).ToContainTextAsync("SKOK");
        await kort.GetByRole(AriaRole.Button, new() { Name = juster ? "Juster" : "Godkjenn", Exact = true }).ClickAsync();
        var valg = juster ? n.Side.Locator("form.skjema") : kort;
        await Expect(valg.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("SKOK") })).ToHaveCountAsync(0);
        await Expect(valg.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await valg.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await valg.GetByRole(AriaRole.Button, new() { Name = juster ? "Lagre" : "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Godkjenningskø", Exact = true })).ToBeVisibleAsync();
        await Expect(ForslagTester.Kort(n.Side, "AK gammelt SKOK-forslag")).ToHaveCountAsync(0);
        var publisert = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK gammelt SKOK-forslag")!;
        Assert.Equal(new[] { "FA" }, publisert["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.LoggInn("demo-pol");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "AK gammelt SKOK-forslag");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Revidert_brukerforslag_fjerner_inaktivt_forhandsvalg_for_ny_innsending() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await OpprettSkok(n);
        await n.LoggInn("demo-fa");
        await ForslagTester.Send(n, "AK revidert SKOK-forslag", "SKOK");
        await n.LoggInn("demo-admin");
        await DeaktiverSkok(n);
        await n.Gaa("/admin/ko");
        var kort = ForslagTester.Kort(n.Side, "AK revidert SKOK-forslag");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Avvis", Exact = true }).ClickAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft avvisning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        await n.LoggInn("demo-fa");
        await n.Gaa("/forslag/mine");
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK revidert SKOK-forslag" });
        await rad.GetByRole(AriaRole.Link).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("SKOK") })).ToHaveCountAsync(0);
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Send inn", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Mine forslag", Exact = true })).ToBeVisibleAsync();
        await Expect(rad).ToContainTextAsync("Venter");
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/ko");
        kort = ForslagTester.Kort(n.Side, "AK revidert SKOK-forslag");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") })).ToBeCheckedAsync();
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK revidert SKOK-forslag")!;
        Assert.Equal(new[] { "FA" }, publisert["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Generering_med_inaktiv_viderefort_gruppe_kan_godkjennes_med_nytt_aktivt_valg() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await OpprettSkok(n);
        await n.OpprettFrist("AK fjorårets SKOK-frist", "SKOK");
        var original = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK fjorårets SKOK-frist")!;
        await n.Gaa($"/admin/frist/{(string)original["id"]!}");
        await n.Side.GetByLabel("Løp", new() { Exact = true }).FillAsync("ak-skok");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        await n.Gaa("/admin/mal/ny");
        await n.Side.GetByLabel("Løp (funksjonsnavn)").FillAsync("ak-skok");
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK videreført SKOK");
        await n.Side.GetByLabel("Regelparametre (JSON)").FillAsync("{\"maaned\":11,\"dag\":20}");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "AK videreført SKOK" })).ToBeVisibleAsync();
        await DeaktiverSkok(n);
        await n.Gaa("/admin/generer");
        await n.Side.GetByLabel("Målbudsjettår").FillAsync("2032");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Generér", Exact = true }).ClickAsync();
        var kort = ForslagTester.Kort(n.Side, "AK videreført SKOK");
        await Expect(kort).ToContainTextAsync("SKOK");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "AK videreført SKOK");
        await kort.GetByRole(AriaRole.Button, new() { Name = "Godkjenn", Exact = true }).ClickAsync();
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("SKOK") })).ToHaveCountAsync(0);
        await Expect(kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await kort.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await kort.GetByRole(AriaRole.Button, new() { Name = "Bekreft godkjenning", Exact = true }).ClickAsync();
        await Expect(kort).ToHaveCountAsync(0);
        var publisert = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK videreført SKOK")!;
        Assert.Equal(new[] { "FA" }, publisert["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.LoggInn("demo-pol");
        Assert.DoesNotContain(await n.Frister(), f => (string?)f!["tittel"] == "AK videreført SKOK");
        n.BekreftIngenKretsfeil();
    });

    [Fact]
    public Task Administrator_forvalter_grupper_uten_aa_endre_koder_eller_historisk_synlighet() => Nettleser.Kjor(async n =>
    {
        await n.LoggInn("demo-admin");
        await n.Gaa("/admin/grupper");
        var ny = n.Side.GetByRole(AriaRole.Form, new() { Name = "Ny synlighetsgruppe", Exact = true });
        await ny.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kode");
        await ny.GetByLabel("Kode", new() { Exact = true }).FillAsync("SK OK");
        await ny.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Skatteøkonomisk avdeling");
        await ny.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("Kode");
        await ny.GetByLabel("Kode", new() { Exact = true }).FillAsync("pol");
        await ny.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Alert)).ToContainTextAsync("finnes allerede");
        await ny.GetByLabel("Kode", new() { Exact = true }).FillAsync(" skok ");
        await ny.GetByRole(AriaRole.Button, new() { Name = "Opprett gruppe", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("opprettet");
        await n.Gaa("/admin/grupper");
        var rad = n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" });
        await Expect(rad).ToContainTextAsync("Skatteøkonomisk avdeling");
        await Expect(rad).ToContainTextAsync("Egendefinert");
        await Expect(n.Side.GetByRole(AriaRole.Button, new() { Name = "Slett", Exact = true })).ToHaveCountAsync(0);

        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.OpprettFrist("AK egendefinert avdelingsfrist", "SKOK");
        var frist = (await n.Frister()).Single(f => (string?)f!["tittel"] == "AK egendefinert avdelingsfrist")!;
        var id = (string)frist["id"]!;
        Assert.Equal(new[] { "SKOK" }, frist["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());

        await n.Gaa("/admin/grupper");
        await rad.GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true }).ClickAsync();
        var rediger = n.Side.GetByRole(AriaRole.Form, new() { Name = "Rediger synlighetsgruppe", Exact = true });
        await Expect(rediger.GetByLabel("Kode", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(rediger).ToContainTextAsync("SKOK");
        await rediger.GetByLabel("Visningsnavn", new() { Exact = true }).FillAsync("Nytt avdelingsnavn <test>");
        await rediger.GetByRole(AriaRole.Button, new() { Name = "Lagre endringer", Exact = true }).ClickAsync();
        await Expect(rad).ToContainTextAsync("Nytt avdelingsnavn <test>");
        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nytt avdelingsnavn <test> (SKOK)", Exact = true })).ToBeVisibleAsync();

        await n.Gaa("/admin/grupper");
        await rad.GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true }).ClickAsync();
        await rediger.GetByLabel("Aktiv", new() { Exact = true }).UncheckAsync();
        await rediger.GetByRole(AriaRole.Button, new() { Name = "Lagre endringer", Exact = true }).ClickAsync();
        await Expect(rad).ToContainTextAsync("Inaktiv");
        await n.Gaa("/admin/grupper");
        await Expect(rad).ToContainTextAsync("Inaktiv");
        var historisk = (await n.Frister()).Single(f => (string)f!["id"]! == id)!;
        Assert.Equal(new[] { "SKOK" }, historisk["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.Gaa($"/admin/frist/{id}");
        var inaktivtValg = n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nytt avdelingsnavn <test> (SKOK) – inaktiv, eksisterende tilordning", Exact = true });
        await Expect(inaktivtValg).ToBeVisibleAsync();
        await Expect(inaktivtValg).ToBeCheckedAsync();
        await n.Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync("AK innhold redigert etter deaktivering");
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        var endret = (await n.Frister()).Single(f => (string)f!["id"]! == id)!;
        Assert.Equal("AK innhold redigert etter deaktivering", (string?)endret["tittel"]);
        Assert.Equal(new[] { "SKOK" }, endret["synlighet"]!.AsArray().Select(x => (string)x!).ToArray());
        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(SKOK\\)$") })).ToHaveCountAsync(0);
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(POL\\)$") })).Not.ToBeCheckedAsync();
        await n.Gaa("/admin/abonnement");
        await Expect(n.Side.Locator("option[value='SKOK']")).ToHaveCountAsync(0);

        foreach (var persona in new[] { "demo-fa", "demo-pol" })
        {
            await n.LoggInn(persona);
            Assert.DoesNotContain(await n.Frister(), f => (string)f!["id"]! == id);
            await n.Side.GotoAsync("/admin/grupper");
            await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Velg hvem du vil være" })).ToBeVisibleAsync();
        }

        await n.LoggInn("demo-admin");
        await n.Gaa($"/admin/frist/{id}");
        inaktivtValg = n.Side.GetByRole(AriaRole.Checkbox, new() { Name = "Nytt avdelingsnavn <test> (SKOK) – inaktiv, eksisterende tilordning", Exact = true });
        await Expect(inaktivtValg).ToBeCheckedAsync();
        await inaktivtValg.UncheckAsync();
        await n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(FA\\)$") }).CheckAsync();
        await n.Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
        await n.Gaa($"/admin/frist/{id}");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("SKOK") })).ToHaveCountAsync(0);
        await n.Gaa("/admin/grupper");
        await n.Side.GetByRole(AriaRole.Row).Filter(new() { HasText = "SKOK" })
            .GetByRole(AriaRole.Button, new() { Name = "Rediger", Exact = true }).ClickAsync();
        rediger = n.Side.GetByRole(AriaRole.Form, new() { Name = "Rediger synlighetsgruppe", Exact = true });
        await rediger.GetByLabel("Aktiv", new() { Exact = true }).CheckAsync();
        await rediger.GetByRole(AriaRole.Button, new() { Name = "Lagre endringer", Exact = true }).ClickAsync();
        await Expect(n.Side.GetByRole(AriaRole.Status)).ToContainTextAsync("lagret");
        await n.Gaa("/admin/frist/ny");
        await Expect(n.Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("\\(SKOK\\)$") })).ToBeVisibleAsync();
        n.BekreftIngenKretsfeil();
    });
}
