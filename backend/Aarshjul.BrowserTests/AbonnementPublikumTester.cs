using System.Net;
using System.Text.RegularExpressions;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Kalender;
using Aarshjul.Domain;
using Aarshjul.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

public sealed class AbonnementPublikumTester
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gruppen_ALT_og_fullt_innsyn_er_separate_eksplisitte_valg_med_eksakt_lagret_publikum_og_anonym_feed(bool fulltInnsyn)
    {
        var kontroll = "oppsett";
        try
        {
            await using var app = new Publikumsapp();
            using var anonym = app.CreateClient(); // Ingen identitetsheader eller cookie.
            using var scope = app.Services.CreateScope();
            var tjenester = scope.ServiceProvider;
            await tjenester.GetRequiredService<IGruppetjeneste>().OpprettAsync("ALT", "Avgrenset testgruppe");
            var rader = new[]
            {
                Rad("avgrenset", "ALT"), Rad("FA", "FA"), Rad("FAG", "FAG"),
                Rad("POL", "POL"), Rad("skjult"),
                Rad("upublisert", "ALT") with { Status = FristStatus.Forslag },
                Rad("historisk", "ALT") with { Dato = new(2020, 1, 1) }
            };
            await tjenester.GetRequiredService<IFristDatautveksling>().ImporterAsync(new() { Frister = rader });
            var abonnementer = tjenester.GetRequiredService<IKalenderabonnement>();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL")
            });
            await using var kontekst = await browser.NewContextAsync(new()
            {
                BaseURL = anonym.BaseAddress!.ToString(), Locale = "nb-NO", TimezoneId = "Europe/Oslo",
                ExtraHTTPHeaders = new Dictionary<string, string> { ["X-Test-Rolle"] = "Administrator" }
            });
            var side = await kontekst.NewPageAsync();
            side.SetDefaultTimeout(10_000);

            // Filutvalget har allerede separate gruppe- og Alt-kontroller. Samme lovlige
            // kode må fortsatt gi avgrenset eksport, også når fullt innsyn finnes ved siden av.
            kontroll = "filutvalg";
            await Klar(side, "/admin/eksport");
            var forventet = fulltInnsyn ? rader.Take(5).ToArray() : [rader[0]];
            if (fulltInnsyn) await side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new("^Alt ") }).CheckAsync();
            else await Gruppe(side).SelectOptionAsync(new SelectOptionValue { Label = "Avgrenset testgruppe (ALT)" });
            await side.GetByLabel("Fra dato", new() { Exact = true }).FillAsync("2037-01-01");
            await side.GetByRole(AriaRole.Button, new() { Name = "Kontroller utvalg", Exact = true }).ClickAsync();
            var preview = side.GetByRole(AriaRole.Region, new() { Name = "Eksportutvalg", Exact = true });
            await Expect(preview.Locator("tbody tr")).ToHaveCountAsync(forventet.Length);
            await Expect(preview).ToContainTextAsync(fulltInnsyn ? "Alt · FIN-internt" : "Avgrenset testgruppe (ALT)");
            foreach (var rad in rader)
            {
                if (forventet.Contains(rad)) await Expect(preview).ToContainTextAsync(rad.Tittel);
                else await Expect(preview).Not.ToContainTextAsync(rad.Tittel);
            }
            var filurl = await side.GetByRole(AriaRole.Link, new() { Name = "Last ned kalender (.ics)", Exact = true }).GetAttributeAsync("href");
            var fil = await kontekst.APIRequest.GetAsync(filurl!);
            Assert.True(fil.Status == 200, "Kalenderfilen kunne ikke hentes.");
            BekreftUtvalg(await fil.TextAsync(), forventet);

            await Klar(side, "/admin/abonnement");
            var gruppe = Gruppe(side);
            var opprett = side.GetByRole(AriaRole.Button, new() { Name = "Opprett lenke", Exact = true });
            await Expect(gruppe).ToHaveValueAsync("");
            await Expect(opprett).ToBeDisabledAsync();
            await Expect(side.Locator("#abonnement-utvalg")).ToContainTextAsync("POL krever et aktivt valg");
            var etikett = fulltInnsyn ? "Alt (fullt innsyn – FIN-internt)" : "Avgrenset testgruppe (ALT)";
            kontroll = "valgt publikum";
            await gruppe.SelectOptionAsync(new SelectOptionValue { Label = etikett });
            await Expect(side.Locator("#abonnement-utvalg")).ToHaveTextAsync($"Valgt publikum: {etikett}.");
            var bilde = fulltInnsyn ? "fullt-innsyn" : "gruppen-ALT";
            await AbonnementTester.TokenfrieBilder(side, bilde + "-valgt");
            kontroll = "opprettingsbekreftelse";
            await opprett.ClickAsync();
            var dialog = side.GetByRole(AriaRole.Dialog);
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(dialog.Locator("strong")).ToHaveTextAsync(etikett);
            await AbonnementTester.TokenfrieBilder(side, bilde + "-bekreftelse");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Ja, opprett lenke", Exact = true }).ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(side.GetByRole(AriaRole.Status)).ToContainTextAsync("er opprettet");
            await Expect(side.Locator("tbody tr")).ToHaveCountAsync(1);
            await Expect(side.Locator("tbody th")).ToHaveTextAsync(etikett);

            kontroll = "lagret publikum";
            var lagrede = await abonnementer.HentAlleAsync();
            Assert.True(lagrede.Count == 1, "UI-opprettingen lagret ikke ett abonnement.");
            var lagret = lagrede[0];
            Assert.True(lagret.GruppeKode == (fulltInnsyn ? null : "ALT") && lagret.ErAlt == fulltInnsyn,
                "Lagret abonnement har feil publikum.");
            // Hemmeligheten og bearer-URL-en forblir i minnet. Ingen trace, dump eller bilde.
            kontroll = "anonymt feedutvalg";
            using var svar = await anonym.GetAsync($"/kalender/feed/{lagret.Token}.ics");
            Assert.True(svar.StatusCode == HttpStatusCode.OK, "Den anonyme feeden kunne ikke hentes.");
            BekreftUtvalg(await svar.Content.ReadAsStringAsync(), forventet);
            kontroll = "ny lesing";
            await Klar(side, "/admin/abonnement");
            await Expect(side.Locator("tbody th")).ToHaveTextAsync(etikett);
            await Expect(gruppe).ToHaveValueAsync("");
            await Expect(opprett).ToBeDisabledAsync();
        }
        catch (Exception)
        {
            // ADR-0009: behold ingen URL, DOM/ARIA-snapshot eller indre unntak i TRX.
            throw new Xunit.Sdk.XunitException($"Abonnementets publikumskontroll feilet ved {kontroll}. Hemmelige lenker er skjult.");
        }
    }

    private static ILocator Gruppe(IPage side) => side.GetByRole(AriaRole.Combobox, new() { Name = "Gruppe", Exact = true });
    private static async Task Klar(IPage side, string sti)
    {
        await side.GotoAsync(sti);
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await Expect(Gruppe(side)).ToBeEnabledAsync();
    }
    private static void BekreftUtvalg(string ics, FristEksport[] forventet)
    {
        var utfoldet = Regex.Replace(ics, "\r\n[ \\t]", "");
        var ids = Regex.Matches(utfoldet, @"(?m)^UID:([0-9a-f-]+)@aarshjul\.fin\.dep\.no\r?$")
            .Select(m => Guid.Parse(m.Groups[1].Value)).Order().ToArray();
        Assert.True(ids.SequenceEqual(forventet.Select(f => f.Id).Order()), "Kalenderen inneholder feil fristutvalg.");
    }
    private static FristEksport Rad(string navn, params string[] grupper) => new()
    {
        Id = Guid.NewGuid(), Tittel = "Publikumskontroll " + navn, Dato = new(2037, 11, 20),
        Budsjettaar = 2038, Kategori = Kategori.Budsjett, Status = FristStatus.Godkjent, SynligFor = grupper
    };
    private sealed class Publikumsapp() : TestApplikasjon(brukKestrel: true)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStaticWebAssets();
            builder.ConfigureLogging(logg => logg.ClearProviders());
        }
    }
}
