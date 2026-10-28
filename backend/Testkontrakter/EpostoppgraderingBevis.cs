using System.Text.Json;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Testkontrakter;

public static class EpostoppgraderingBevis
{
    public static async Task<KontrollertBekreftelsespost> LeggTilAsync(Epostdatabase database)
    {
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var post = new KontrollertBekreftelsespost();
        var paamelding = new EpostabonnementTjeneste(database.Fabrikk(), new EpostabonnementKontrakt.Proeveklokke(), post);
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
            var rader = await db.Set<T>().ToArrayAsync();
            return rader.Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
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
