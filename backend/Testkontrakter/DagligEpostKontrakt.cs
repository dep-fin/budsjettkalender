using Aarshjul.Application.Epost;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Application;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Infrastructure.Frister;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace Aarshjul.Testkontrakter;

public abstract class DagligEpostKontrakt
{
    protected abstract Task<Epostdatabase> OpprettAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Innlest_UnderSending_plan_kan_ikke_overtas_naar_en_ny_hendelse_ankommer(bool uavklart)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første frist"));
        await p.Jobb(post: new UtilgjengeligOppsummeringspost()).KjorAsync();
        var port = new FoerPlanlesing(); var post = new VentendePost(uavklart);
        IDagligEpostoppsummering b = new DagligEpostoppsummering(db.FabrikkMedKommando(port), p.Klokke, p.Tilgang, post,
            Options.Create(new DagligEpostOpsjoner()));
        var jobbB = b.KjorAsync();
        Task<Oppsummeringsresultat>? jobbA = null;
        try
        {
            await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            jobbA = p.Jobb(post: post).KjorAsync();
            await post.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await p.Skriv.OpprettAsync(Inndata("Ny frist mens A sender"));
            if (uavklart) { post.Fortsett.TrySetResult(); await jobbA; }
            port.Fortsett.TrySetResult();
            var svarB = await jobbB.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("Uavklart", Assert.Single(svarB.Mottakere).Status);
            post.Fortsett.TrySetResult();
            Assert.Equal(uavklart ? "Uavklart" : "Bekreftet", Assert.Single((await jobbA).Mottakere).Status);
            Assert.Equal(1, post.Antall);
            p.Klokke.Flytt(TimeSpan.FromDays(1)); var nesteSvar = await p.Jobb().KjorAsync();
            if (uavklart)
            {
                Assert.Equal("Uavklart", Assert.Single(nesteSvar.Mottakere).Status);
                Assert.Empty(p.Post.Hent("a"));
            }
            else
            {
                var neste = Assert.Single(p.Post.Hent("a"));
                Assert.Contains("Ny frist mens A sender", neste.Tekst); Assert.DoesNotContain("Første frist", neste.Tekst);
            }
        }
        finally
        {
            port.Fortsett.TrySetResult(); post.Fortsett.TrySetResult();
            await jobbB;
            if (jobbA is not null) await jobbA;
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Publisering_startet_foer_men_fullfoert_etter_aktivering_varsles_uavhengig_av_UTC_ticks(int tidsforskjell)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        var aktiveringstid = p.Klokke.GetUtcNow();
        // En allerede publisert frist skal utelates selv om klokken viser en senere tid.
        p.Klokke.Flytt(TimeSpan.FromTicks(2));
        await p.Skriv.OpprettAsync(Inndata("Historisk, også med senere klokke"));
        p.Klokke.Flytt(aktiveringstid.AddTicks(tidsforskjell) - p.Klokke.GetUtcNow());
        var startet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = new FoerLagring(async _ => { startet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15)); });
        IFristskriving forsinket = new FristskrivingTjeneste(db.Fabrikk(port), p.Klokke);
        var publisering = forsinket.OpprettAsync(Inndata("Publisert etter aktivering"));
        try
        {
            await startet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            p.Klokke.Flytt(aktiveringstid - p.Klokke.GetUtcNow());
            await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
            p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, ["FAG"])));
            fortsett.TrySetResult(); await publisering;
            Assert.Equal("Bekreftet", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
            var melding = Assert.Single(p.Post.Hent("a"));
            Assert.Contains("Publisert etter aktivering", melding.Tekst);
            Assert.DoesNotContain("Historisk", melding.Tekst);
            p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb().KjorAsync();
            Assert.Single(p.Post.Hent("a"));
        }
        finally { fortsett.TrySetResult(); await publisering; }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Aktivering_startet_foer_men_fullfoert_etter_publisering_tilbakefyller_ikke(int tidsforskjell)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        var startet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = new FoerLagring(async _ => { startet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15)); });
        IEpostabonnement forsinket = new EpostabonnementTjeneste(db.Fabrikk(port), p.Klokke, p.Bekreftelsespost);
        var aktivering = forsinket.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        try
        {
            await startet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            p.Klokke.Flytt(TimeSpan.FromTicks(tidsforskjell));
            await p.Skriv.OpprettAsync(Inndata("Publisert før vellykket aktivering"));
            fortsett.TrySetResult(); await aktivering;
            p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, ["FAG"])));
            Assert.Equal("IngenHendelser", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
            Assert.Empty(p.Post.Hent("a"));
            await p.Skriv.OpprettAsync(Inndata("Etter aktivering med samme klokke"));
            await p.Jobb().KjorAsync();
            var melding = Assert.Single(p.Post.Hent("a"));
            Assert.Contains("Etter aktivering med samme klokke", melding.Tekst);
            Assert.DoesNotContain("Publisert før", melding.Tekst);
        }
        finally { fortsett.TrySetResult(); await aktivering; }
    }

    [Theory]
    [InlineData("ubekreftet")]
    [InlineData("avmeldt")]
    [InlineData("inaktiv")]
    [InlineData("uavklart")]
    [InlineData("kildefeil")]
    [InlineData("gruppeloes")]
    [InlineData("POL")]
    [InlineData("transport")]
    [InlineData("nyeAv")]
    public async Task Ikke_autorisert_mottaker_faar_aldri_innhold_i_postadapteren(string tilstand)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        if (tilstand == "ubekreftet") await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        else await p.Aktiver();
        if (tilstand == "avmeldt") await p.Abonnement.MeldAvAsync("a");
        if (tilstand is "inaktiv" or "uavklart") p.Tilgang.Sett("a", new(tilstand == "inaktiv" ? MottakertilgangStatus.Inaktiv : MottakertilgangStatus.Uavklart));
        if (tilstand is "gruppeloes" or "POL") p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, tilstand == "POL" ? ["POL"] : [])));
        if (tilstand == "nyeAv")
        {
            await using var kontekst = db.Fabrikk().CreateDbContext();
            var rad = await kontekst.Epostabonnementer.SingleAsync(); rad.NyeFrister = false;
            await kontekst.SaveChangesAsync();
        }
        await p.Skriv.OpprettAsync(Inndata("Bare FAG, aldri automatisk POL eller jobbadmin"));
        var kilde = tilstand == "kildefeil" ? new Tilgangsadapter(_ => throw new InvalidOperationException("hemmelig kildefeil")) : null;
        var resultat = await p.Jobb(tilgang: kilde, post: tilstand == "transport" ? new UtilgjengeligOppsummeringspost() : null).KjorAsync();
        Assert.Empty(p.Post.Hent("a"));
        if (tilstand is "inaktiv" or "uavklart" or "kildefeil" or "transport") Assert.Single(resultat.Mottakere);
    }

    [Fact]
    public async Task Gamle_frister_foer_aktivering_og_redigering_gir_ingen_historisk_oppsummering()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var gammel = await p.Skriv.OpprettAsync(Inndata("Før aktivering"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(1)); await p.Aktiver();
        await p.Skriv.OppdaterAsync(gammel, Inndata("Korrektur etter aktivering"));
        Assert.Equal("IngenHendelser", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        Assert.Empty(p.Post.Hent("a"));
        await p.Abonnement.MeldAvAsync("a");
        await p.Skriv.OpprettAsync(Inndata("Mens avmeldt"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2)); await p.Aktiver();
        await p.Jobb().KjorAsync();
        Assert.Empty(p.Post.Hent("a"));
        await p.Skriv.OpprettAsync(Inndata("Etter ny aktivering"));
        await p.Jobb().KjorAsync();
        var melding = Assert.Single(p.Post.Hent("a"));
        Assert.Contains("Etter ny aktivering", melding.Tekst);
        Assert.DoesNotContain("Mens avmeldt", melding.Tekst);
    }

    [Theory]
    [InlineData(null, "Tentativt: november 2030")]
    [InlineData(Datokvalifikator.Primo, "Tentativt: primo november 2030")]
    [InlineData(Datokvalifikator.Medio, "Tentativt: medio november 2030")]
    [InlineData(Datokvalifikator.Ultimo, "Tentativt: ultimo november 2030")]
    public async Task Tentativ_periode_og_brukerinnhold_bevares_som_trygg_tekst(Datokvalifikator? kvalifikator, string forventet)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var input = Inndata("<script>alert('x')</script> & Gul bok");
        input.Datopresisjon = Datopresisjon.Maaned; input.Datokvalifikator = kvalifikator;
        await p.Skriv.OpprettAsync(input); await p.Jobb().KjorAsync();
        var melding = Assert.Single(p.Post.Hent("a"));
        Assert.Contains(forventet, melding.Tekst); Assert.DoesNotContain("20.11", melding.Tekst);
        Assert.DoesNotContain("13:45", melding.Tekst); Assert.Contains("&lt;script&gt;", melding.Html);
        Assert.DoesNotContain("<script>", melding.Html); Assert.Contains("&amp;", melding.Html);
    }

    [Theory]
    [InlineData("synlighet")]
    [InlineData("avmelding")]
    [InlineData("slettet")]
    [InlineData("ikkeGodkjent")]
    public async Task Endring_mellom_planlegging_og_overlevering_begrenser_utvalget(string handling)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var id = await p.Skriv.OpprettAsync(Inndata("Skal ikke overleveres"));
        var kilde = new Tilgangsadapter(async antall =>
        {
            if (antall == 2)
            {
                if (handling == "avmelding") await p.Abonnement.MeldAvAsync("a");
                else if (handling == "synlighet") await p.Skriv.OppdaterAsync(id, Inndata("Skal ikke overleveres", "POL"));
                else
                {
                    await using var kontekst = db.Fabrikk().CreateDbContext();
                    var frist = await kontekst.Frister.SingleAsync(x => x.Id == id);
                    if (handling == "slettet") kontekst.Frister.Remove(frist); else frist.Status = FristStatus.Forslag;
                    await kontekst.SaveChangesAsync();
                }
            }
            return await p.Tilgang.HentAsync("a");
        });
        await p.Jobb(tilgang: kilde).KjorAsync();
        Assert.Empty(p.Post.Hent("a"));
    }

    [Fact]
    public async Task Siste_tilstand_grupperes_en_gang_og_hver_mottaker_faar_eget_utvalg()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver(); await p.Aktiver("b");
        p.Tilgang.Sett("b", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, ["POL"])));
        var id = await p.Skriv.OpprettAsync(Inndata("Gammel tittel"));
        await p.Skriv.OppdaterAsync(id, Inndata("Siste tittel"));
        await p.Skriv.OpprettAsync(Inndata("Politisk ledelse", "POL"));
        await p.Jobb().KjorAsync();
        var a = Assert.Single(p.Post.Hent("a")); var b = Assert.Single(p.Post.Hent("b"));
        Assert.Contains("Siste tittel", a.Tekst); Assert.DoesNotContain("Gammel", a.Tekst); Assert.DoesNotContain("Politisk", a.Tekst);
        Assert.Contains("Politisk ledelse", b.Tekst); Assert.DoesNotContain("Siste tittel", b.Tekst);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task To_instansers_samtidige_kjoring_vinner_en_plan_og_en_overlevering()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Bare én gang"));
        var begge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kilde = new Tilgangsadapter(async n =>
        {
            if (n == 2) begge.TrySetResult();
            if (n <= 2) await begge.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return await p.Tilgang.HentAsync("a");
        });
        // Opprett først en utsatt plan, så to uavhengige instanser konkurrerer om samme versjon.
        await p.Jobb(post: new UtilgjengeligOppsummeringspost()).KjorAsync();
        var svar = await Task.WhenAll(p.Jobb(tilgang: kilde).KjorAsync(), p.Jobb(tilgang: kilde).KjorAsync());
        Assert.Single(p.Post.Hent("a"));
        Assert.Single(svar.SelectMany(x => x.Mottakere), x => x.Status == "Bekreftet");
        Assert.Single(svar.SelectMany(x => x.Mottakere).Select(x => x.OppsummeringId).Distinct());
    }

    [Fact]
    public async Task To_instansers_foerste_planlegging_bruker_databasens_unike_bruker_og_dato()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Samtidig førstegangsplan"));
        var begge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var n = 0;
        var port = new FoerLagring(async kontekst =>
        {
            if (!kontekst.ChangeTracker.Entries<Epostoppsummering>().Any(x => x.State == EntityState.Added)) return;
            if (Interlocked.Increment(ref n) == 2) begge.TrySetResult();
            await begge.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });
        var svar = await Task.WhenAll(p.Jobb(port).KjorAsync(), p.Jobb(port).KjorAsync());
        Assert.Single(p.Post.Hent("a"));
        Assert.Single(svar.SelectMany(x => x.Mottakere).Select(x => x.OppsummeringId).Distinct());
    }

    [Fact]
    public async Task Avbrudd_etter_vinnende_lagring_bevares_som_uavklart_etter_ny_oppstart()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Avbrutt jobb"));
        var port = new FoerLagring(kontekst =>
        {
            if (kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Simulert prosessavbrudd før overlevering.");
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.Jobb(port).KjorAsync());
        p.Klokke.Flytt(TimeSpan.FromDays(1));
        Assert.Equal("Uavklart", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        Assert.Empty(p.Post.Hent("a"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Avmelding_eller_endret_synlighet_under_siste_lagring_stanser_adapteren(bool synlighet)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var fristId = await p.Skriv.OpprettAsync(Inndata("Siste revalidering")); var endret = false;
        var port = new FoerLagring(async kontekst =>
        {
            if (endret || !kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(x => x.State == EntityState.Added)) return;
            endret = true;
            if (synlighet) await p.Skriv.OppdaterAsync(fristId, Inndata("Siste revalidering", "POL"));
            else await p.Abonnement.MeldAvAsync("a");
        });
        await p.Jobb(port).KjorAsync(); Assert.Empty(p.Post.Hent("a"));
    }

    [Fact]
    public async Task Sen_commit_etter_dagens_bekreftelse_kommer_neste_dag_uten_tapt_hendelse()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var ankommet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = new FoerLagring(async _ => { ankommet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15)); });
        IFristskriving forsinket = new FristskrivingTjeneste(db.Fabrikk(port), p.Klokke);
        var lagring = forsinket.OpprettAsync(Inndata("Sen commit"));
        try
        {
            await ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            p.Klokke.Flytt(TimeSpan.FromMinutes(1)); await p.Skriv.OpprettAsync(Inndata("Rask commit"));
            await p.Jobb().KjorAsync(); fortsett.TrySetResult(); await lagring;
            await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
            p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb().KjorAsync();
            Assert.Equal(2, p.Post.Hent("a").Count);
            Assert.Contains("Sen commit", p.Post.Hent("a")[1].Tekst); Assert.DoesNotContain("Rask commit", p.Post.Hent("a")[1].Tekst);
        }
        finally { fortsett.TrySetResult(); }
    }

    [Fact]
    public async Task Gammel_kjoring_over_dagsskifte_kan_ikke_gjenta_neste_dags_oppsummering()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Over dagsskifte"));
        var startet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kilde = new Tilgangsadapter(async _ =>
        {
            startet.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return await p.Tilgang.HentAsync("a");
        });
        var gammel = p.Jobb(tilgang: kilde).KjorAsync();
        try
        {
            await startet.Task.WaitAsync(TimeSpan.FromSeconds(15)); p.Klokke.Flytt(TimeSpan.FromDays(1));
            await p.Jobb().KjorAsync(); fortsett.TrySetResult(); await gammel;
            Assert.Single(p.Post.Hent("a"));
        }
        finally { fortsett.TrySetResult(); }
    }

    [Fact]
    public async Task Nedetid_samler_etterslep_i_dagens_plan_og_bekreftet_status_gjentas_ikke()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Før nedetid"));
        Assert.Equal("UtilgjengeligTransport", Assert.Single((await p.Jobb(post: new UtilgjengeligOppsummeringspost()).KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromDays(4)); await p.Skriv.OpprettAsync(Inndata("Etter nedetid"));
        var resultat = await p.Jobb().KjorAsync();
        Assert.Equal(new DateOnly(2030, 1, 5), resultat.LokalDato);
        var melding = Assert.Single(p.Post.Hent("a")); Assert.Contains("Før nedetid", melding.Tekst); Assert.Contains("Etter nedetid", melding.Tekst);
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
    }

    [Fact]
    public async Task Mulig_overlevering_og_avbrutt_vinnende_plan_gir_ingen_blind_retry()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Usikkert utfall"));
        var post = new UsikkerPost();
        Assert.Equal("Uavklart", Assert.Single((await p.Jobb(post: post).KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromDays(1));
        Assert.Equal("Uavklart", Assert.Single((await p.Jobb(post: post).KjorAsync()).Mottakere).Status);
        Assert.Equal(1, post.Antall);
    }

    [Fact]
    public async Task Reell_lagringsfeil_ruller_tilbake_baade_publisering_og_hendelse_og_neste_operasjon_virker()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await using (var db = database.Fabrikk().CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer()
                ? "CREATE TRIGGER TR_EV03_fail ON NyFristHendelser AFTER INSERT AS BEGIN THROW 51000, 'EV03 rollback', 1; END"
                : "CREATE TRIGGER TR_EV03_fail AFTER INSERT ON NyFristHendelser BEGIN SELECT RAISE(ABORT, 'EV03 rollback'); END;");
        var skriv = p.Skriv;
        var feil = await Assert.ThrowsAsync<DbUpdateException>(() => skriv.OpprettAsync(Inndata("Mislykket publisering")));
        Assert.True(feil.InnerException?.Message.Contains("EV03 rollback", StringComparison.Ordinal) == true,
            "Prøven krever at den reelle INSERT-triggeren utløser rollback, ikke en annen databasefeil.");
        var lesing = new FristTjeneste(database.Fabrikk(), p.Klokke);
        Assert.Empty(await lesing.HentSynligeAsync(new Synlighetskontekst(true, []), new() { InkluderHistorikk = true }));
        Assert.Equal("IngenHendelser", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        Assert.Empty(p.Post.Hent("a"));
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_EV03_fail");
        await skriv.OpprettAsync(Inndata("Vellykket neste operasjon")); await p.Jobb().KjorAsync();
        var melding = Assert.Single(p.Post.Hent("a")); Assert.Contains("Vellykket neste", melding.Tekst); Assert.DoesNotContain("Mislykket", melding.Tekst);
    }

    [Theory]
    [InlineData("2030-01-01T06:59:00Z", "08:00", true, "2030-01-01")]
    [InlineData("2030-01-01T07:00:00Z", "08:00", false, "2030-01-01")]
    [InlineData("2030-06-01T05:59:00Z", "08:00", true, "2030-06-01")]
    [InlineData("2030-06-01T06:00:00Z", "08:00", false, "2030-06-01")]
    [InlineData("2030-03-31T00:59:00Z", "02:30", true, "2030-03-31")]
    [InlineData("2030-03-31T01:00:00Z", "02:30", false, "2030-03-31")]
    [InlineData("2030-10-27T00:29:00Z", "02:30", true, "2030-10-27")]
    [InlineData("2030-10-27T00:30:00Z", "02:30", false, "2030-10-27")]
    [InlineData("2030-10-27T01:30:00Z", "02:30", false, "2030-10-27")]
    [InlineData("2030-01-01T23:05:00Z", "00:01", false, "2030-01-02")]
    public async Task Konfigurert_tid_foelger_Oslo_dato_og_sommertid(string naa, string tid, bool foer, string dato)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        p.Klokke.Flytt(DateTimeOffset.Parse(naa) - p.Klokke.GetUtcNow());
        var resultat = await p.Jobb(sendetid: TimeOnly.Parse(tid)).KjorAsync();
        Assert.Equal(foer, resultat.FoerSendetid); Assert.Equal(DateOnly.Parse(dato), resultat.LokalDato); Assert.Empty(p.Post.Hent("a"));
    }

    [Fact]
    public async Task Tilbakekalt_tilgang_under_varig_planlegging_stanser_alt_adapterinnhold()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Skjult etter tilbakekall"));
        var port = new FoerLagring(async kontekst =>
        {
            if (kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(x => x.State == EntityState.Added))
            {
                p.Tilgang.Sett("a", new(MottakertilgangStatus.Inaktiv));
                await Task.CompletedTask;
            }
        });
        await p.Jobb(port).KjorAsync();
        Assert.Empty(p.Post.Hent("a"));
    }

    [Fact]
    public async Task Bekreftet_abonnent_faar_flere_nye_frister_i_en_varig_daglig_oppsummering()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Rammefordeling"));
        await p.Skriv.OpprettAsync(Inndata("Gul bok"));
        var resultat = await p.Jobb().KjorAsync();
        Assert.Equal("Bekreftet", Assert.Single(resultat.Mottakere).Status);
        var melding = Assert.Single(p.Post.Hent("a"));
        Assert.Equal("egen@fin.dep.no", melding.Adresse);
        Assert.Contains("Rammefordeling", melding.Tekst); Assert.Contains("Gul bok", melding.Tekst);
        Assert.Contains("20.11.2030 kl. 13:45", melding.Tekst);
        Assert.Contains("Budsjettår 2031", melding.Tekst);
        Assert.Contains("Ny frist", melding.Tekst);
        Assert.Contains("https://kalender.example/kalender", melding.Tekst);
        Assert.Equal("Bekreftet", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        Assert.Single(p.Post.Hent("a"));
    }

    protected static FristInndata Inndata(string tittel, params string[] grupper) => new()
    {
        Tittel = tittel, Dato = new(2030, 11, 20), Klokkeslett = new(13, 45), Budsjettaar = 2031,
        Synlighetskoder = grupper.Length == 0 ? ["FAG"] : grupper.ToList()
    };

    protected sealed class Proeve(Epostdatabase database)
    {
        public readonly EpostabonnementKontrakt.Proeveklokke Klokke = new();
        public readonly KontrollertBekreftelsespost Bekreftelsespost = new();
        public readonly KontrollertOppsummeringspost Post = new();
        public readonly KontrollertMottakertilgang Tilgang = new();
        public IEpostabonnement Abonnement => new EpostabonnementTjeneste(database.Fabrikk(), Klokke, Bekreftelsespost);
        public IFristskriving Skriv => new FristskrivingTjeneste(database.Fabrikk(), Klokke);
        public IDagligEpostoppsummering Jobb(SaveChangesInterceptor? port = null, IMottakertilgangskilde? tilgang = null,
            IOppsummeringspost? post = null, TimeOnly? sendetid = null) => new DagligEpostoppsummering(database.Fabrikk(port), Klokke,
                tilgang ?? Tilgang, post ?? Post, Options.Create(new DagligEpostOpsjoner { Klokkeslett = sendetid ?? new(8, 0) }));
        public async Task Aktiver(string id = "a")
        {
            await Abonnement.MeldPaaAsync(id, "egen@fin.dep.no", true);
            await Abonnement.BekreftAsync(id, Bekreftelsespost.Hent(id)!.Token);
            Klokke.Flytt(TimeSpan.FromTicks(1));
            Tilgang.Sett(id, new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, ["FAG"])));
        }
        public static async Task<Proeve> Opprett(Epostdatabase database)
        {
            await using var db = database.Fabrikk().CreateDbContext();
            db.Synlighetsgrupper.AddRange(Startdata.Standardgrupper.Select(g => new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = g.Kode, Navn = g.Navn }));
            await db.SaveChangesAsync();
            return new(database);
        }
    }

    protected sealed class FoerLagring(Func<AppDbContext, Task> handling) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            await handling((AppDbContext)data.Context!);
            return result;
        }
    }

    private sealed class Tilgangsadapter(Func<int, Task<Mottakertilgang>> handling) : IMottakertilgangskilde
    {
        private int _antall;
        public Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default) => handling(Interlocked.Increment(ref _antall));
    }
    private sealed class UsikkerPost : IOppsummeringspost
    {
        public int Antall;
        public bool Tilgjengelig => true;
        public Task SendAsync(Oppsummeringsmelding melding, CancellationToken ct = default)
        {
            Antall++;
            throw new InvalidOperationException("Usikkert utfall med hemmelig innhold skal ikke logges.");
        }
    }

    private sealed class FoerPlanlesing : DbCommandInterceptor
    {
        private int _ankommet;
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand kommando,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if ((kommando.CommandText.Contains("\"LokalDato\" =", StringComparison.Ordinal)
                || kommando.CommandText.Contains("[LokalDato] =", StringComparison.Ordinal)) && Interlocked.Exchange(ref _ankommet, 1) == 0)
            {
                Ankommet.TrySetResult(); await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }

    private sealed class VentendePost(bool uavklart) : IOppsummeringspost
    {
        private int _antall;
        public int Antall => Volatile.Read(ref _antall);
        public bool Tilgjengelig => true;
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(Oppsummeringsmelding melding, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _antall) != 1) return;
            Ankommet.TrySetResult(); await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            if (uavklart) throw new InvalidOperationException("Kontrollert uavklart utfall.");
        }
    }
}
