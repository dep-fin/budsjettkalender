using System.Text.Json;
using System.Data.Common;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Epost;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Epost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aarshjul.Testkontrakter;

public abstract partial class DagligEpostKontrakt
{
    [Fact]
    public async Task Import_samler_bare_faktisk_ny_endret_og_fjernet_publisert_identitet()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        IFristDatautveksling import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var uendret = Importrad("Uendret"); var endret = Importrad("Endret"); var slettet = Importrad("Fjernet");
        var upublisert = Importrad("Aldri publisert") with { Status = FristStatus.Forslag };
        await import.ImporterAsync(new() { Frister = [uendret, endret, slettet, upublisert] });
        await p.Aktiver(); await VelgUtvalg(p, null, null, true, true);
        var ny = Importrad("Helt ny");
        var resultat = await import.ImporterAsync(new() { Frister = [uendret, endret with { Dato = new(2030, 12, 5) }, ny] });
        Assert.Equal(4, resultat.AntallErstattet); Assert.Equal(3, resultat.AntallImportert);
        await p.Jobb().KjorAsync(); var melding = Assert.Single(p.Post.Hent("a"));
        Assert.Contains("Ny frist: Helt ny", melding.Tekst); Assert.Contains("Vesentlig endring: Endret", melding.Tekst);
        Assert.Contains("Tidligere: 20.11.2030", melding.Tekst); Assert.Contains("05.12.2030", melding.Tekst);
        Assert.Contains("Fjernet frist: Fjernet", melding.Tekst);
        Assert.DoesNotContain("Uendret", melding.Tekst); Assert.DoesNotContain("Aldri publisert", melding.Tekst);
        await using var kontroll = db.Fabrikk().CreateDbContext();
        Assert.Equal(3, await kontroll.Oppsummeringshendelser.CountAsync());
    }

    [Fact]
    public async Task Identisk_import_med_annen_grupperekkefolge_og_tentativ_referansedag_bevarer_sekvens_og_hendelser()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rad = Importrad("Tentativt") with { Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio, SynligFor = ["FAG", "FA"] };
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver();
        await VelgUtvalg(p, null, null, true, true); var foer = await Varslingsjournal(db);
        rad = rad with { Dato = new(2030, 11, 3), Klokkeslett = new(22, 10), SynligFor = ["FA", "FAG", "FA"] };
        await import.ImporterAsync(new() { Frister = [rad] }); await import.ImporterAsync(new() { Frister = [rad] });
        Assert.Equal(foer, await Varslingsjournal(db)); await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
    }

    [Theory]
    [InlineData("dato", true)] [InlineData("tid", true)] [InlineData("heldag", true)]
    [InlineData("presisjon", true)] [InlineData("maaned", true)] [InlineData("kvalifikator", true)]
    [InlineData("aar", true)] [InlineData("kategori", true)] [InlineData("referansedag", false)]
    [InlineData("tittel", false)] [InlineData("notat", false)] [InlineData("kilde", false)]
    public async Task Import_bruker_felles_semantisk_vesentlighet_og_korrekturregel(string felt, bool varsles)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rad = Importrad("Semantisk import") with { Klokkeslett = new(13, 45) };
        if (felt is "maaned" or "kvalifikator" or "referansedag")
            rad = rad with { Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio };
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver(); await VelgUtvalg(p, null, null, false, true);
        var etter = felt switch
        {
            "dato" => rad with { Dato = new(2030, 12, 5) }, "tid" => rad with { Klokkeslett = new(14, 0) },
            "heldag" => rad with { Klokkeslett = null }, "presisjon" => rad with { Datopresisjon = Datopresisjon.Maaned },
            "maaned" => rad with { Dato = new(2030, 12, 20) }, "kvalifikator" => rad with { Datokvalifikator = Datokvalifikator.Primo },
            "aar" => rad with { Budsjettaar = 2032 }, "kategori" => rad with { Kategori = Kategori.Regnskap },
            "referansedag" => rad with { Dato = new(2030, 11, 3) }, "tittel" => rad with { Tittel = "Korrektur" },
            "notat" => rad with { Notat = "Korrektur" }, _ => rad with { Kilde = "Korrigert kilde" }
        };
        await import.ImporterAsync(new() { Frister = [etter] }); await p.Jobb().KjorAsync();
        Assert.Equal(varsles ? 1 : 0, p.Post.Hent("a").Count);
        if (varsles) Assert.Contains("Vesentlig endring: Semantisk import", Assert.Single(p.Post.Hent("a")).Tekst);
    }

    [Theory]
    [InlineData("ny", true, false)] [InlineData("ny", false, true)] [InlineData("ny", true, true)]
    [InlineData("etablert", true, false)] [InlineData("etablert", false, true)] [InlineData("etablert", true, true)]
    [InlineData("sendt", true, false)] [InlineData("sendt", false, true)] [InlineData("sendt", true, true)]
    public async Task Ny_endret_fjernet_bruker_mottakerens_livssyklus_uavhengig_av_valgte_varseltyper(string liv, bool nye, bool endringer)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke); var rad = Importrad("Kort livsløp");
        if (liv == "etablert") await import.ImporterAsync(new() { Frister = [rad] });
        await p.Aktiver(); await VelgUtvalg(p, null, null, nye, endringer);
        if (liv != "etablert") await import.ImporterAsync(new() { Frister = [rad] });
        rad = rad with { Dato = new(2030, 12, 5) }; await import.ImporterAsync(new() { Frister = [rad] });
        if (liv == "sendt") { await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a")); p.Klokke.Flytt(TimeSpan.FromDays(1)); }
        await import.ImporterAsync(new() { Frister = [] }); await p.Jobb().KjorAsync();
        var antall = (liv == "sendt" ? 1 : 0) + (liv != "ny" && endringer ? 1 : 0);
        Assert.Equal(antall, p.Post.Hent("a").Count);
        if (liv != "ny" && endringer) Assert.Contains("Fjernet frist: Kort livsløp", p.Post.Hent("a").Last().Tekst);
        await using var kontroll = db.Fabrikk().CreateDbContext();
        if (liv == "ny") Assert.Empty(await kontroll.Oppsummeringshendelser.ToListAsync());
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Gjeninnfort_eksplisitt_identitet_faar_aldri_en_ekstra_Ny_eller_gammelt_slettingsvarsel(bool legacy, bool endret)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke); var rad = Importrad("Gjeninnført");
        if (legacy)
        {
            await using var oppsett = db.Fabrikk().CreateDbContext();
            oppsett.Frister.Add(new() { Id = rad.Id, Tittel = rad.Tittel, Dato = rad.Dato, Budsjettaar = rad.Budsjettaar,
                Kategori = rad.Kategori, Status = FristStatus.Godkjent, Synlighet = [new() { GruppeKode = "FAG" }] });
            await oppsett.SaveChangesAsync();
        }
        else await import.ImporterAsync(new() { Frister = [rad] });
        await p.Aktiver(); await VelgUtvalg(p, null, null, true, true);
        await import.ImporterAsync(new() { Frister = [] });
        if (endret) rad = rad with { Dato = new(2030, 12, 5) };
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Jobb().KjorAsync();
        Assert.Equal(endret ? 1 : 0, p.Post.Hent("a").Count);
        if (endret) { var post = Assert.Single(p.Post.Hent("a")); Assert.Contains("Vesentlig endring: Gjeninnført", post.Tekst); Assert.DoesNotContain("Fjernet", post.Tekst); }
        Assert.Equal(rad.Id, Assert.Single((await import.EksporterAsync()).Frister).Id);
        await using var kontroll = db.Fabrikk().CreateDbContext();
        Assert.Equal(legacy ? 0 : 1, await kontroll.NyFristHendelser.CountAsync(h => h.Type == "Ny"));
    }

    [Theory]
    [InlineData("FAG", "FAG", false, true)] [InlineData("FA", "FA", false, true)]
    [InlineData("POL", "FAG", false, false)] [InlineData("POL", "POL", false, true)]
    [InlineData("", "FAG", false, false)] [InlineData("", "", true, true)]
    [InlineData("FA", "FAG", false, false)]
    public async Task Sletting_bruker_historisk_synlighet_og_fersk_egen_rolle(string historisk, string gruppe, bool admin, bool varsles)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rad = Importrad("Historisk publikum") with { SynligFor = historisk == "" ? [] : [historisk] };
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver(); await VelgUtvalg(p, null, null, false, true);
        await import.ImporterAsync(new() { Frister = [] });
        p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(admin, gruppe == "" ? [] : [gruppe])));
        if (historisk != "")
        {
            await using var oppsett = db.Fabrikk().CreateDbContext();
            oppsett.Synlighetsgrupper.Remove(await oppsett.Synlighetsgrupper.SingleAsync(g => g.Kode == historisk));
            await oppsett.SaveChangesAsync(); // Historisk publikum har ingen FK til dagens grupperegister.
        }
        await p.Jobb().KjorAsync(); Assert.Equal(varsles ? 1 : 0, p.Post.Hent("a").Count);
    }

    [Theory]
    [InlineData(2, "gruppe")] [InlineData(3, "gruppe")] [InlineData(2, "konto")]
    [InlineData(3, "konto")] [InlineData(2, "feil")] [InlineData(3, "feil")]
    [InlineData(2, "admin")] [InlineData(3, "admin")]
    public async Task Sletting_stanser_ved_fersk_tilbakekalling_eller_tilgangsfeil_foer_begge_innholdskontroller(int kontroll, string feil)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        await import.ImporterAsync(new() { Frister = [Importrad("Tilbakekalt sletting") with { SynligFor = [feil == "admin" ? "POL" : "FAG"] }] });
        await p.Aktiver(); await VelgUtvalg(p, null, null, false, true); await import.ImporterAsync(new() { Frister = [] });
        var kilde = new Tilgangsadapter(n =>
        {
            if (n >= kontroll && feil == "feil") throw new InvalidOperationException("Ikke logg kildeinnhold");
            return Task.FromResult(n >= kontroll
                ? feil == "konto" ? new Mottakertilgang(MottakertilgangStatus.Inaktiv) : new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, []))
                : new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(feil == "admin", ["FAG"])));
        });
        await p.Jobb(tilgang: kilde).KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        await using var dbkontroll = db.Fabrikk().CreateDbContext();
        Assert.All(await dbkontroll.Epostoppsummeringer.ToListAsync(), x => Assert.Equal("Planlagt", x.Status));
        Assert.Empty(await dbkontroll.Oppsummeringshendelser.ToListAsync());
    }

    [Theory]
    [InlineData(2031, Kategori.Budsjett, true)] [InlineData(2032, Kategori.Regnskap, true)]
    [InlineData(2031, Kategori.Regnskap, false)] [InlineData(2032, Kategori.Budsjett, false)]
    public async Task Importendring_treffer_hele_foer_eller_hele_etter_aldri_krysstreff(int aar, Kategori kategori, bool varsles)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke); var rad = Importrad("Flyttet import");
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver(); await VelgUtvalg(p, [aar], [kategori], false, true);
        await import.ImporterAsync(new() { Frister = [rad with { Budsjettaar = 2032, Kategori = Kategori.Regnskap }] });
        await p.Jobb().KjorAsync(); Assert.Equal(varsles ? 1 : 0, p.Post.Hent("a").Count);
    }

    [Fact]
    public async Task Sletting_filtrerer_siste_hele_utvalg_foer_gruppering_forklaring_og_plankoblinger()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var input = Inndata("Tidligere skjult tekst"); var id = await p.Skriv.OpprettAsync(input);
        await p.Aktiver(); await VelgUtvalg(p, [2031], [Kategori.Budsjett], false, true);
        input.Notat = "Gammel begrunnelse skal utelates"; input.VesentligEndring = true; input.Endringsforklaring = "Skjult historisk forklaring";
        await p.Skriv.OppdaterAsync(id, input);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke); var fil = await import.EksporterAsync();
        var siste = fil.Frister.Single() with { Tittel = "Siste publiserte", Notat = "Siste notat", Budsjettaar = 2032, Kategori = Kategori.Regnskap };
        await import.ImporterAsync(new() { Frister = [siste] }); await import.ImporterAsync(new() { Frister = [] });
        await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a")); // Førutvalg på gammel Endret kan ikke slippe inn slettingen.
        await VelgUtvalg(p, null, null, false, true); // Ingen tilbakefylling av allerede skjedd bortfall.
        await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        // En annen abonnent var aktiv i siste utvalg før bortfallet.
        await p.Aktiver("b");
        var b = await p.Abonnement.HentAsync("b"); await p.Abonnement.EndreValgAsync("b", false, true, b.Versjon, [2032], [Kategori.Regnskap]);
        await import.ImporterAsync(new() { Frister = [siste] }); await import.ImporterAsync(new() { Frister = [] }); await p.Jobb().KjorAsync();
        var melding = Assert.Single(p.Post.Hent("b")); Assert.Contains("Siste publiserte", melding.Tekst);
        Assert.DoesNotContain("Skjult historisk forklaring", melding.Tekst); Assert.DoesNotContain("Tidligere skjult tekst", melding.Tekst);
        await using var kontroll = db.Fabrikk().CreateDbContext();
        var koblet = await kontroll.Oppsummeringshendelser.Where(k => k.Oppsummering.BrukerId == "b").Select(k => k.HendelseId).ToListAsync();
        Assert.Equal("Slettet", (await kontroll.NyFristHendelser.SingleAsync(h => h.Id == koblet.Single())).Type);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Importfeil_etter_faktiske_skriv_ruller_tilbake_frister_synlighet_hendelser_grupper_og_sekvens_med_samme_tjeneste_retry(bool tom)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        IFristDatautveksling import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        await import.ImporterAsync(new() { Frister = [Importrad("EV07 original")] });
        await p.Aktiver(); await VelgUtvalg(p, null, null, true, true);
        var foer = JsonSerializer.Serialize((await import.EksporterAsync()).Frister);
        var journal = await Varslingsjournal(db);
        var fil = new FristDatabase { Frister = tom ? [] : [Importrad("EV07 kandidat")] };
        await using (var oppsett = db.Fabrikk().CreateDbContext())
        {
            var sql = oppsett.Database.IsSqlServer()
                ? tom
                    ? "CREATE TRIGGER TR_EV07_fail ON NyFristHendelser AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Type = 'Slettet') THROW 51000, 'EV07-final-write', 1; END"
                    : "CREATE TRIGGER TR_EV07_fail ON Frister AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Tittel = 'EV07 kandidat') AND NOT EXISTS(SELECT 1 FROM Frister WHERE Tittel = 'EV07 original') AND EXISTS(SELECT 1 FROM NyFristHendelser h JOIN FristHendelsessynlighet s ON s.HendelseId = h.Id WHERE h.Type = 'Slettet' AND s.GruppeKode = 'FAG') AND EXISTS(SELECT 1 FROM NyFristHendelser WHERE Type = 'Ny' AND EtterJson LIKE '%EV07 kandidat%') THROW 51000, 'EV07-final-write', 1; END"
                : tom
                    ? "CREATE TRIGGER TR_EV07_fail AFTER INSERT ON NyFristHendelser WHEN NEW.Type = 'Slettet' BEGIN SELECT RAISE(ABORT, 'EV07-final-write'); END;"
                    : "CREATE TRIGGER TR_EV07_fail AFTER INSERT ON Frister WHEN NEW.Tittel = 'EV07 kandidat' AND NOT EXISTS(SELECT 1 FROM Frister WHERE Tittel = 'EV07 original') AND EXISTS(SELECT 1 FROM NyFristHendelser h JOIN FristHendelsessynlighet s ON s.HendelseId = h.Id WHERE h.Type = 'Slettet' AND s.GruppeKode = 'FAG') AND EXISTS(SELECT 1 FROM NyFristHendelser WHERE Type = 'Ny' AND EtterJson LIKE '%EV07 kandidat%') BEGIN SELECT RAISE(ABORT, 'EV07-final-write'); END;";
            await oppsett.Database.ExecuteSqlRawAsync(sql);
        }
        var feil = await Assert.ThrowsAsync<DbUpdateException>(() => import.ImporterAsync(fil));
        Assert.Contains("EV07-final-write", feil.InnerException!.Message);
        Assert.Equal(foer, JsonSerializer.Serialize((await import.EksporterAsync()).Frister));
        Assert.Equal(journal, await Varslingsjournal(db)); await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        await using (var oppsett = db.Fabrikk().CreateDbContext()) await oppsett.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_EV07_fail");
        await import.ImporterAsync(fil); await p.Jobb().KjorAsync();
        var melding = Assert.Single(p.Post.Hent("a")); Assert.Contains("Fjernet frist: EV07 original", melding.Tekst);
        Assert.Equal(!tom, melding.Tekst.Contains("Ny frist: EV07 kandidat", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("aktivering", false, 0)] [InlineData("aktivering", true, 0)]
    [InlineData("aktivering", false, -1)] [InlineData("aktivering", true, -1)]
    [InlineData("utvidelse", false, 0)] [InlineData("utvidelse", true, 0)]
    [InlineData("utvidelse", false, -1)] [InlineData("utvidelse", true, -1)]
    public async Task Import_og_abonnementsgrense_ordnes_etter_commit_med_lik_eller_bakovergaaende_UTC(string handling, bool abonnentForsinket, int ticks)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        if (handling == "aktivering") await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        else { await p.Aktiver(); await VelgUtvalg(p, [2031], null); }
        p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, ["FAG"])));
        var fil = new FristDatabase { Frister = [Importrad("Grenseordnet import") with { Budsjettaar = 2032 }] };
        var ankommet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lagringsport = new FoerLagring(async _ => { ankommet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15)); });
        var leseport = new FoerImportlesing(ankommet, fortsett);
        var status = await p.Abonnement.HentAsync("a");
        Task Abonnement(bool vent) => handling == "aktivering"
            ? new EpostabonnementTjeneste(db.Fabrikk(vent ? lagringsport : null), p.Klokke, p.Bekreftelsespost).BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token)
            : new EpostabonnementTjeneste(db.Fabrikk(vent ? lagringsport : null), p.Klokke, p.Bekreftelsespost).EndreValgAsync("a", true, false, status.Versjon, null, null);
        Task ventende = abonnentForsinket ? Abonnement(true)
            : new FristDatautveksling(db.FabrikkMedKommando(leseport), p.Klokke).ImporterAsync(fil);
        try
        {
            await ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15)); p.Klokke.Flytt(TimeSpan.FromTicks(ticks));
            if (abonnentForsinket) await import.ImporterAsync(fil); else await Abonnement(false);
            fortsett.TrySetResult(); await ventende; await p.Jobb().KjorAsync();
            Assert.Equal(abonnentForsinket ? 0 : 1, p.Post.Hent("a").Count);
        }
        finally { fortsett.TrySetResult(); await ventende; }
    }

    private sealed class FoerImportlesing(TaskCompletionSource ankommet, TaskCompletionSource fortsett) : DbCommandInterceptor
    {
        private int _brukt;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("Synlighetsgrupper", StringComparison.Ordinal) && Interlocked.Exchange(ref _brukt, 1) == 0)
            { ankommet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }

    [Theory]
    [InlineData("abonnement")] [InlineData("innhold")] [InlineData("gjeninnfort")]
    public async Task Import_sletting_revalideres_etter_siste_planlagring_foer_overlevering(string endring)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke); var rad = Importrad("Opprinnelig sletting");
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver(); await VelgUtvalg(p, null, null, true, true);
        await import.ImporterAsync(new() { Frister = [] }); var endret = false;
        var port = new FoerLagring(async kontekst =>
        {
            if (endret || !kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(e => e.State == EntityState.Added)) return;
            endret = true;
            if (endring == "abonnement") await VelgUtvalg(p, [2040], null, true, true);
            else
            {
                await import.ImporterAsync(new() { Frister = [rad with { Tittel = "Ny siste tilstand", Dato = new(2030, 12, 5) }] });
                if (endring == "innhold") await import.ImporterAsync(new() { Frister = [] });
            }
        });
        var resultat = await p.Jobb(port).KjorAsync(); Assert.True(endret); Assert.Empty(p.Post.Hent("a"));
        Assert.Equal("EndretFoerOverlevering", Assert.Single(resultat.Mottakere).Status);
        await using var kontroll = db.Fabrikk().CreateDbContext(); Assert.Empty(await kontroll.Oppsummeringshendelser.ToListAsync());
    }

    [Fact]
    public async Task Gammel_v1_import_med_tom_identitet_tildeles_en_gang_uten_abonnentdata_i_fristfilen()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var gammel = JsonSerializer.Deserialize<FristDatabase>("""{"Versjon":1,"Frister":[{"Tittel":"Gammel fil","Dato":"2030-11-20","Budsjettaar":2031,"Status":1,"SynligFor":["FAG"]}]}""")!;
        Assert.Equal(Guid.Empty, Assert.Single((await import.ForhaandsvisAsync(gammel)).Frister).Id);
        await import.ImporterAsync(gammel); var eksport = await import.EksporterAsync(); var id = Assert.Single(eksport.Frister).Id;
        Assert.NotEqual(Guid.Empty, id); await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        var json = JsonSerializer.Serialize(eksport);
        Assert.DoesNotContain("egen@fin.dep.no", json); Assert.DoesNotContain("Bekreftelses", json); Assert.DoesNotContain("Publiseringssekvens", json);
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await import.ImporterAsync(eksport); await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        Assert.Equal(id, Assert.Single((await import.EksporterAsync()).Frister).Id);
    }

    [Theory]
    [InlineData(FristStatus.Forslag, FristStatus.Godkjent, "Ny frist: Publisert etter import")]
    [InlineData(FristStatus.Godkjent, FristStatus.Forslag, "Fjernet frist: Siste godkjente")]
    [InlineData(FristStatus.Godkjent, FristStatus.Fullfoert, "Fjernet frist: Siste godkjente")]
    [InlineData(FristStatus.Forslag, FristStatus.Avvist, null)]
    public async Task Import_sammenlikner_publisert_tilstand_og_gjengir_aldri_upublisert_etterinnhold(FristStatus foer, FristStatus etter, string? forventet)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rad = Importrad("Siste godkjente") with { Status = foer };
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Aktiver(); await VelgUtvalg(p, null, null, true, true);
        var tittel = etter == FristStatus.Godkjent ? "Publisert etter import" : "Upublisert hemmelig etterinnhold";
        await import.ImporterAsync(new() { Frister = [rad with { Status = etter, Tittel = tittel }] });
        await p.Jobb().KjorAsync();
        if (forventet is null) Assert.Empty(p.Post.Hent("a"));
        else
        {
            var melding = Assert.Single(p.Post.Hent("a")); Assert.Contains(forventet, melding.Tekst);
            Assert.DoesNotContain("Upublisert hemmelig etterinnhold", melding.Tekst);
        }
    }

    [Fact]
    public async Task Manglende_frist_uten_atomisk_slettingshendelse_gir_ingen_historisk_fallback()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var id = await p.Skriv.OpprettAsync(Inndata("Ikke et slettingsvarsel")); await p.Aktiver(); await VelgUtvalg(p, null, null, false, true);
        var input = Inndata("Ikke et slettingsvarsel"); input.Dato = new(2030, 12, 5); await p.Skriv.OppdaterAsync(id, input);
        await using (var oppsett = db.Fabrikk().CreateDbContext())
        { oppsett.Frister.Remove(await oppsett.Frister.SingleAsync()); await oppsett.SaveChangesAsync(); }
        await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
    }

    private static async Task<string> Varslingsjournal(Epostdatabase database)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        return JsonSerializer.Serialize(new
        {
            Hendelser = await db.NyFristHendelser.AsNoTracking().OrderBy(h => h.Id).ToListAsync(),
            Grupper = await db.Set<FristHendelsessynlighet>().AsNoTracking().OrderBy(s => s.HendelseId).ThenBy(s => s.GruppeKode).ToListAsync(),
            Sekvens = await db.Database.SqlQueryRaw<long>("SELECT Verdi AS Value FROM Epostrekkefolge WHERE Id = 1").SingleAsync()
        });
    }
}
