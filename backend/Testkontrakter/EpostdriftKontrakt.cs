using Aarshjul.Infrastructure.Epost;
using Aarshjul.Application.Epost;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Testkontrakter;

public abstract partial class DagligEpostKontrakt
{
    [Fact]
    public async Task EV11_sikkert_usendt_skilles_fra_mulig_overlevering()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Sikker avvisning"));
        Assert.Equal("Feilet", Assert.Single((await p.Jobb(post: new SikkertAvvist()).KjorAsync()).Mottakere).Status);
        Assert.Empty(p.Post.Hent("a"));
    }
    private sealed class SikkertAvvist : IOppsummeringspost
    {
        public bool Tilgjengelig => true;
        public Overleveringskvittering StartOverlevering(Oppsummeringsmelding melding, CancellationToken ct = default)
            => throw new SikkertUsendtException();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV11_sikkert_usendt_avklarer_observerende_instans_uten_aa_avbryte_resten_av_jobben(bool asynkront)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        await p.Aktiver(); await p.Aktiver("b"); await p.Skriv.OpprettAsync(Inndata("Avklart etter observasjon"));
        var klokke = new Forsoeksklokke(p.Klokke.GetUtcNow());
        var adgang = new ForsinketAndreTilgang(p.Tilgang);
        var post = new AvvistEtterObservasjon(asynkront, p.Post);
        var lagrerFeil = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stanset = 0;
        var port = new FoerLagring(async db =>
        {
            if (db.ChangeTracker.Entries<Epostoppsummering>().Any(x => x.Entity.BrukerId == "a" && x.Entity.Status == "Feilet")
                && Interlocked.Exchange(ref stanset, 1) == 0)
            { lagrerFeil.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        });
        IDagligEpostoppsummering Jobb(bool sender) => new DagligEpostoppsummering(database.Fabrikk(sender ? port : null), klokke,
            sender ? adgang : p.Tilgang, sender ? post : p.Post, Options.Create(new DagligEpostOpsjoner()));
        var kjoring = Jobb(true).KjorAsync();
        try
        {
            await adgang.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await klokke.TilgangstimerOpprettet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            klokke.Flytt(TimeSpan.FromSeconds(14)); adgang.Fortsett.TrySetResult();
            await post.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (!asynkront) await lagrerFeil.Task.WaitAsync(TimeSpan.FromSeconds(15));
            else await klokke.KvitteringstimerOpprettet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            // Claim er 31 s gammel; transportens egen 30 s-frist har bare brukt 17 s.
            klokke.Flytt(TimeSpan.FromSeconds(17));
            var observasjon = await Jobb(false).KjorAsync();
            Assert.Contains(observasjon.Mottakere, x => x.BrukerId == "a" && x.Status == "Uavklart");
            var uavklart = await Drift(database, klokke).HentAsync();
            Assert.Contains(uavklart.SisteForsoek, x => x.Status == "Uavklart" && x.Nummer == 1);
            if (asynkront) post.Avvis.TrySetException(new SikkertUsendtException());
            await lagrerFeil.Task.WaitAsync(TimeSpan.FromSeconds(15)); fortsett.TrySetResult();
            var svar = await kjoring.WaitAsync(TimeSpan.FromSeconds(15));
            var a = Assert.Single(svar.Mottakere, x => x.BrukerId == "a"); Assert.Equal("Feilet", a.Status);
            Assert.Contains(svar.Mottakere, x => x.BrukerId == "b" && x.Status == "Bekreftet");
            var feil = await Drift(database, klokke).HentAsync();
            var plan = Assert.Single(feil.UavsluttedePlaner); Assert.Equal(a.OppsummeringId, plan.Id);
            Assert.Equal("Feilet", plan.Status); Assert.Equal(1, plan.AntallForsoek);
            Assert.Equal((klokke.GetUtcNow() + TimeSpan.FromMinutes(5)).UtcTicks, plan.NesteForsoekUtcTicks);
            Assert.Equal("Feilet", Assert.Single(feil.SisteForsoek, x => x.OppsummeringId == plan.Id).Status);
            klokke.Flytt(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
            Assert.Contains((await Jobb(false).KjorAsync()).Mottakere, x => x.BrukerId == "a" && x.Status == "VenterPaaRetry");
            Assert.Empty(p.Post.Hent("a"));
            klokke.Flytt(TimeSpan.FromTicks(1));
            Assert.Contains((await Jobb(false).KjorAsync()).Mottakere, x => x.BrukerId == "a" && x.Status == "Bekreftet");
            await Jobb(false).KjorAsync();
            Assert.Single(p.Post.Hent("a")); Assert.Single(p.Post.Hent("b"));
            var slutt = await Drift(database, klokke).HentAsync(); Assert.Empty(slutt.UavsluttedePlaner);
            Assert.Equal(new[] { "Feilet", "Bekreftet" }, slutt.SisteForsoek.Where(x => x.OppsummeringId == plan.Id).OrderBy(x => x.Nummer).Select(x => x.Status));
        }
        finally { adgang.Fortsett.TrySetResult(); if (asynkront) post.Avvis.TrySetException(new SikkertUsendtException()); fortsett.TrySetResult(); }
    }

    private sealed class ForsinketAndreTilgang(KontrollertMottakertilgang vanlig) : IMottakertilgangskilde
    {
        private int _antall;
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default)
        {
            if (brukerId == "a" && Interlocked.Increment(ref _antall) == 2)
            { Ankommet.TrySetResult(); await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return await vanlig.HentAsync(brukerId, ct);
        }
    }
    private sealed class AvvistEtterObservasjon(bool asynkront, KontrollertOppsummeringspost vanlig) : IOppsummeringspost
    {
        public bool Tilgjengelig => true;
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Avvis = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Overleveringskvittering StartOverlevering(Oppsummeringsmelding melding, CancellationToken ct = default)
        {
            if (melding.BrukerId != "a") return vanlig.StartOverlevering(melding, ct);
            Ankommet.TrySetResult();
            if (!asynkront) throw new SikkertUsendtException();
            return new(Avvis.Task);
        }
    }
    private sealed class Forsoeksklokke(DateTimeOffset naa) : TimeProvider
    {
        private readonly object _laas = new();
        private readonly List<Forsoekstimer> _timere = [];
        public readonly TaskCompletionSource TilgangstimerOpprettet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource KvitteringstimerOpprettet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() { lock (_laas) return naa; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_laas)
            {
                var timer = new Forsoekstimer(this, callback, state); timer.Change(dueTime, period); _timere.Add(timer);
                if (dueTime == TimeSpan.FromSeconds(15)) TilgangstimerOpprettet.TrySetResult();
                if (dueTime == TimeSpan.FromSeconds(30)) KvitteringstimerOpprettet.TrySetResult();
                return timer;
            }
        }
        public void Flytt(TimeSpan tid)
        {
            Forsoekstimer[] timere;
            lock (_laas) { naa += tid; timere = _timere.ToArray(); }
            foreach (var timer in timere) timer.Vekk();
        }
        private sealed class Forsoekstimer(Forsoeksklokke klokke, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? _frist;
            private bool _lukket;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            { lock (klokke._laas) { if (_lukket) return false; _frist = dueTime == Timeout.InfiniteTimeSpan ? null : klokke.GetUtcNow() + dueTime; return true; } }
            public void Vekk()
            {
                lock (klokke._laas) { if (_lukket || _frist is null || _frist > klokke.GetUtcNow()) return; _frist = null; }
                callback(state);
            }
            public void Dispose() { lock (klokke._laas) { _lukket = true; _frist = null; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    [Fact]
    public async Task EV11_sikkert_feilet_historikk_og_forsoek_beholdes_uansett_alder_og_filterendring()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var id = await p.Skriv.OpprettAsync(Inndata("Feilet historikk")); await p.Jobb(post: new SikkertAvvist()).KjorAsync();
        var ny = Inndata("Feilet historikk"); ny.Dato = new(2030, 12, 10); await p.Skriv.OppdaterAsync(id, ny);
        await VelgUtvalg(p, [2040], null, true, false); p.Klokke.Flytt(TimeSpan.FromDays(100));
        await Drift(database, p.Klokke).RyddAsync();
        await using var db = database.Fabrikk().CreateDbContext();
        Assert.NotNull((await db.NyFristHendelser.SingleAsync(h => h.Type == "Ny")).EtterJson);
        Assert.Equal("Feilet", Assert.Single(await db.Epostsendeforsoek.ToListAsync()).Status);
        Assert.Single(await db.Oppsummeringshendelser.ToListAsync());
        Assert.Equal("Feilet", Assert.Single(await db.Epostoppsummeringer.ToListAsync()).Status);
    }
    [Fact]
    public async Task EV11_utsatt_tilgang_har_varig_begrensning_av_nytt_oppslag_over_instans()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Oppslag med pause")); var oppslag = 0;
        var kilde = new Tilgangsadapter(_ => { oppslag++; return Task.FromResult(new Mottakertilgang(MottakertilgangStatus.Uavklart)); });
        IDagligEpostoppsummering Jobb() => new DagligEpostoppsummering(database.Fabrikk(), p.Klokke, kilde, p.Post, Options.Create(new DagligEpostOpsjoner()));
        await Jobb().KjorAsync(); await Jobb().KjorAsync(); Assert.Equal(1, oppslag);
        p.Klokke.Flytt(TimeSpan.FromMinutes(1)); await Jobb().KjorAsync(); Assert.Equal(2, oppslag);
        Assert.Empty((await Drift(database, p.Klokke).HentAsync()).SisteForsoek);
    }

    [Fact]
    public async Task EV11_retry_venter_til_fristen_og_bevarer_budsjett_over_instans_og_nye_dager()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Ventende"));
        Assert.Equal("Feilet", Assert.Single((await p.Jobb(post: new SikkertAvvist()).KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        Assert.Equal("VenterPaaRetry", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromTicks(1));
        Assert.Equal("Feilet", Assert.Single((await p.Jobb(post: new SikkertAvvist()).KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromDays(1));
        Assert.Equal("Feilet", Assert.Single((await p.Jobb(post: new SikkertAvvist()).KjorAsync()).Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Skriv.OpprettAsync(Inndata("Ny etter uttømt budsjett"));
        Assert.Equal("ForsoekUttomt", Assert.Single((await p.Jobb().KjorAsync()).Mottakere).Status);
        await using var db = database.Fabrikk().CreateDbContext();
        Assert.Equal(3, Assert.Single(await db.Epostoppsummeringer.ToListAsync()).AntallForsoek);
        Assert.Equal(3, await db.Epostsendeforsoek.CountAsync()); Assert.Empty(p.Post.Hent("a"));
        Assert.Equal(2, await db.NyFristHendelser.CountAsync());
    }

    [Fact]
    public async Task EV11_retry_samtidige_instansers_ene_vinner_aksepterer_en_gang()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Samtidig retry")); await p.Jobb(post: new SikkertAvvist()).KjorAsync();
        p.Klokke.Flytt(TimeSpan.FromMinutes(5));
        var svar = await Task.WhenAll(p.Jobb().KjorAsync(), p.Jobb().KjorAsync());
        Assert.Single(p.Post.Hent("a"));
        await using var db = database.Fabrikk().CreateDbContext();
        Assert.Equal(2, Assert.Single(await db.Epostoppsummeringer.ToListAsync()).AntallForsoek);
        Assert.Equal(1, await db.Epostsendeforsoek.CountAsync(x => x.Status == "Bekreftet"));
    }

    [Theory]
    [InlineData("adgang")] [InlineData("inaktiv")] [InlineData("uavklart")] [InlineData("kildefeil")]
    [InlineData("avmelding")] [InlineData("adminstans")] [InlineData("adresse")] [InlineData("filter")]
    [InlineData("driftsstans")]
    public async Task EV11_retry_kontrollerer_fersk_adgang_abonnement_og_stans(string endring)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Må kontrolleres på nytt")); await p.Jobb(post: new SikkertAvvist()).KjorAsync();
        p.Klokke.Flytt(TimeSpan.FromMinutes(5)); var status = await p.Abonnement.HentAsync("a");
        IMottakertilgangskilde? kilde = null;
        switch (endring)
        {
            case "adgang": p.Tilgang.Sett("a", new(MottakertilgangStatus.Aktiv, new Synlighetskontekst(false, []))); break;
            case "inaktiv": p.Tilgang.Sett("a", new(MottakertilgangStatus.Inaktiv)); break;
            case "uavklart": p.Tilgang.Sett("a", new(MottakertilgangStatus.Uavklart)); break;
            case "kildefeil": kilde = new Tilgangsadapter(_ => throw new InvalidOperationException("Ikke lagre leverandørtekst")); break;
            case "avmelding": await p.Abonnement.MeldAvAsync("a"); break;
            case "adminstans": await new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke).StansAsync("a", status.Versjon, true, "admin"); break;
            case "adresse": await p.Abonnement.EndreAdresseAsync("a", "ny@jd.dep.no", true, status.Versjon); break;
            case "filter": await p.Abonnement.EndreValgAsync("a", true, false, status.Versjon, [2040], null); break;
            case "driftsstans": var drift = Drift(database, p.Klokke); await drift.SettStansAsync(true, (await drift.HentAsync()).Versjon, true, "admin"); break;
        }
        await p.Jobb(tilgang: kilde).KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal(1, await db.Epostsendeforsoek.CountAsync());
    }

    [Theory]
    [InlineData("2030-03-31T00:59:59Z", "2030-03-31T01:00:00Z")]
    [InlineData("2030-10-27T00:29:59Z", "2030-10-27T00:30:00Z")]
    public async Task EV11_aktive_abonnenter_ved_vaarhull_og_hoestoverlapp_har_en_aksept_og_nye_hendelser_neste_dag(string foer, string ved)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        p.Klokke.Flytt(DateTimeOffset.Parse(foer) - p.Klokke.GetUtcNow()); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Før terskel"));
        Assert.True((await p.Jobb(sendetid: new(2, 30)).KjorAsync()).FoerSendetid); Assert.Empty(p.Post.Hent("a"));
        p.Klokke.Flytt(DateTimeOffset.Parse(ved) - p.Klokke.GetUtcNow());
        await p.Jobb(sendetid: new(2, 30)).KjorAsync(); Assert.Single(p.Post.Hent("a"));
        await p.Skriv.OpprettAsync(Inndata("Etter dagens aksept")); p.Klokke.Flytt(TimeSpan.FromHours(1));
        await p.Jobb(sendetid: new(2, 30)).KjorAsync(); Assert.Single(p.Post.Hent("a"));
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb(sendetid: new(2, 30)).KjorAsync();
        Assert.Equal(2, p.Post.Hent("a").Count);
        var siste = EposttekstBevis.UtenHemmelighet(p.Post.Hent("a").Last().Tekst);
        Assert.Contains("Etter dagens aksept", siste); Assert.DoesNotContain("Før terskel", siste);
    }

    [Fact]
    public async Task EV11_varig_stans_over_ny_instans_og_gjenopptakelse_bevarer_etterslep()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Før stans"));
        var drift = Drift(database, p.Klokke); var foer = await drift.HentAsync();
        await drift.SettStansAsync(true, foer.Versjon, true, "driftsadmin");
        Assert.True((await Drift(database, p.Klokke).HentAsync()).Stanset);
        await p.Skriv.OpprettAsync(Inndata("Publisert under stans")); p.Klokke.Flytt(TimeSpan.FromDays(2));
        await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        var stanset = await drift.HentAsync(); Assert.Equal("driftsadmin", stanset.EndretAv);
        await drift.SettStansAsync(false, stanset.Versjon, true, "driftsadmin");
        await Task.WhenAll(p.Jobb().KjorAsync(), p.Jobb().KjorAsync());
        var tekst = EposttekstBevis.UtenHemmelighet(Assert.Single(p.Post.Hent("a")).Tekst);
        Assert.Contains("Før stans", tekst); Assert.Contains("Publisert under stans", tekst);
    }

    private static Epostdrift Drift(Epostdatabase database, TimeProvider klokke, DagligEpostOpsjoner? opsjoner = null)
        => new(database.Fabrikk(), klokke, Options.Create(opsjoner ?? new()));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV11_tidsavbrudd_bevarer_mulig_overlevering_og_lar_neste_mottaker_fortsette(bool kvittering)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver(); await p.Aktiver("b");
        await p.Skriv.OpprettAsync(Inndata("Begrenset venting")); var klokke = new Fristklokke(p.Klokke.GetUtcNow());
        var transport = new AldriKvittert();
        var kilde = new HengendeTilgang(p.Tilgang, !kvittering);
        var jobb = new DagligEpostoppsummering(database.Fabrikk(), klokke, kilde, kvittering ? transport : p.Post, Options.Create(new DagligEpostOpsjoner()));
        var kjoring = jobb.KjorAsync();
        await klokke.TimerOpprettet.Task.WaitAsync(TimeSpan.FromSeconds(15)); klokke.Flytt(TimeSpan.FromSeconds(31));
        var svar = await kjoring.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains(svar.Mottakere, x => x.BrukerId == "b" && x.Status == "Bekreftet");
        if (kvittering)
        {
            Assert.Contains(svar.Mottakere, x => x.BrukerId == "a" && x.Status == "Uavklart");
            await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a")); Assert.Equal(2, transport.Antall);
        }
        else
        {
            Assert.Contains(svar.Mottakere, x => x.BrukerId == "a" && x.Status == "UavklartTilgang");
            Assert.True(kilde.Avbrutt); Assert.Empty(p.Post.Hent("a"));
        }
    }

    [Fact]
    public async Task EV11_kansellering_etter_mottak_lagrer_uavklart_uten_blind_retry()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Mulig mottatt")); using var ct = new CancellationTokenSource(); var post = new AldriKvittert();
        var kjoring = p.Jobb(post: post).KjorAsync(ct.Token); await post.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15)); ct.Cancel();
        var svar = await kjoring; Assert.Equal("Uavklart", Assert.Single(svar.Mottakere).Status);
        p.Klokke.Flytt(TimeSpan.FromDays(2)); await p.Jobb().KjorAsync(); Assert.Empty(p.Post.Hent("a"));
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal("Uavklart", Assert.Single(await db.Epostsendeforsoek.ToListAsync()).Status);
    }

    [Fact]
    public async Task EV11_opprydding_bevarer_behandlingsmarkorer_ventende_innhold_og_gjeldende_gamle_avmeldingslenke()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var id = await p.Skriv.OpprettAsync(Inndata("Akseptert historikk")); await p.Jobb().KjorAsync();
        var token = Assert.Single(p.Post.Hent("a")).Tekst.Split("/epostvarsling/avmeld#")[1].Trim();
        // En nyere tilstand gjør gammel innholdsmetadata overflødig, mens markører fortsatt trengs.
        var endret = Inndata("Akseptert historikk"); endret.Dato = new(2030, 12, 7); await p.Skriv.OppdaterAsync(id, endret);
        await p.Skriv.OpprettAsync(Inndata("Gammel ventende")); p.Klokke.Flytt(TimeSpan.FromDays(100));
        var drift = Drift(database, p.Klokke); Assert.True(await drift.RyddAsync() > 0); Assert.Equal(0, await drift.RyddAsync());
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            Assert.Single(await db.Epostoppsummeringer.ToListAsync()); Assert.Single(await db.Oppsummeringshendelser.ToListAsync());
            Assert.Empty(await db.Epostsendeforsoek.ToListAsync());
            Assert.Null((await db.NyFristHendelser.SingleAsync(h => h.FristId == id && h.Type == "Ny")).EtterJson);
            Assert.NotNull((await db.NyFristHendelser.SingleAsync(h => h.FristId != id)).EtterJson);
        }
        await Task.WhenAll(p.Jobb().KjorAsync(), p.Jobb().KjorAsync()); Assert.Equal(2, p.Post.Hent("a").Count);
        var tekst = EposttekstBevis.UtenHemmelighet(p.Post.Hent("a").Last().Tekst);
        Assert.Contains("Gammel ventende", tekst); Assert.DoesNotContain("Akseptert historikk", tekst);
        await p.Abonnement.MeldAvMedLenkeAsync(token); Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
    }

    [Fact]
    public async Task EV11_utlopt_bekreftelsesmateriale_ryddes_uten_aa_nullstille_kvote_eller_aktivere()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true); var token = p.Bekreftelsespost.Hent("a")!.Token;
        p.Klokke.Flytt(TimeSpan.FromMinutes(16)); Assert.Equal(1, await Drift(database, p.Klokke).RyddAsync());
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var a = await db.Epostabonnementer.SingleAsync(); Assert.Null(a.BekreftelsesHash); Assert.Null(a.BekreftelseUtloeper);
            Assert.Equal(1, a.AntallSendt); Assert.Equal("VenterPaaBekreftelse", a.Status); Assert.Null(a.Aktivert);
        }
        await Assert.ThrowsAsync<Aarshjul.Application.Valideringsfeil>(() => p.Abonnement.BekreftAsync("a", token));
    }

    private sealed class HengendeTilgang(KontrollertMottakertilgang vanlig, bool heng) : IMottakertilgangskilde
    {
        public bool Avbrutt;
        public Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default)
        {
            if (brukerId != "a" || !heng) return vanlig.HentAsync(brukerId, ct);
            ct.Register(() => Avbrutt = true);
            return new TaskCompletionSource<Mottakertilgang>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }
    }
    private sealed class AldriKvittert : IOppsummeringspost
    {
        public bool Tilgjengelig => true;
        public int Antall;
        public readonly TaskCompletionSource Mottatt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Overleveringskvittering StartOverlevering(Oppsummeringsmelding melding, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Antall); Mottatt.TrySetResult();
            return new(melding.BrukerId == "a" ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task : Task.CompletedTask);
        }
    }
    private sealed class Fristklokke(DateTimeOffset naa) : TimeProvider
    {
        private DateTimeOffset _naa = naa;
        private Fristtimer? _timer;
        public readonly TaskCompletionSource TimerOpprettet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override DateTimeOffset GetUtcNow() => _naa;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new(callback, state); TimerOpprettet.TrySetResult(); return _timer;
        }
        public void Flytt(TimeSpan tid) { _naa += tid; _timer?.Vekk(); }
        private sealed class Fristtimer(TimerCallback callback, object? state) : ITimer
        {
            private bool _lukket;
            public void Vekk() { if (!_lukket) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !_lukket;
            public void Dispose() => _lukket = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV11_global_stans_og_synkront_mottak_respekterer_begge_databaseordninger(bool stansFoerst)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Global lås"));
        var drift = Drift(database, p.Klokke); var versjon = (await drift.HentAsync()).Versjon;
        if (stansFoerst)
        {
            var port = new FoerOverleveringslaas();
            var jobb = new DagligEpostoppsummering(database.FabrikkMedPort(port), p.Klokke, p.Tilgang, p.Post, Options.Create(new DagligEpostOpsjoner()));
            var sending = jobb.KjorAsync();
            try
            {
                await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await drift.SettStansAsync(true, versjon, true, "admin"); port.Fortsett.TrySetResult(); await sending;
                Assert.Empty(p.Post.Hent("a")); Assert.Empty((await drift.HentAsync()).SisteForsoek);
            }
            finally { port.Fortsett.TrySetResult(); await sending; }
        }
        else
        {
            var port = new SisteLesingPort(); var transport = new Grensepost(port);
            var jobb = new DagligEpostoppsummering(database.FabrikkMedKommando(port), p.Klokke, p.Tilgang, transport, Options.Create(new DagligEpostOpsjoner()));
            var sending = jobb.KjorAsync();
            try
            {
                await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
                var stansport = new FoerOverleveringslaas(stans: false);
                var annen = new Epostdrift(database.FabrikkMedPort(stansport), p.Klokke, Options.Create(new DagligEpostOpsjoner()));
                var stans = Task.Run(async () => { await annen.SettStansAsync(true, versjon, true, "admin"); port.Rekkefolge.Enqueue("Stanset"); });
                await stansport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15)); Assert.False(stans.IsCompleted);
                port.Fortsett.TrySetResult(); await transport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await stans.WaitAsync(TimeSpan.FromSeconds(15)); Assert.Equal(new[] { "Overlevert", "Stanset" }, port.Rekkefolge.ToArray());
                transport.Fortsett.TrySetResult(); await sending;
                Assert.Equal("Bekreftet", Assert.Single((await drift.HentAsync()).SisteForsoek).Status);
            }
            finally { port.Fortsett.TrySetResult(); transport.Fortsett.TrySetResult(); await sending; }
        }
        Assert.True((await drift.HentAsync()).Stanset);
    }

    [Fact]
    public async Task EV11_opprydding_ruller_hele_batchen_tilbake_ved_konkurranse_og_kan_gjentas()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true); p.Klokke.Flytt(TimeSpan.FromMinutes(16));
        var feil = new FoerLagring(_ => throw new InvalidOperationException("Kontrollert batchfeil"));
        var drift = new Epostdrift(database.Fabrikk(feil), p.Klokke, Options.Create(new DagligEpostOpsjoner()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => drift.RyddAsync());
        await using (var db = database.Fabrikk().CreateDbContext()) Assert.NotNull((await db.Epostabonnementer.SingleAsync()).BekreftelsesHash);
        Assert.Equal(1, await Drift(database, p.Klokke).RyddAsync()); Assert.Equal(0, await Drift(database, p.Klokke).RyddAsync());
    }

    [Fact]
    public async Task EV11_retensjon_bevarer_slettingssynlighet_siste_tilstand_og_livslang_Ny_identitet()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await VelgUtvalg(p, null, null, true, true);
        var import = new Aarshjul.Infrastructure.Datautveksling.FristDatautveksling(database.Fabrikk(), p.Klokke);
        var rad = Importrad("Etablert før bortfall");
        await import.ImporterAsync(new() { Frister = [rad] }); await p.Jobb().KjorAsync();
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await import.ImporterAsync(new() { Frister = [] }); p.Klokke.Flytt(TimeSpan.FromDays(100));
        await Drift(database, p.Klokke).RyddAsync(); await p.Jobb().KjorAsync();
        Assert.Contains("Fjernet frist: Etablert før bortfall", EposttekstBevis.UtenHemmelighet(p.Post.Hent("a").Last().Tekst));
        await import.ImporterAsync(new() { Frister = [rad with { Dato = new(2030, 12, 12) }] });
        var kortlevd = Importrad("Ny og fjernet"); await import.ImporterAsync(new() { Frister = [rad, kortlevd] });
        await import.ImporterAsync(new() { Frister = [rad] }); p.Klokke.Flytt(TimeSpan.FromDays(100));
        await Drift(database, p.Klokke).RyddAsync(); await Task.WhenAll(p.Jobb().KjorAsync(), p.Jobb().KjorAsync());
        var siste = EposttekstBevis.UtenHemmelighet(p.Post.Hent("a").Last().Tekst);
        Assert.Contains("Vesentlig endring: Etablert før bortfall", siste); Assert.DoesNotContain("Ny og fjernet", siste);
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal(1, await db.NyFristHendelser.CountAsync(h => h.FristId == rad.Id && h.Type == "Ny"));
    }
}
