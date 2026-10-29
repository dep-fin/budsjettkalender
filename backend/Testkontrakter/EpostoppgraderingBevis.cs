using System.Text.Json;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Aarshjul.Testkontrakter;

public static class EpostoppgraderingBevis
{
    // Autentisk modell for abonnementet før EV05. Bare migrasjonsoppsett bruker den;
    // alle kontroller etter oppgradering og all runtime bruker dagens modell.
    public static IDbContextFactory<AppDbContext> HistoriskFabrikk(Epostdatabase database)
        => HistoriskFabrikk(database.Fabrikk());
    public static IDbContextFactory<AppDbContext> HistoriskFabrikk(IDbContextFactory<AppDbContext> fabrikk)
    {
        using var db = fabrikk.CreateDbContext();
        return new HistoriskAdapter((DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>());
    }
    private sealed class HistoriskAdapter(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    { public AppDbContext CreateDbContext() => new HistoriskKontekst(options); }
    private sealed class HistoriskKontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        { base.OnModelCreating(b); b.Entity<Epostabonnement>().Ignore(x => x.UtvalgJson);
            b.Ignore<FristHendelsessynlighet>(); b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Livssyklussekvens"); }
    }
    public sealed record EV03Plan(Guid Behandlet, Guid Ventende, Guid GammelPlan, Guid VentendePlan);

    public static async Task<EV03Plan> LeggTilEV04PlanAsync(Epostdatabase database)
    {
        var plan = await LeggTilEV03PlanAsync(database, ev04: true);
        await using var db = database.Fabrikk().CreateDbContext();
        var id = await db.NyFristHendelser.Where(h => h.Id == plan.Ventende).Select(h => h.FristId).SingleAsync();
        var skriving = new Aarshjul.Infrastructure.Frister.FristskrivingTjeneste(database.Fabrikk(), new EpostabonnementKontrakt.Proeveklokke());
        var input = (await skriving.HentForRedigeringAsync(id))!; input.Dato = input.Dato.AddDays(1);
        input.VesentligEndring = true; input.Endringsforklaring = "Bevar EV04-forklaring";
        await skriving.OppdaterAsync(id, input);
        await db.Database.ExecuteSqlRawAsync("UPDATE Epostabonnementer SET VesentligeEndringer = 1 WHERE BrukerId = 'eldre'");
        await db.Database.ExecuteSqlRawAsync("UPDATE Epostaktiveringer SET Endringssekvens = 7 WHERE BrukerId = 'eldre'");
        return plan;
    }

    public static async Task<string> HentVarslingsdataAsync(Epostdatabase database)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        async Task<string[]> Rader<T>() where T : class
        {
            var rader = await db.Set<T>().ToArrayAsync();
            return rader.Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))).Order(StringComparer.Ordinal).ToArray();
        }
        return JsonSerializer.Serialize(new
        {
            Hendelser = await Rader<NyFristHendelse>(), Planer = await Rader<Epostoppsummering>(), Koblinger = await Rader<Oppsummeringshendelse>(),
            Sekvens = await db.Database.SqlQueryRaw<long>("SELECT Sekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync(),
            Endringssekvens = await db.Database.SqlQueryRaw<long>("SELECT Endringssekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync(),
            Rekkefolge = await db.Database.SqlQueryRaw<long>("SELECT Verdi AS Value FROM Epostrekkefolge WHERE Id = 1").SingleAsync()
        });
    }

    public static async Task BekreftEV05PlanAsync(Epostdatabase database, EV03Plan plan)
    {
        var kilde = new KontrollertMottakertilgang(); kilde.Sett("eldre", new(MottakertilgangStatus.Aktiv, new Aarshjul.Application.Synlighet.Synlighetskontekst(true, [])));
        var post = new KontrollertOppsummeringspost();
        var jobb = new DagligEpostoppsummering(database.Fabrikk(), new EpostabonnementKontrakt.Proeveklokke(), kilde, post,
            Microsoft.Extensions.Options.Options.Create(new DagligEpostOpsjoner()));
        var resultat = Assert.Single((await jobb.KjorAsync()).Mottakere);
        Assert.Equal("Bekreftet", resultat.Status); Assert.Equal(plan.VentendePlan, resultat.OppsummeringId); Assert.Equal(1, resultat.AntallFrister);
        Assert.Single(post.Hent("eldre"));
        await using var db = database.Fabrikk().CreateDbContext();
        Assert.Equal(3, await db.NyFristHendelser.CountAsync());
        Assert.Equal(3, await db.Oppsummeringshendelser.CountAsync());
        Assert.Equal("Bekreftet", (await db.Epostoppsummeringer.SingleAsync(x => x.Id == plan.GammelPlan)).Status);
    }

    public static async Task<EV03Plan> LeggTilEV03PlanAsync(Epostdatabase database, bool ev04 = false)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        var frister = await db.Frister.Where(x => x.Status == FristStatus.Godkjent).OrderBy(x => x.Tittel).Take(2).Select(x => x.Id).ToListAsync();
        var bevis = new EV03Plan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO NyFristHendelser (Id, FristId, OpprettetUtcTicks, Publiseringssekvens) VALUES ({bevis.Behandlet}, {frister[0]}, {10L}, {6L})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO NyFristHendelser (Id, FristId, OpprettetUtcTicks, Publiseringssekvens) VALUES ({bevis.Ventende}, {frister[1]}, {10L}, {8L})");
        if (ev04)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Epostaktiveringer (BrukerId, Sekvens, Endringssekvens) VALUES ({"eldre"}, {7L}, {0L})");
        else await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Epostaktiveringer (BrukerId, Sekvens) VALUES ({"eldre"}, {7L})");
        await db.Database.ExecuteSqlRawAsync("UPDATE Epostrekkefolge SET Verdi = 9 WHERE Id = 1");
        db.Epostoppsummeringer.Add(new()
        {
            Id = bevis.GammelPlan, BrukerId = "eldre", LokalDato = new(2029, 12, 31), Status = "Bekreftet",
            Hendelser = [new() { HendelseId = bevis.Behandlet }]
        });
        db.Epostoppsummeringer.Add(new() { Id = bevis.VentendePlan, BrukerId = "eldre", LokalDato = new(2030, 1, 1) });
        await db.SaveChangesAsync(); return bevis;
    }

    public static async Task BekreftEV04PlanAsync(Epostdatabase database, EV03Plan bevis)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        var hendelser = await db.NyFristHendelser.OrderBy(x => x.Publiseringssekvens).ToListAsync();
        Assert.Equal(new[] { bevis.Behandlet, bevis.Ventende }, hendelser.Select(x => x.Id));
        Assert.All(hendelser, x => { Assert.Equal("Ny", x.Type); Assert.Null(x.FoerJson); Assert.Null(x.EtterJson); Assert.Null(x.Forklaring); });
        Assert.Equal(new long[] { 6, 8 }, hendelser.Select(x => x.Publiseringssekvens));
        Assert.Equal(7, await db.Database.SqlQueryRaw<long>("SELECT Sekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<long>("SELECT Endringssekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync());
        Assert.Equal("Bekreftet", (await db.Epostoppsummeringer.SingleAsync(x => x.Id == bevis.GammelPlan)).Status);
        Assert.Equal("Planlagt", (await db.Epostoppsummeringer.SingleAsync(x => x.Id == bevis.VentendePlan)).Status);
        Assert.Equal(bevis.Behandlet, (await db.Oppsummeringshendelser.SingleAsync()).HendelseId);
        var kilde = new KontrollertMottakertilgang(); kilde.Sett("eldre", new(MottakertilgangStatus.Aktiv, new Aarshjul.Application.Synlighet.Synlighetskontekst(true, [])));
        var post = new KontrollertOppsummeringspost();
        var jobb = new DagligEpostoppsummering(database.Fabrikk(), new EpostabonnementKontrakt.Proeveklokke(), kilde, post,
            Microsoft.Extensions.Options.Options.Create(new DagligEpostOpsjoner()));
        var resultat = Assert.Single((await jobb.KjorAsync()).Mottakere);
        Assert.Equal("Bekreftet", resultat.Status); Assert.Equal(bevis.VentendePlan, resultat.OppsummeringId);
        Assert.Single(post.Hent("eldre")); Assert.Equal(1, resultat.AntallFrister);
    }
    public static async Task<KontrollertBekreftelsespost> LeggTilAsync(Epostdatabase database)
    {
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var post = new KontrollertBekreftelsespost();
        var paamelding = new EpostabonnementTjeneste(HistoriskFabrikk(database), new EpostabonnementKontrakt.Proeveklokke(), post);
        await paamelding.MeldPaaAsync("eldre", "eldre@fin.dep.no", true);
        await paamelding.MeldPaaAsync("venter", "venter@fin.dep.no", true);
        await using var db = database.Fabrikk().CreateDbContext();
        // Seed autentisk EV02-aktivering før EV03-tabellene finnes.
        var naa = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Epostabonnementer SET Status = 'Aktiv', Aktivert = {naa}, Endret = {naa}, BekreftelsesHash = NULL, BekreftelseUtloeper = NULL WHERE BrukerId = 'eldre'");
        db.Kalenderabonnementer.Add(new() { Id = Guid.NewGuid(), Token = "EV03-syntetisk-feedtoken", Etikett = "Bevar FA", GruppeKode = "FA", Aktiv = true });
        await db.SaveChangesAsync();
        var klokke = new EpostabonnementKontrakt.Proeveklokke();
        var kalender = new InternkalenderTjeneste(database.Fabrikk(), klokke);
        var regler = new GjoeremaalRegelTjeneste(database.Fabrikk());
        await regler.OpprettAsync(new()
        {
            Tittel = "Bevar intern regel", Rundetyper = [Rundetype.Rnb],
            Tidfestingstype = Tidfestingstype.KonkretDato, Maaned = 5, Dag = 20,
            Ansvarlige = [new("eldre", "Eldre bruker"), new(null, "Fagkontakt")]
        });
        var runde = await kalender.GenererRundeAsync(Rundetype.Rnb, 2031, "eldre");
        var fullfoert = await kalender.OpprettGjoeremaalAsync(runde, new()
        {
            Tittel = "Bevar internkalender", Dato = new(2031, 5, 10), Tidfestingstype = Tidfestingstype.KonkretDato,
            Ansvarlige = [new("eldre", "Eldre bruker")]
        });
        await kalender.FullfoerAsync(fullfoert, "eldre", "Eldre bruker");
        await kalender.HurtiglaggAsync(await kalender.HentEllerOpprettOvrigAsync("eldre"), "Bevar stående oppgave");
        return post;
    }

    public static async Task<string> HentAsync(Epostdatabase database)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        async Task<string[]> Rader<T>() where T : class
        {
            await using var historisk = HistoriskFabrikk(database).CreateDbContext();
            var kontekst = typeof(T) == typeof(Epostabonnement) ? historisk : db;
            var rader = await kontekst.Set<T>().ToArrayAsync();
            return rader.Select(r => JsonSerializer.Serialize(kontekst.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))).Order(StringComparer.Ordinal).ToArray();
        }
        return JsonSerializer.Serialize(new { Fasedata = await FasedataBevis.HentAsync(database.Fabrikk()),
            Abonnement = await Rader<Epostabonnement>(), Feed = await Rader<Kalenderabonnement>(), Runder = await Rader<InternRunde>(),
            Gjoeremaal = await Rader<InterntGjoeremaal>(), Ansvar = await Rader<GjoeremaalAnsvarlig>(),
            Regler = await Rader<GjoeremaalRegel>(), Rundetyper = await Rader<RegelRundetype>(), Regelansvar = await Rader<RegelAnsvarlig>() });
    }

    public static async Task BekreftInternvern(Epostdatabase database)
    {
        foreach (var (type, aar) in new[] { (Rundetype.Rnb, (int?)2031), (Rundetype.Ovrig, (int?)null) })
        {
            await using var db = database.Fabrikk().CreateDbContext();
            db.InternRunder.Add(new() { Id = Guid.NewGuid(), Rundetype = type, Aar = aar });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var gammel = database.Fabrikk().CreateDbContext();
        var gammelRunde = await gammel.InternRunder.SingleAsync(r => r.Rundetype == Rundetype.Rnb);
        Assert.NotNull(gammelRunde.SistSynkronisert);
        await using (var fersk = database.Fabrikk().CreateDbContext())
        {
            var runde = await fersk.InternRunder.SingleAsync(r => r.Id == gammelRunde.Id);
            runde.SistSynkronisert = runde.SistSynkronisert!.Value.AddMinutes(1);
            await fersk.SaveChangesAsync();
        }
        gammelRunde.SistSynkronisert = gammelRunde.SistSynkronisert!.Value.AddMinutes(2);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => gammel.SaveChangesAsync());
    }

    public static async Task BekreftIngenTilbakefylling(Epostdatabase database, KontrollertBekreftelsespost bekreftelsespost)
    {
        var klokke = new EpostabonnementKontrakt.Proeveklokke();
        var kilde = new KontrollertMottakertilgang();
        kilde.Sett("eldre", new(MottakertilgangStatus.Aktiv, new Aarshjul.Application.Synlighet.Synlighetskontekst(true, [])));
        var post = new KontrollertOppsummeringspost();
        var jobb = new DagligEpostoppsummering(database.Fabrikk(), klokke, kilde, post,
            Microsoft.Extensions.Options.Options.Create(new DagligEpostOpsjoner()));
        Assert.Equal("IngenHendelser", Assert.Single((await jobb.KjorAsync()).Mottakere).Status);
        Assert.Empty(post.Hent("eldre"));
        Assert.Equal("Aktiv", (await new EpostabonnementTjeneste(database.Fabrikk(), klokke, new KontrollertBekreftelsespost()).HentAsync("eldre")).Status);
        await new Aarshjul.Infrastructure.Frister.FristskrivingTjeneste(database.Fabrikk(), klokke).OpprettAsync(new()
        {
            Tittel = "Ny frist etter EV03-oppgradering", Dato = new(2030, 11, 20), Budsjettaar = 2031, Synlighetskoder = ["FA"]
        });
        // Levert EV02 har implisitt ny-grense 0 og ingen aktiveringsrad. Første
        // utvidelse til endringer må ikke flytte denne grensen og miste ventende Ny.
        var eldreAbonnement = new EpostabonnementTjeneste(database.Fabrikk(), klokke, bekreftelsespost);
        var eldreStatus = await eldreAbonnement.HentAsync("eldre");
        await eldreAbonnement.EndreValgAsync("eldre", true, true, eldreStatus.Versjon);
        Assert.Equal("Bekreftet", Assert.Single((await jobb.KjorAsync()).Mottakere).Status);
        var melding = Assert.Single(post.Hent("eldre"));
        Assert.Contains("Ny frist etter EV03-oppgradering", melding.Tekst);
        Assert.Equal(1, melding.Tekst.Split("Ny frist:", StringSplitOptions.None).Length - 1);
        // Også ventende EV02-bekreftelse/versjon overlever, men aktivering gir ikke etterslep.
        var abonnement = new EpostabonnementTjeneste(database.Fabrikk(), klokke, bekreftelsespost);
        Assert.Equal("VenterPaaBekreftelse", (await abonnement.HentAsync("venter")).Status);
        Assert.Equal("Aktiv", (await abonnement.BekreftAsync("venter", bekreftelsespost.Hent("venter")!.Token)).Status);
        kilde.Sett("venter", new(MottakertilgangStatus.Aktiv, new Aarshjul.Application.Synlighet.Synlighetskontekst(true, [])));
        Assert.Equal("IngenHendelser", (await jobb.KjorAsync()).Mottakere.Single(m => m.BrukerId == "venter").Status);
        Assert.Empty(post.Hent("venter"));
    }
}
