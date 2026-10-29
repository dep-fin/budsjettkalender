using Aarshjul.Application;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Testkontrakter;

public abstract partial class DagligEpostKontrakt
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV10_ny_uttrykkelig_paamelding_taper_ikke_for_en_gammel_dialog_paa_avmeldt_abonnement(bool adminFoerst)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke);
        await admin.StansAsync("a", (await p.Abonnement.HentAsync("a")).Versjon, true, "foerste-admin");
        var avmeldt = await p.Abonnement.HentAsync("a"); p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        if (adminFoerst)
        {
            var brukt = false;
            var port = new FoerLagring(async _ =>
            {
                if (brukt) return; brukt = true;
                await admin.StansAsync("a", avmeldt.Versjon, true, "gammel-dialog");
            });
            await new EpostabonnementTjeneste(database.Fabrikk(port), p.Klokke, p.Bekreftelsespost).MeldPaaAsync("a", "egen@fin.dep.no", true);
            Assert.True(brukt);
        }
        else
        {
            var port = new FoerOverleveringslaas();
            var stans = new Epostabonnentadministrasjon(database.FabrikkMedPort(port), p.Klokke).StansAsync("a", avmeldt.Versjon, true, "gammel-dialog");
            try
            {
                await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
                port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => stans);
            }
            finally { port.Fortsett.TrySetResult(); }
        }
        Assert.Equal("VenterPaaBekreftelse", (await p.Abonnement.HentAsync("a")).Status);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        Assert.Equal("Aktiv", (await p.Abonnement.HentAsync("a")).Status);
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal("foerste-admin", Assert.Single(await db.Epostadministratorstanser.ToListAsync()).AktoerId);
    }
    [Fact]
    public async Task EV10_siste_stans_foelger_varig_rekkefolge_ogsaa_naar_UTC_gaar_bakover()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke);
        await admin.StansAsync("a", (await p.Abonnement.HentAsync("a")).Versjon, true, "foerste-admin");
        p.Klokke.Flytt(TimeSpan.FromMinutes(2)); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        p.Klokke.Flytt(TimeSpan.FromMinutes(-10));
        await admin.StansAsync("a", (await p.Abonnement.HentAsync("a")).Versjon, true, "siste-admin");
        var siste = Assert.Single(await admin.HentAsync());
        Assert.Equal("siste-admin", siste.SistStansetAv); Assert.Equal(p.Klokke.GetUtcNow(), siste.SistStanset);
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal(2, await db.Epostadministratorstanser.CountAsync());
    }
    [Theory]
    [InlineData(true, false, false)] [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(true, false, true)] [InlineData(false, true, true)] [InlineData(true, true, true)]
    public async Task EV10_administratorstans_og_ny_aktivering_stanser_alle_hendelsestyper_uten_inaktivt_etterslep(bool nye, bool endringer, bool alle)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rader = new List<FristEksport>();
        foreach (var fase in new[] { "før avmelding", "under venting", "etter aktivering" })
        {
            rader.Add(Importrad("Endring " + fase)); rader.Add(Importrad("Sletting " + fase));
        }
        await import.ImporterAsync(new() { Frister = rader }); await p.Aktiver();
        rader.Add(Importrad("Faktisk tidligere post")); await import.ImporterAsync(new() { Frister = rader }); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a")));
        await VelgUtvalg(p, alle ? null : [2031], alle ? null : [Kategori.Budsjett], nye, endringer);
        async Task Publiser(string fase)
        {
            rader.Add(Importrad("Ny " + fase));
            var i = rader.FindIndex(r => r.Tittel == "Endring " + fase);
            rader[i] = rader[i] with { Dato = new(2030, 12, 5) };
            rader.RemoveAll(r => r.Tittel == "Sletting " + fase);
            await import.ImporterAsync(new() { Frister = rader });
        }
        await Publiser("før avmelding"); await AdminStans(p, db);
        await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        await Publiser("under venting"); await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true, nye, endringer,
            alle ? null : [2031], alle ? null : [Kategori.Budsjett]);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        await Publiser("etter aktivering");
        rader.Add(Importrad("Annet utvalg etter aktivering") with { Budsjettaar = 2040, Kategori = Kategori.Regnskap });
        await import.ImporterAsync(new() { Frister = rader });
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb().KjorAsync();
        Assert.Equal(2, p.Post.Hent("a").Count); var melding = p.Post.Hent("a")[1];
        Assert.Equal(nye, melding.Tekst.Contains("Ny etter aktivering", StringComparison.Ordinal));
        Assert.Equal(endringer, melding.Tekst.Contains("Endring etter aktivering", StringComparison.Ordinal));
        Assert.Equal(endringer, melding.Tekst.Contains("Sletting etter aktivering", StringComparison.Ordinal));
        Assert.Equal(alle && nye, melding.Tekst.Contains("Annet utvalg etter aktivering", StringComparison.Ordinal));
        Assert.DoesNotContain("før avmelding", EposttekstBevis.UtenHemmelighet(melding.Tekst)); Assert.DoesNotContain("under venting", EposttekstBevis.UtenHemmelighet(melding.Tekst));
    }

    [Fact]
    public async Task EV10_oversikt_viser_bare_abonnenter_med_faktiske_valg_bekreftelse_og_stabil_identitet()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            db.Brukere.AddRange(new Bruker { Id = "a", Navn = "Ada" }, new Bruker { Id = "uten", Navn = "Ikke abonnent" });
            await db.SaveChangesAsync();
        }
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke);
        Assert.Empty(await admin.HentAsync());
        await p.Aktiver(); await p.Abonnement.MeldPaaAsync("venter", "venter@fin.dep.no", true);
        var status = await p.Abonnement.HentAsync("a");
        await p.Abonnement.EndreValgAsync("a", false, true, status.Versjon, [2033, 2031], [Kategori.Regnskap]);
        var rader = await admin.HentAsync(); Assert.Equal(new[] { "a", "venter" }, rader.Select(x => x.BrukerId));
        var a = rader[0]; Assert.Equal("Ada", a.Navn); Assert.True(a.AdresseBekreftet); Assert.False(a.NyeFrister); Assert.True(a.VesentligeEndringer);
        Assert.Equal(new[] { 2031, 2033 }, a.Budsjettaar); Assert.Equal(new[] { Kategori.Regnskap }, a.Kategorier);
        var v = rader[1]; Assert.Equal("venter", v.Navn); Assert.False(v.AdresseBekreftet); Assert.True(v.NyeFrister); Assert.False(v.VesentligeEndringer);
        Assert.Null(v.Budsjettaar); Assert.Null(v.Kategorier); Assert.Null(v.SistStanset);
        await p.Abonnement.MeldAvAsync("a"); a = (await admin.HentAsync())[0];
        Assert.Equal("Avmeldt", a.Status); Assert.True(a.AdresseBekreftet); Assert.Null(a.SistStanset);
        await using var kontroll = database.Fabrikk().CreateDbContext();
        Assert.Equal(2, await kontroll.Brukere.CountAsync()); Assert.Equal(2, await kontroll.Epostabonnementer.CountAsync());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV10_bekreftet_stans_er_varig_bevarer_valg_roller_grupper_og_tillater_ny_paa_melding(bool aktiv)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            db.Brukere.Add(new Bruker { Id = "a", Navn = "Abonnent", Funksjonsrolle = Funksjonsrolle.Administrator,
                AdminViaEntra = true, Grupper = [new() { BrukerId = "a", GruppeKode = "POL", Kilde = GruppeMedlemskapKilde.Manuell }] });
            await db.SaveChangesAsync();
        }
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true, true, true, [2031], [Kategori.Budsjett]);
        var token = p.Bekreftelsespost.Hent("a")!.Token;
        if (aktiv) await p.Abonnement.BekreftAsync("a", token);
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke); var foer = Assert.Single(await admin.HentAsync());
        await Assert.ThrowsAsync<Valideringsfeil>(() => admin.StansAsync("a", foer.Versjon, false, "admin-1"));
        Assert.Equivalent(foer, Assert.Single(await admin.HentAsync()), strict: true);
        await admin.StansAsync("a", foer.Versjon, true, "admin-1");
        var etter = Assert.Single(await new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke).HentAsync());
        Assert.Equal("Avmeldt", etter.Status); Assert.Equal(aktiv, etter.AdresseBekreftet);
        Assert.Equal(foer.Opprettet, etter.Opprettet); Assert.Equal(foer.Aktivert, etter.Aktivert);
        Assert.Equal(foer.Budsjettaar, etter.Budsjettaar); Assert.Equal(foer.Kategorier, etter.Kategorier);
        Assert.Equal("admin-1", etter.SistStansetAv); Assert.Equal(p.Klokke.GetUtcNow(), etter.SistStanset);
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => admin.StansAsync("a", foer.Versjon, true, "admin-2"));
        await admin.StansAsync("a", etter.Versjon, true, "admin-2"); // Ingen falsk ny hendelse.
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Abonnement.BekreftAsync("a", token));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2)); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        var ny = Assert.Single(await admin.HentAsync()); Assert.Equal("Aktiv", ny.Status); Assert.Equal("admin-1", ny.SistStansetAv);
        await using var kontroll = database.Fabrikk().CreateDbContext();
        Assert.Single(await kontroll.Epostadministratorstanser.ToListAsync());
        var bruker = await kontroll.Brukere.Include(x => x.Grupper).SingleAsync();
        Assert.Equal(Funksjonsrolle.Administrator, bruker.Funksjonsrolle); Assert.True(bruker.AdminViaEntra);
        Assert.Equal("POL", Assert.Single(bruker.Grupper).GruppeKode); Assert.Equal(GruppeMedlemskapKilde.Manuell, Assert.Single(bruker.Grupper).Kilde);
    }

    [Fact]
    public async Task EV10_manglende_maal_og_manglende_aktoer_eller_versjon_oppretter_intet_spor()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke); var foer = Assert.Single(await admin.HentAsync());
        await Assert.ThrowsAsync<Valideringsfeil>(() => admin.StansAsync("ukjent", Guid.NewGuid(), true, "admin"));
        await Assert.ThrowsAsync<Valideringsfeil>(() => admin.StansAsync("a", foer.Versjon, true, ""));
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => admin.StansAsync("a", Guid.Empty, true, "admin"));
        Assert.Equivalent(foer, Assert.Single(await admin.HentAsync()), strict: true);
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Empty(await db.Epostadministratorstanser.ToListAsync());
    }

    [Fact]
    public async Task EV10_to_administratorer_med_samme_versjon_faar_en_vinner_og_ett_revisjonsspor()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke); var foer = Assert.Single(await admin.HentAsync());
        async Task<bool> Stans(string id)
        {
            try { await new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke).StansAsync("a", foer.Versjon, true, id); return true; }
            catch (EpostabonnementKonflikt) { return false; }
        }
        var utfall = await Task.WhenAll(Task.Run(() => Stans("admin-1")), Task.Run(() => Stans("admin-2")));
        Assert.Single(utfall, x => x); Assert.Single(utfall, x => !x);
        await using var db = database.Fabrikk().CreateDbContext();
        var spor = Assert.Single(await db.Epostadministratorstanser.ToListAsync());
        Assert.Equal(utfall[0] ? "admin-1" : "admin-2", spor.AktoerId); Assert.Equal("Stanset", spor.Handling);
        Assert.Equal("Avmeldt", Assert.Single(await admin.HentAsync()).Status);
    }

    [Theory]
    [InlineData("valg", false)] [InlineData("valg", true)]
    [InlineData("adresse", false)] [InlineData("adresse", true)]
    [InlineData("bekreftelse", false)] [InlineData("bekreftelse", true)]
    [InlineData("gjenpaamelding", false)] [InlineData("gjenpaamelding", true)]
    public async Task EV10_stans_og_eierhandling_respekterer_begge_vinnerrekkefolger(string handling, bool adminFoerst)
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        if (handling is "bekreftelse" or "gjenpaamelding")
        {
            var status = await p.Abonnement.HentAsync("a");
            if (handling == "bekreftelse") await p.Abonnement.EndreAdresseAsync("a", "ny@jd.dep.no", true, status.Versjon);
            else { await p.Abonnement.MeldAvAsync("a"); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true); }
        }
        var foer = await p.Abonnement.HentAsync("a"); var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke);
        async Task Eierhandling(IEpostabonnement tjeneste)
        {
            if (handling is "bekreftelse" or "gjenpaamelding") await tjeneste.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
            else if (handling == "adresse") await tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, foer.Versjon);
            else await tjeneste.EndreValgAsync("a", true, true, foer.Versjon, [2031], [Kategori.Budsjett]);
        }
        if (adminFoerst)
        {
            var port = new FoerLagring(async _ => await admin.StansAsync("a", foer.Versjon, true, "admin"));
            var forsinket = new EpostabonnementTjeneste(database.Fabrikk(port), p.Klokke, p.Bekreftelsespost);
            await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => Eierhandling(forsinket));
            Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
        }
        else
        {
            var port = new FoerOverleveringslaas();
            var forsinket = new Epostabonnentadministrasjon(database.FabrikkMedPort(port), p.Klokke);
            var stans = forsinket.StansAsync("a", foer.Versjon, true, "admin");
            try
            {
                await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15)); await Eierhandling(p.Abonnement);
                port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => stans);
            }
            finally { port.Fortsett.TrySetResult(); }
            Assert.Equal(handling is "bekreftelse" or "gjenpaamelding" ? "Aktiv" : handling == "adresse" ? "VenterPaaBekreftelse" : "Aktiv", (await p.Abonnement.HentAsync("a")).Status);
        }
        await using var db = database.Fabrikk().CreateDbContext(); Assert.Equal(adminFoerst ? 1 : 0, await db.Epostadministratorstanser.CountAsync());
    }

    [Fact]
    public async Task EV10_feil_i_revisjonslagring_ruller_stans_tilbake_og_samme_tjeneste_kan_proeve_igjen()
    {
        await using var database = await OpprettAsync(); var p = await Proeve.Opprett(database); await p.Aktiver();
        await using (var db = database.Fabrikk().CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer()
                ? "CREATE TRIGGER TR_EV10_fail ON Epostadministratorstanser AFTER INSERT AS BEGIN THROW 51000, 'Kontrollert feil', 1; END"
                : "CREATE TRIGGER TR_EV10_fail BEFORE INSERT ON Epostadministratorstanser BEGIN SELECT RAISE(ABORT, 'Kontrollert feil'); END");
        var admin = new Epostabonnentadministrasjon(database.Fabrikk(), p.Klokke); var foer = Assert.Single(await admin.HentAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => admin.StansAsync("a", foer.Versjon, true, "admin"));
        Assert.Equivalent(foer, Assert.Single(await admin.HentAsync()), strict: true);
        await using (var db = database.Fabrikk().CreateDbContext())
        { Assert.Empty(await db.Epostadministratorstanser.ToListAsync()); await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_EV10_fail"); }
        await admin.StansAsync("a", foer.Versjon, true, "admin"); Assert.Equal("Avmeldt", Assert.Single(await admin.HentAsync()).Status);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task EV10_administratorstans_ved_begge_sistekontroller_stanser_koelagt_oppsummering(int kontroll)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Tidligere post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Kølagt")); var brukt = false;
        var port = new FoerLagring(async kontekst =>
        {
            if (brukt) return;
            var koblinger = kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(x => x.State == EntityState.Added);
            var sending = kontekst.ChangeTracker.Entries<Epostoppsummering>().Any(x => x.State == EntityState.Modified && x.Entity.Status == "UnderSending");
            if (kontroll == 1 ? !sending || koblinger : !koblinger) return;
            brukt = true; await AdminStans(p, db);
        });
        await p.Jobb(port).KjorAsync(); Assert.True(brukt); Assert.Single(p.Post.Hent("a"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EV10_administratorstans_bevarer_overlevert_UnderSending_og_Uavklart_uten_blind_retry(bool uavklart)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Overlevert")); var transport = new VentendePost(uavklart);
        var sending = p.Jobb(post: transport).KjorAsync();
        try
        {
            await transport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await AdminStans(p, db);
            Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            await using (var les = db.Fabrikk().CreateDbContext())
            {
                var plan = await les.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2));
                Assert.Equal("UnderSending", plan.Status); Assert.True(plan.AvmeldingsHash?.Length == 64);
                Assert.Single(await les.Oppsummeringshendelser.Where(h => h.OppsummeringId == plan.Id).ToListAsync());
            }
            transport.Fortsett.TrySetResult(); await sending;
            await p.Jobb(post: transport).KjorAsync(); Assert.Equal(1, transport.Antall);
            await using var slutt = db.Fabrikk().CreateDbContext();
            Assert.Equal(uavklart ? "Uavklart" : "Bekreftet", (await slutt.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2))).Status);
        }
        finally { transport.Fortsett.TrySetResult(); await sending; }
    }

    [Fact]
    public async Task EV10_administratorstans_som_vinner_foer_overleveringslaasen_stanser_siste_gap_paa_to_instanser()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Kølagt siste gap"));
        var port = new FoerOverleveringslaas();
        var jobb = new DagligEpostoppsummering(db.FabrikkMedPort(port), p.Klokke, p.Tilgang, p.Post, Options.Create(new DagligEpostOpsjoner()));
        var sending = jobb.KjorAsync();
        try
        {
            await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await AdminStans(p, db);
            port.Fortsett.TrySetResult(); await sending;
            Assert.Single(p.Post.Hent("a")); Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            await using var les = db.Fabrikk().CreateDbContext();
            Assert.Equal("Planlagt", (await les.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2))).Status);
        }
        finally { port.Fortsett.TrySetResult(); await sending; }
    }

    [Fact]
    public async Task EV10_siste_abonnementslesing_og_synkront_transportmottak_holder_samme_databaselaas()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Overleveringsgrensen"));
        var versjon = (await p.Abonnement.HentAsync("a")).Versjon;
        var port = new SisteLesingPort();
        var transport = new Grensepost(port);
        var jobb = new DagligEpostoppsummering(db.FabrikkMedKommando(port), p.Klokke, p.Tilgang, transport, Options.Create(new DagligEpostOpsjoner()));
        var sending = jobb.KjorAsync();
        try
        {
            // Porten venter etter den siste SELECT-en mens skrivelåsen faktisk holdes.
            await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var avmeldingsport = new FoerOverleveringslaas(stans: false);
            var annenInstans = new Epostabonnentadministrasjon(db.FabrikkMedPort(avmeldingsport), p.Klokke);
            var avmelding = Task.Run(async () =>
            {
                await annenInstans.StansAsync("a", versjon, true, "admin");
                port.Rekkefolge.Enqueue("Avmeldt");
            });
            await avmeldingsport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(avmelding.IsCompleted);
            port.Fortsett.TrySetResult();
            await transport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await avmelding.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(new[] { "Overlevert", "Avmeldt" }, port.Rekkefolge.ToArray());
            Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            transport.Fortsett.TrySetResult(); await sending;
        }
        finally { port.Fortsett.TrySetResult(); transport.Fortsett.TrySetResult(); await sending; }
    }


    private static async Task AdminStans(Proeve p, Epostdatabase db)
    {
        var status = await p.Abonnement.HentAsync("a");
        await new Epostabonnentadministrasjon(db.Fabrikk(), p.Klokke).StansAsync("a", status.Versjon, true, "admin");
    }

}
